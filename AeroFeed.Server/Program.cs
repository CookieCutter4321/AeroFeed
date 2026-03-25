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
 */