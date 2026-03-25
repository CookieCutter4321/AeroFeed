using AeroFeed.Server.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHostedService<Producer>();
builder.Services.AddHostedService<Consumer>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

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

app.UseAuthorization();

app.MapControllers();

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
 * TODO: The kafka (and probably redis eventually) certs are currently hardcoded, meaning it won't work on docker. We should do the following:
 * 1. Running Docker locally - copy the certs (based off of KAFKA_CERT_LOCATION) to the output folder and use those.
 * 2. Running Docker in production - use secrets and mount them to the container, then use those paths in the config.
 * 
 * TODO: We want to keep below the 500k monthly command limit for redis. We should aim to:
 * 1. batch updates in groups (perhaps every 100 messages or every 5 minutes, whichever comes first). We could also perform these calculations ourselves before sending.
 * 2. When we consume from the kafka topic we should send the update to the client via SignalR. 
 * We should NOT poll redis for updates, but instead rely on the fact that the client will receive the update via SignalR and then update their local state accordingly. 
 * This will reduce the number of reads we perform on Redis.
 * tips: Maybe ConcurrentStack or Channel<T>
 * 
 * 100 messages / sec * 60 secs / min * 60 min / hr * 24 hr / day * 30 days / month = 259,200,000 messages per month (assuming worst case of 100 m/s)
 * So the plan is something like:
 * 1. Deduplication - batch 1,000 UUIDS . 259,200 BF.MADD calls
 * 2. Statistics - calculate the stats ourselves and then send the aggregate to Redis every minute. 43,200 calls 
 * 3. SignalR - send the update to the client every time we consume a message from Kafka. 0 calls
 * 4. Startup - we can load the most recent stats from Redis on startup. negligible calls
 * 5. Total - 302,400 calls per month (assuming worst case of 100 m/s)
 */