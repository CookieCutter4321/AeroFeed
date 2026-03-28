using AeroFeed.Server.Hubs;
using AeroFeed.Server.Models;
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using System.Data.Common;
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
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ConsumerConfig _consumerConfig;
        public Consumer(IConfiguration config, IHubContext<NotificationHub> hubContext)
        {
            _config = config;
            _hubContext = hubContext;

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

        /*
         * Will not work with multiple consumers (such as if we are utilizing partitioning) since we are just keeping a single global state here.
        */
        RecentChangeAnalytics data = new();

        private void UpdateAnalytics(RecentChange? result, RecentChangeAnalytics target)
        {
            if (result is null) { return; }

            if (result.Length?.Old != null && result.Length.New != null)
            {
                target.NetLength += (int)(result.Length.New - result.Length.Old);
            }

            if (result.Type is not null)
            {
                if (!target.TypeCounts.ContainsKey(result.Type))
                {
                    target.TypeCounts[result.Type] = 0;
                }
                target.TypeCounts[result.Type]++;
            }

            if (result.Bot != null)
            {
                if (result.Bot.Value)
                {
                    target.Bots++;
                }
                else
                {
                    target.NonBots++;
                }
            }
        }

        // On startup, we need to join the consumer group first. Thus there will be a delay, followed by a bulk update as we consume all the messages in the topic.
        // After that, we will be consuming messages in real time, and sending updates to clients as we receive them.
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            using var consumer = new ConsumerBuilder<string, string>(_consumerConfig)
                .SetKeyDeserializer(Deserializers.Utf8)
                .SetValueDeserializer(Deserializers.Utf8)
                .Build();

            consumer.Subscribe("RecentChanges");
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(500));
                    if (consumeResult?.Message?.Value is null)
                    {
                        Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [INFO] No messages in queue or timeout");
                        continue;
                    }
                    var result = JsonSerializer.Deserialize<RecentChange>(consumeResult.Message.Value, options);
                    UpdateAnalytics(result, data);

                    //broadcast
                    try
                    {
                        await _hubContext.Clients.All.SendAsync("ReceiveUpdate", data, stoppingToken);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [ERROR] Unable to deliver message to clients. Reason: {e.Message}");
                    }
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
