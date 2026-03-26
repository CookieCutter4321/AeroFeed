const { env } = require('process');

const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
  env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://localhost:7025';


const PROXY_CONFIG = [
  {
    context: [
      "/api",
      "/notificationHub"
    ],
    target: "https://localhost:7025",
    secure: false, // This ignores the "Self-Signed Certificate" error
    ws: true,      // Essential for SignalR
    changeOrigin: true, // This makes the backend think the request came from 7025
    logLevel: "debug"
  }
]

module.exports = PROXY_CONFIG;
