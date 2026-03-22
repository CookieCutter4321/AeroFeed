using System.Text.Json;
using System.Text.Json.Serialization;
using AeroFeed.Server.Models;
using LaunchDarkly.EventSource;

namespace AeroFeed.Server.Workers
{
    public class SSEWorker : BackgroundService
    {

        public static readonly JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            HttpClient client = new();
            client.DefaultRequestHeaders.Add("User-Agent", "MyCsharpApp/1.0");

            using var stream = await client.GetStreamAsync("https://stream.wikimedia.org/v2/stream/recentchange", stoppingToken);
            using var reader = new StreamReader(stream);

            while (!stoppingToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync();

                if (!string.IsNullOrEmpty(line) && line.StartsWith("data: "))
                {
                    RecentChange? entry = JsonSerializer.Deserialize<RecentChange>(line[6..], options);
                    if (entry is null) continue;
                    Console.WriteLine($"Successfully deserialized: {entry.Meta.Id}");
                }
            }


        }
    }
}
