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
        private static readonly Log _log = new Log();

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
            Log log = new Log();

            try {
                _listener = new TcpListener(IPAddress.Any, 7020);
                _listener.Start();

                if (!_listener.Server.IsBound) {
                    throw new InvalidOperationException("Listener not bound after Start()");
                }

            } catch (SocketException ex) {
                log.Error("Network", "ListenerStartFailed", $"Unable to start TCP listener on port 7020. SocketError={ex.SocketErrorCode}", ex);
                throw;
            } catch (Exception ex) {
                log.Error("Network", "ListenerStartFailed", "Unexpected error starting TCP listener on port 7020.", ex);
                throw;
            }

            while (true) {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                uint sessionId = _nextSessionId++;

                ClientSession session = new ClientSession(client, sessionId, _dispatcher);

                _log.Info(
                    "Session",
                    "Connected",
                    $"Session {sessionId} connected from {client.Client.RemoteEndPoint}.");

                // Add to active sessions
                _sessions[sessionId] = session;

                _ = Task.Run(async () => {
                    try {
                        await session.RunAsync();
                    } finally {
                        // Remove on disconnect
                        _sessions.TryRemove(sessionId, out _);
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
