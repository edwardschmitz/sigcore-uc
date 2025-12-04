using SigCoreCommon;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SigCoreServer {
    public class SigCoreServer {
        private readonly IDispatcher _dispatcher;
        private TcpListener _listener;
        private uint _nextSessionId = 1;

        // 🧠 Track active sessions
        private readonly ConcurrentDictionary<uint, ClientSession> _sessions =
            new ConcurrentDictionary<uint, ClientSession>();

        public SigCoreServer() {
            HardwareManager hwManager = new HardwareManager();

            OsManager osManager = new OsManager();

            _dispatcher = new CommandDispatcher(hwManager, osManager);

            hwManager.Dispatcher = _dispatcher;
            hwManager.Initialize();
        }

        public async Task StartAsync() {
            _listener = new TcpListener(IPAddress.Any, 7020);
            _listener.Start();
            Console.WriteLine("SigCore Server started on port 7020");

            while (true) {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                uint sessionId = _nextSessionId++;
                Console.WriteLine($"--- Client Connected: Session {sessionId} ---");

                ClientSession session = new ClientSession(client, sessionId, _dispatcher);

                // Add to active sessions
                _sessions[sessionId] = session;

                _ = Task.Run(async () => {
                    try {
                        await session.RunAsync();
                    } finally {
                        // Remove on disconnect
                        _sessions.TryRemove(sessionId, out _);
                        Console.WriteLine($"Session {sessionId} closed.");
                    }
                });
            }
        }

        // Optional helper: close all
        public void CloseAll() {
            foreach (var s in _sessions.Values) {
                s.Close();
            }
            _sessions.Clear();
        }
    }
}
