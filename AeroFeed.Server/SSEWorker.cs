namespace AeroFeed.Server
{
    public class SSEWorker : BackgroundService
    {
        private static readonly string url = "https://stream.wikimedia.org/v2/stream/recentchange";
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                HttpClient client = new();

                client.DefaultRequestHeaders.Add("User-Agent", "DotNetSSEClient/1.0");

                using var streamReader = new StreamReader(await client.GetStreamAsync(url, stoppingToken));
                string? line;
                while ((line = await streamReader.ReadLineAsync(stoppingToken)) is not null)
                {
                    Console.WriteLine($"Received message: {line}");
                }
            }
        }
    }
}
