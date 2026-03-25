using System.Linq.Expressions;
using System.Text.Json;
using AeroFeed.Server.Models;
using Confluent.Kafka;

namespace AeroFeed.Server.Workers
{
    public class Producer : BackgroundService
    {
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
                    using var stream = await client.GetStreamAsync("https://stream.wikimedia.org/v2/stream/recentchange", stoppingToken);
                    using var reader = new StreamReader(stream);

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        string? line = await reader.ReadLineAsync(stoppingToken);

                        if (string.IsNullOrEmpty(line)) continue;
                        if (!line.StartsWith("data: ")) continue;

                        var deliveryResult = await producer.ProduceAsync("RecentChanges", new Message<string, string>
                        {
                            Key = Guid.NewGuid().ToString(),
                            Value = line[6..]
                        }, stoppingToken);
                        Console.WriteLine("PRODUCER | Sent message to Kafka");
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Connection lost: {ex.Message}. Retrying in 5 seconds...");
                    await Task.Delay(5000, stoppingToken);
                }
            }

        }
    }
}
