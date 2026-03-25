using AeroFeed.Server.Models;
using Confluent.Kafka;
using System.Linq.Expressions;
using System.Text.Json;

namespace AeroFeed.Server.Workers
{
    public class Consumer : BackgroundService
    {
        public static readonly JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        private readonly IConfiguration _config;
        private ConsumerConfig _consumerConfig;
        public Consumer(IConfiguration config)
        {
            _config = config;
            string certFolder = _config["KAFKA_CERT_LOCATION"];

            _consumerConfig = new ConsumerConfig
            {
                BootstrapServers = "aerofeed-kafka-jw9007235-5415.l.aivencloud.com:24013",
                SecurityProtocol = SecurityProtocol.Ssl,

                // truststore (CA)
                SslCaLocation = Path.Combine(certFolder, "ca.pem"),

                // keystore (Service Cert + Key)
                SslCertificateLocation = Path.Combine(certFolder, "service.cert"),
                SslKeyLocation = Path.Combine(certFolder, "service.key"),

                GroupId = "aerofeed-recent-changes-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                SessionTimeoutMs = 45000,
                EnableAutoCommit = true,
            };
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            using var consumer = new ConsumerBuilder<string, string>(_consumerConfig)
                .SetKeyDeserializer(Deserializers.Utf8)
                .SetValueDeserializer(Deserializers.Utf8)
                .Build();

            consumer.Subscribe("RecentChanges");
            try
            {
                int n = 0;
                while (!stoppingToken.IsCancellationRequested)
                {
                    var consumeResult = consumer.Consume(stoppingToken);

                    if (consumeResult?.Message?.Value is null) continue;

                    var result = JsonSerializer.Deserialize<RecentChange>(consumeResult.Message.Value, options);

                    Console.WriteLine($"CONSUMER | {n += 1}: {result.Meta.Id}");
                }
            }
            catch (OperationCanceledException)
            {

            }
            finally
            {
                consumer.Close();
            }
        }
    }
}
