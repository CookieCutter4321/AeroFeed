using AeroFeed.Server.Models;
using Confluent.Kafka;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;


namespace AeroFeed.Server.Workers
{
    public class Producer : BackgroundService
    {
        public static readonly JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        private static readonly HttpClient client = new()
        {
            DefaultRequestHeaders =
            {
                { "User-Agent", "AeroFeed/1.0" }
            }
        };

        private readonly IConfiguration _config;
        private readonly ProducerConfig _producerConfig;
        public Producer(IConfiguration config)
        {
            _config = config;
            string certFolder = _config["KAFKA_CERT_LOCATION"];

            _producerConfig = new ProducerConfig
            {
                BootstrapServers = "aerofeed-kafka-jw9007235-5415.l.aivencloud.com:24013",
                SecurityProtocol = SecurityProtocol.Ssl,

                // truststore (CA)
                SslCaLocation = Path.Combine(certFolder, "ca.pem"),

                // keystore (Service Cert + Key)
                SslCertificateLocation = Path.Combine(certFolder, "service.cert"),
                SslKeyLocation = Path.Combine(certFolder, "service.key"),

                // Compression settings
                CompressionType = CompressionType.Lz4,
                LingerMs = 100,
                BatchSize = 64 * 1024, // 64 KB
            };
        }

        private readonly SemaphoreSlim _kafkaThrottle = new(2, 2); // Two seems to be the sweet spot to not let the lag get out of hand.
        protected override async Task ExecuteAsync(CancellationToken stoppingToken) 
        {

            using var producer = new ProducerBuilder<string, string>(_producerConfig)
                .SetKeySerializer(Serializers.Utf8)
                .SetValueSerializer(Serializers.Utf8)
                .Build();
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    //Testing: https://stream.wikimedia.org/v2/stream/recentchange?since=2026-03-25
                    using var stream = await client.GetStreamAsync("https://stream.wikimedia.org/v2/stream/recentchange", stoppingToken);
                    using var reader = new StreamReader(stream);
                    //TODO: can we shrink the payload size by modifying the json? although this has CPU implications.
                    //TODO: As for redis, we can just store the relevant data in its specific timeframe. then expire in 3 days or something.
                    //No need to do anything fancy like deduplication for now, the scope of this project will not be able to reach that amount of bandwidth,
                    //especially for replaying.
                    //Maybe an entries per second counter to log to the client as well =)
                    int n = 0;
                    long lastOffset = 0;
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        string? line = await reader.ReadLineAsync(stoppingToken);

                        if (string.IsNullOrEmpty(line)) continue;
                        if (!line.StartsWith("data: ")) continue;
                        if (n >= 50) { DisplayLagInfo(line, client, stoppingToken); n = 0; }

                        await _kafkaThrottle.WaitAsync(stoppingToken);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var deliveryResult = await producer.ProduceAsync("RecentChanges", new Message<string, string>
                                {
                                    Key = Guid.NewGuid().ToString(),
                                    Value = line[6..]
                                }, stoppingToken);
                                n++;
                                DisplayProduceAsyncStatus(deliveryResult);
                            } finally
                            {
                                _kafkaThrottle.Release();
                            }
                        });

                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Connection lost: {ex.Message}. Retrying in 5 seconds...");
                    await Task.Delay(5000, stoppingToken);
                }
            }

        }
        public async void DisplayLagInfo(string? line, HttpClient client, CancellationToken stoppingToken)
        {
            try
            {
                using var temp_stream = await client.GetStreamAsync("https://stream.wikimedia.org/v2/stream/recentchange", stoppingToken);
                using var temp_reader = new StreamReader(temp_stream);

                string? temp_line;
                while ((temp_line = await temp_reader.ReadLineAsync(stoppingToken)) != null)
                {
                    if (temp_line.StartsWith("data: "))
                    {
                        var latest = JsonSerializer.Deserialize<RecentChange>(temp_line.AsSpan(6), options);
                        var current = JsonSerializer.Deserialize<RecentChange>(line.AsSpan(6), options);

                        if (current != null && latest != null)
                        {
                            Console.WriteLine($"LAG: {latest.Meta.Offset - current.Meta.Offset} messages");
                        }
                        break;
                    }
                }
            }
            catch (Exception ex) { }
        }

        public void DisplayProduceAsyncStatus (DeliveryResult<string,string> deliveryResult)
        {
            switch (deliveryResult.Status)
            {
                case PersistenceStatus.Persisted: // No need to log if messages are delivered successfully.
                    //Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [INFO] message persisted to Kafka.");
                    break;
                case PersistenceStatus.NotPersisted:
                    Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [WARN] message not persisted to Kafka");
                    break;
                default:
                    Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [ERROR] unexpected status {deliveryResult.Status} when sending message to Kafka");
                    break;
            }
        }
    }
}
