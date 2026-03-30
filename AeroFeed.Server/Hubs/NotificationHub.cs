using AeroFeed.Server.Models;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using System.Text.Json;

namespace AeroFeed.Server.Hubs
{
    public class NotificationHub : Hub
    {
        public static readonly JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        private readonly IDatabase _db;
        public NotificationHub(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public override async Task OnConnectedAsync()
        {
            // 1. Get the latest snapshot from Redis
            RedisValue cachedJson = await _db.StringGetAsync("recent_changes:latest");
            if (cachedJson.HasValue)
            {
                RecentChangeAnalytics data = JsonSerializer.Deserialize<RecentChangeAnalytics>(cachedJson.ToString(), options)!;
                await Clients.Caller.SendAsync("ReceiveUpdate", data);
            }

            await base.OnConnectedAsync();
        }
    }
}
