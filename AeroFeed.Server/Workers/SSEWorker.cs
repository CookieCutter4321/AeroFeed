using System.Linq.Expressions;
using System.Text.Json;
using AeroFeed.Server.Models;

namespace AeroFeed.Server.Workers
{
    public class SSEWorker : BackgroundService
    {
        public static readonly JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        public static HttpClient client = new()
        {
            DefaultRequestHeaders =
            {
                { "User-Agent", "AeroFeed/1.0" }
            }
        };

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var stream = await client.GetStreamAsync("https://stream.wikimedia.org/v2/stream/recentchange", stoppingToken);
                    using var reader = new StreamReader(stream);

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        string? line = await reader.ReadLineAsync(stoppingToken);

                        if (!string.IsNullOrEmpty(line) && line.StartsWith("data: "))
                        {
                            RecentChange? entry = JsonSerializer.Deserialize<RecentChange>(line[6..], options);
                            if (entry is null) continue;
                            Console.WriteLine($"Successfully deserialized: {entry.Meta.Id}");
                        }
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
