using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SigCoreCommon.A_IN;

namespace SigCoreCommon {
    public class SigCoreClient : ISession {
        private readonly IDispatcher _dispatcher;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly object _connectionSync = new object();
        private readonly Log _log = new Log();

        private DateTime _lastReceiveActivity = DateTime.UtcNow;

        private TcpClient _client = null;
        private NetworkStream _stream = null;
        private StreamReader _reader = null;
        private StreamWriter _writer = null;

        private CancellationTokenSource _cts = null;
        private CancellationTokenSource _watchdogCts = null;
        private Task _receiveTask = null;
        private Task _watchdogTask = null;

        private long _connectionGeneration = 0;

        private uint sessionID = 0;

        public event Action<string> ConnectionLost;

        public string IPAddress { get; private set; } = "";
        public bool isCommander { get; private set; } = false;

        public bool IsConnected {
            get {
                TcpClient client;

                lock (_connectionSync) {
                    client = _client;
                }

                try {
                    if (client == null || client.Client == null)
                        return false;

                    if (!client.Client.Connected)
                        return false;

                    bool disconnected =
                        client.Client.Poll(0, SelectMode.SelectRead) &&
                        client.Client.Available == 0;

                    return !disconnected;
                } catch {
                    return false;
                }
            }
        }

        bool ISession.IsCommander {
            get { return isCommander; }
            set { isCommander = value; }
        }

        uint ISession.SessionID {
            get { return sessionID; }
            set { sessionID = value; }
        }

        public SigCoreClient(IDispatcher system) {
            _dispatcher = system;
        }

        // -------------------------------------------------------------
        // CONNECT
        //
        // Safe for repeated use. Every successful call creates a completely
        // new transport generation. Old receive/watchdog tasks cannot dispose
        // a newer connection.
        // -------------------------------------------------------------
        public async Task<bool> ConnectAsync(string ipAddress, int port = 7020) {
            Close();

            TcpClient client = null;
            NetworkStream stream = null;
            StreamReader reader = null;
            StreamWriter writer = null;
            CancellationTokenSource cts = null;
            CancellationTokenSource watchdogCts = null;

            try {
                client = new TcpClient();

                Task connectTask = client.ConnectAsync(ipAddress, port);
                if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                    throw new TimeoutException("Connection attempt timed out.");

                // Observe any exception from the completed connect task.
                await connectTask;

                stream = client.GetStream();
                reader = new StreamReader(stream, Encoding.UTF8);
                writer = new StreamWriter(stream, Encoding.UTF8) {
                    AutoFlush = true
                };

                cts = new CancellationTokenSource();
                watchdogCts = new CancellationTokenSource();

                long generation;

                lock (_connectionSync) {
                    generation = ++_connectionGeneration;

                    _client = client;
                    _stream = stream;
                    _reader = reader;
                    _writer = writer;
                    _cts = cts;
                    _watchdogCts = watchdogCts;

                    IPAddress = ipAddress;
                    _lastReceiveActivity = DateTime.UtcNow;
                }

                _receiveTask = Task.Run(
                    () => ReceiveLoopAsync(
                        generation,
                        client,
                        reader,
                        writer,
                        cts.Token));

                _watchdogTask = Task.Run(
                    () => ClientWatchdogLoop(
                        generation,
                        watchdogCts.Token));

                _log.Info(
                    "Connection",
                    "Connected",
                    $"Connected to SigCore at {ipAddress}:{port}.");

                return true;
            } catch (Exception ex) {
                _log.Warn(
                    "Connection",
                    "ConnectFailure",
                    $"Connection to SigCore failed: {ex.Message}");

                SafeDispose(
                    client,
                    stream,
                    reader,
                    writer,
                    cts,
                    watchdogCts);

                return false;
            }
        }

        // -------------------------------------------------------------
        // RECEIVE LOOP
        // -------------------------------------------------------------
        private async Task ReceiveLoopAsync(
            long generation,
            TcpClient client,
            StreamReader reader,
            StreamWriter writer,
            CancellationToken token) {

            string disconnectReason = "SigCore server closed the connection.";

            try {
                while (!token.IsCancellationRequested &&
                       IsCurrentConnection(generation, client)) {

                    string line;

                    try {
                        line = await reader.ReadLineAsync();

                        if (line == null) {
                            disconnectReason =
                                "SigCore server closed the connection.";
                            break;
                        }
                    } catch (IOException ex) {
                        disconnectReason =
                            "SigCore receive I/O error: " + ex.Message;
                        break;
                    } catch (ObjectDisposedException) {
                        break;
                    } catch (Exception ex) {
                        disconnectReason =
                            "SigCore receive error: " + ex.Message;
                        break;
                    }

                    if (line.Length == 0)
                        continue;

                    SigCoreMessage request;

                    try {
                        request = SigCoreProtocol.Decode(line);
                    } catch (Exception ex) {
                        _log.Error(
                            "Protocol",
                            "DecodeFailure",
                            "Received message could not be decoded.",
                            ex);
                        continue;
                    }

                    SigCoreMessage response = null;

                    try {
                        response = request.Dispatch(_dispatcher, this);
                    } catch (Exception ex) {
                        _log.Error(
                            "Protocol",
                            "DispatchFailure",
                            "Received command failed during dispatch.",
                            ex);
                        response =
                            SigCoreMessage.CreateError(
                                request.Command,
                                ex.Message,
                                request.MsgId);
                    }

                    if (response != null) {
                        if (response.MsgId == 0)
                            response.MsgId = request.MsgId;

                        try {
                            string encoded = SigCoreProtocol.Encode(response);

                            await _sendLock.WaitAsync(token);
                            try {
                                await writer.WriteAsync(encoded);
                                await writer.FlushAsync();
                            } finally {
                                _sendLock.Release();
                            }
                        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                            break;
                        } catch (Exception ex) {
                            disconnectReason =
                                "SigCore response send error: " + ex.Message;
                            break;
                        }
                    }

                    _lastReceiveActivity = DateTime.UtcNow;
                }
            } catch (Exception ex) {
                disconnectReason =
                    "Unexpected receive loop error: " + ex.Message;

                _log.Error(
                    "Connection",
                    "ReceiveLoopFailure",
                    disconnectReason);
            } finally {
                if (!token.IsCancellationRequested) {
                    EndCurrentConnection(
                        generation,
                        disconnectReason,
                        notifyConnectionLost: true);
                }
            }
        }

        // -------------------------------------------------------------
        // SEND
        // -------------------------------------------------------------
        public async Task SendAsync(SigCoreMessage msg) {
            StreamWriter writer;
            long generation;

            lock (_connectionSync) {
                writer = _writer;
                generation = _connectionGeneration;
            }

            if (writer == null || !IsConnected)
                throw new IOException("SigCore client is not connected.");

            string encoded = SigCoreProtocol.Encode(msg);

            await _sendLock.WaitAsync();

            try {
                lock (_connectionSync) {
                    if (generation != _connectionGeneration ||
                        !ReferenceEquals(writer, _writer)) {

                        throw new IOException(
                            "SigCore connection changed before send completed.");
                    }
                }

                await writer.WriteAsync(encoded);
                await writer.FlushAsync();
            } catch (Exception ex) {
                if (ex is not IOException)
                    ex = new IOException(
                        "SigCore send failed: " + ex.Message,
                        ex);

                EndCurrentConnection(
                    generation,
                    ex.Message,
                    notifyConnectionLost: true);

                throw;
            } finally {
                _sendLock.Release();
            }
        }

        public Task Send(SigCoreMessage message) {
            return SendAsync(message);
        }

        // -------------------------------------------------------------
        // CLOSE
        //
        // Intentional close. This must not raise ConnectionLost and therefore
        // must not cause SigCoreSystem to reconnect.
        // -------------------------------------------------------------
        public void Close() {
            TcpClient client;
            NetworkStream stream;
            StreamReader reader;
            StreamWriter writer;
            CancellationTokenSource cts;
            CancellationTokenSource watchdogCts;

            lock (_connectionSync) {
                ++_connectionGeneration;

                client = _client;
                stream = _stream;
                reader = _reader;
                writer = _writer;
                cts = _cts;
                watchdogCts = _watchdogCts;

                _client = null;
                _stream = null;
                _reader = null;
                _writer = null;
                _cts = null;
                _watchdogCts = null;
                _receiveTask = null;
                _watchdogTask = null;
            }

            SafeDispose(
                client,
                stream,
                reader,
                writer,
                cts,
                watchdogCts);

        }

        // -------------------------------------------------------------
        // CURRENT CONNECTION FAILURE
        // -------------------------------------------------------------
        private void EndCurrentConnection(
            long generation,
            string reason,
            bool notifyConnectionLost) {

            TcpClient client;
            NetworkStream stream;
            StreamReader reader;
            StreamWriter writer;
            CancellationTokenSource cts;
            CancellationTokenSource watchdogCts;

            lock (_connectionSync) {
                if (generation != _connectionGeneration)
                    return;

                ++_connectionGeneration;

                client = _client;
                stream = _stream;
                reader = _reader;
                writer = _writer;
                cts = _cts;
                watchdogCts = _watchdogCts;

                _client = null;
                _stream = null;
                _reader = null;
                _writer = null;
                _cts = null;
                _watchdogCts = null;
                _receiveTask = null;
                _watchdogTask = null;
            }

            SafeDispose(
                client,
                stream,
                reader,
                writer,
                cts,
                watchdogCts);


            if (notifyConnectionLost)
                ConnectionLost?.Invoke(reason);
        }

        private bool IsCurrentConnection(
            long generation,
            TcpClient client) {

            lock (_connectionSync) {
                return generation == _connectionGeneration &&
                       ReferenceEquals(client, _client);
            }
        }

        // -------------------------------------------------------------
        // INTERNAL CLEANUP
        // -------------------------------------------------------------
        private static void SafeDispose(
            TcpClient client,
            NetworkStream stream,
            StreamReader reader,
            StreamWriter writer,
            CancellationTokenSource cts,
            CancellationTokenSource watchdogCts) {

            try { cts?.Cancel(); } catch { }
            try { watchdogCts?.Cancel(); } catch { }

            try { reader?.Close(); } catch { }
            try { writer?.Close(); } catch { }
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }

            try { cts?.Dispose(); } catch { }
            try { watchdogCts?.Dispose(); } catch { }
        }

        public Task SendOrDiscard(SigCoreMessage msg) {
            return Send(msg);
        }

        // -------------------------------------------------------------
        // WATCHDOG
        //
        // One Ping/Pong exchange at a time. A quiet subscription is normal,
        // so actively test connectivity rather than inferring it from silence.
        // Do not mistake a delayed Pong for a dead connection when other valid
        // messages were received while the Ping was outstanding.
        // -------------------------------------------------------------
        private async Task ClientWatchdogLoop(
            long generation,
            CancellationToken token) {

            const int pingIntervalMs = 1000;
            const int pingTimeoutMs = 3000;

            try {
                while (!token.IsCancellationRequested) {
                    await Task.Delay(pingIntervalMs, token);

                    if (!IsCurrentGeneration(generation))
                        return;

                    if (_dispatcher is not SigCoreSystem system)
                        continue;

                    // Capture receive activity before starting the exchange.
                    // A valid inbound message received during the exchange
                    // means the transport has not gone silent.
                    DateTime lastReceiveBeforePing = _lastReceiveActivity;
                    bool healthy;

                    try {
                        // Await completion before beginning another Ping.
                        healthy = await system.PingAsync(pingTimeoutMs);
                    } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                        return;
                    } catch {
                        if (!IsCurrentGeneration(generation))
                            return;

                        healthy = false;
                    }

                    if (healthy)
                        continue;

                    if (!IsCurrentGeneration(generation))
                        return;

                    if (_lastReceiveActivity > lastReceiveBeforePing)
                        continue;

                    const string heartbeatFailure =
                        "SigCore heartbeat timed out with no inbound protocol traffic.";

                    _log.Warn(
                        "Connection",
                        "HeartbeatFailed",
                        heartbeatFailure);

                    EndCurrentConnection(
                        generation,
                        heartbeatFailure,
                        notifyConnectionLost: true);

                    return;
                }
            } catch (OperationCanceledException) {
            }
        }

        private bool IsCurrentGeneration(long generation) {
            lock (_connectionSync) {
                return generation == _connectionGeneration;
            }
        }
    }
}
