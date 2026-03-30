# AeroFeed | Real-Time Wikipedia Analytics Engine

A high-throughput, event-driven pipeline designed to ingest, process, and broadcast live Wikipedia edits

Technologies used:

1.Fullstack: Angular + C#/.NET. Has a real-time analytics dashboard using SignalR WebSockets for sub-second data broadcasts to the client.

2.Caching: Redis. Maintains a source of truth and serves as data persistence. If the app scales, we use it as a single source of truth.

3.Distributed Event Streaming Platform: Kafka. Serves as a buffer for spikes in data and is fault tolerant

4.Containerization: Docker. Lets us deploy to the cloud without worrying if it will compile

To set this up, make sure you:
1. Make: appsettings.Development.json -- REDIS_TOKEN field if using cloud. I personally use Upstash as it is free and has a 500k monthly command limit, which is pretty generous!
2. Make: appsettings.Production.json -- Useful if you want to run your own docker instance. In addition to REDIS_TOKEN, if using cloud (like Aiven), move your tokens into the repo under a folder kafka-certs
