using AeroFeed.Server.Hubs;
using AeroFeed.Server.Models;
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AeroFeed.Server.Workers
{
    public class RollingAverageCounter
    {
        private readonly ConcurrentQueue<DateTime> _ticks = new();
        private readonly int _windowSeconds;
        public RollingAverageCounter(int windowSeconds = 5) => _windowSeconds = windowSeconds;

        public void Increment() => _ticks.Enqueue(DateTime.UtcNow);

        public float GetAverage()
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-_windowSeconds);

            while (_ticks.TryPeek(out var result) && result < cutoff)
            {
                _ticks.TryDequeue(out _);
            }

            return (float)Math.Round((float)_ticks.Count / _windowSeconds, 1);
        }
    }

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
        private readonly ConfigurationOptions _redisConfig;
        private RollingAverageCounter _counter;
        private IConnectionMultiplexer _redis;
        public Consumer(IConfiguration config, IHubContext<NotificationHub> hubContext, IConnectionMultiplexer connectionMultiplexer)
        {
            _config = config;
            _hubContext = hubContext;
            _redis = connectionMultiplexer;
            _counter = new();
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

            _redisConfig = new ConfigurationOptions
            {
                EndPoints = { "relaxing-marmot-87976.upstash.io:6379" },
                User = "default",
                Password = _config["REDIS_TOKEN"],
                Ssl = true,
                AbortOnConnectFail = false,
            };
        }

        /*
         * Will not work with multiple consumers (such as if we are utilizing partitioning) since we are just keeping a single global state here.
         * For a prod system Redis will be the single source of truth.
        */
        RecentChangeAnalytics data;

        private void UpdateAnalytics(RecentChange? result, RecentChangeAnalytics target)
        {
            if (result is null) { return; }
            
            _counter.Increment();
            target.Average = _counter.GetAverage();

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

        private void SaveToRedis(IConnectionMultiplexer redis)
        {
            PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            _ = Task.Run(async () =>
            {
                while (await timer.WaitForNextTickAsync())
                {
                    try
                    {
                        IDatabase db = redis.GetDatabase();
                        db.StringSet("recent_changes:latest", JsonSerializer.Serialize(data, options));
                        Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [INFO] Saving to redis");
                    } catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to write to redis: {ex.Message}");
                    }
                }
            });

        }

        private void LoadFromRedis(IConnectionMultiplexer redis)
        {
            try
            {
                IDatabase db = redis.GetDatabase();
                string? res = db.StringGet("recent_changes:latest");

                if (res == null)
                {
                    Console.WriteLine("No existing data in redis, starting fresh");
                    return;
                }
                Console.WriteLine("Existing data found in redis, loading..");
                data = JsonSerializer.Deserialize<RecentChangeAnalytics>(res, options)!;
                BroadcastToClients();
            } catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to redis: {ex.Message}");
            }
        }
        
        private async void BroadcastToClients()
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", data);
            }
            catch (Exception e)
            {
                Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [ERROR] Unable to deliver message to clients. Reason: {e.Message}");
            }
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            LoadFromRedis(_redis);
            SaveToRedis(_redis);

            // Subscribe to Kafka topic
            bool joinedGroup = false;
            using var consumer = new ConsumerBuilder<string, string>(_consumerConfig)
                .SetKeyDeserializer(Deserializers.Utf8)
                .SetValueDeserializer(Deserializers.Utf8).SetPartitionsAssignedHandler((c, partitions) =>
                {
                    Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [INFO] Partitions assigned");
                    joinedGroup = true;
                })
                .Build();

            consumer.Subscribe("RecentChanges");
            try
            {
                Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [INFO] Consumer started and subscribed to topic. Waiting for messages and partition assignment..");
                while (!stoppingToken.IsCancellationRequested)
                {
                    var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(1000));
                    if (consumeResult?.Message?.Value is null)
                    {
                        if (!joinedGroup) continue; // Don't log timeouts until we've joined the group, since that's expected behavior
                        Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [INFO] No messages in queue or timeout");
                        continue;
                    }

                    try
                    {
                        RecentChange result = JsonSerializer.Deserialize<RecentChange>(consumeResult.Message.Value, options)!;
                        UpdateAnalytics(result, data);
                    } catch { }
                    //broadcast
                    BroadcastToClients();
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                IDatabase db = _redis.GetDatabase();
                db.StringSet("recent_changes:latest", JsonSerializer.Serialize(data, options));
                Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [INFO] Saving to redis on shutdown..");
                consumer.Close();
            }
        }
    }
}
