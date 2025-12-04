// WebserverBridge/Program.cs

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

Console.WriteLine("[Bridge] Starting Webserver Bridge...");

// Track browser WebSocket clients
ConcurrentBag<WebSocket> webClients = new ConcurrentBag<WebSocket>();

// --- 1. Connect to SigCore Server (port 7020) ---
TcpClient sigCoreClient = new TcpClient();
await sigCoreClient.ConnectAsync("localhost", 7020);
Console.WriteLine("[Bridge] Connected to SigCore Server on port 7020");

NetworkStream sigCoreStream = sigCoreClient.GetStream();
StreamReader sigCoreReader = new StreamReader(sigCoreStream);
StreamWriter sigCoreWriter = new StreamWriter(sigCoreStream) { AutoFlush = true };

// --- Send Subscribe command to SigCore Server ---
string subscribeMsg = "{\"MsgId\":1,\"Command\":37,\"Payload\":{}}";
await sigCoreWriter.WriteLineAsync(subscribeMsg);
Console.WriteLine("[Bridge] Sent Subscribe command (37) to SigCore Server");

// --- 2. Start WebSocket + HTTP Server (port 8080) ---
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Explicitly set the web root path to /var/www/html
builder.Environment.WebRootPath = "/var/www/html";

WebApplication app = builder.Build();

app.UseWebSockets();
app.UseStaticFiles();   // Serves files from /var/www/html

// Serve /status.json
app.MapGet("/status.json", async context => {
    JObject status = new JObject {
        ["systemName"] = "SigCore UC",
        ["serialNumber"] = "SCU123456",
        ["version"] = "1.0.0"
    };

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(status.ToString(Formatting.None));
});

// WebSocket endpoint for live status updates
app.Map("/ws/status", async context => {
    if (!context.WebSockets.IsWebSocketRequest) {
        context.Response.StatusCode = 400;
        return;
    }

    WebSocket ws = await context.WebSockets.AcceptWebSocketAsync();
    webClients.Add(ws);
    Console.WriteLine("[Bridge] Web client connected");

    byte[] buffer = new byte[4096];
    while (ws.State == WebSocketState.Open) {
        WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close)
            break;

        string json = Encoding.UTF8.GetString(buffer, 0, result.Count).Trim();
        Console.WriteLine("[Browser → SigCore] " + json);
        await sigCoreWriter.WriteLineAsync(json);
    }

    Console.WriteLine("[Bridge] Web client disconnected");
});

// Relay SigCore → all WebSocket clients
_ = Task.Run(async () => {
    while (true) {
        string? line = await sigCoreReader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(line)) continue;

        Console.WriteLine("[SigCore → Browser] " + line);
        byte[] msg = Encoding.UTF8.GetBytes(line);

        foreach (WebSocket ws in webClients.ToArray()) {
            if (ws.State == WebSocketState.Open) {
                try {
                    await ws.SendAsync(msg, WebSocketMessageType.Text, true, CancellationToken.None);
                } catch {
                    // Ignore send errors
                }
            }
        }
    }
});

// Start server on 0.0.0.0:8080
app.Run("http://0.0.0.0:8080");
