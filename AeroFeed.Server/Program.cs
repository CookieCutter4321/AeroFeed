using AeroFeed.Server.Hubs;
using AeroFeed.Server.Workers;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);


//Redis config

var redisConfig = new ConfigurationOptions
{
    EndPoints = { builder.Configuration["REDIS_ENDPOINT"] },
    User = "default",
    Password = builder.Configuration["REDIS_TOKEN"],
    Ssl = true,
    AbortOnConnectFail = false,
};

builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConfig));
Console.WriteLine("Connected to Redis");

// Add services to the container.
builder.Services.AddHostedService<Producer>();
builder.Services.AddHostedService<Consumer>();


builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

var app = builder.Build();

app.UseAuthorization();
app.MapControllers();
//

app.MapHub<NotificationHub>("/notificationHub");

app.UseDefaultFiles();
app.MapStaticAssets();
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
{
    app.UseHttpsRedirection();
}

app.MapFallbackToFile("/index.html");
app.Run();

/*
 * 
 * TODO: We can use a bloom filter as the data does not need to be 100% accurate when considering duplication
 * It's possible that, due to some unfortunate timing, we may end up replaying some messages when the service restarts on Container Apps.
 * On the other hand, if we simply increment the count on redis WITH bloom bilters, the 1% false positives will eventually compound.
 * My hope is to eliminate this by bucketing the data on Redis and ONLY writing to the most recent time bucket with a defined TTL.
 * Any bad data will be removed after the TTL expires.
 * 
 * 100 messages / sec * 60 secs / min * 60 min / hr * 24 hr / day * 30 days / month = 259,200,000 messages per month (assuming worst case of 100 m/s)
 * So the plan is something like:
 * 1. Deduplication - batch 1,000 UUIDS . 259,200 BF.MADD calls
 * 2. Statistics - calculate the stats ourselves and then send the aggregate to Redis every minute. 43,200 calls 
 * 3. SignalR - send the update to the client every time we consume a message from Kafka. 0 calls
 * 4. Startup - we can load the most recent stats from Redis on startup. negligible calls
 * 5. Total - 302,400 calls per month (assuming worst case of 100 m/s)
 * 
 * TODO: For Line charts we will need to push the entire graph up (e.g. for timestamps).
 *  1. If user is new, THEN send the entire batch of data. Maybe timestamps only use 8 bytes, and suppose we are interesed in 5 fields of ints worth of data 
 *  (so 40 bytes) = 48 bytes total. If we wanted to slice it per minute for a max of 3 days then 60 min / hr * 24 hr / day * 3 = 4320 slices. 
 *  4320 * 48 bytes ~= 0.20 worth of MB. (OnConnectedAsync maybe?)
 *  2. if the user is OLD, then send only the most recent timestamp data (assume user already has the historical loaded already). 
 *  
 *  TODO: refactor
 *  
 *  TODO: add logging and metrics (e.g. Application Insights)
 *  
 *  TODO: Set up local instances of kafka and redis for development and testings
 */