
using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreServer {
    public class ClientSession : ISession {
        private static readonly Log _log = new Log();
        // ---- Static list of active sessions ----
        private static readonly List<ClientSession> _sessions = new List<ClientSession>();
        private static readonly object _sessionsLock = new object();

        private readonly object _sendLock = new object();
        private readonly IDispatcher _dispatcher;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private int _closed;

        // Diagnostic state is deliberately independent of the protocol. No request,
        // reply, retry, timeout, or socket configuration is changed by this patch.
        private readonly string _remoteEndpoint;
        private readonly Timer _diagnosticTimer;
        private readonly long _connectedTicks = Stopwatch.GetTimestamp();
        private long _lastInboundTicks;
        private long _lastOutboundTicks;
        private long _requestStartedTicks;
        private string _activeRequest = "none";
        private string _lastRequest = "none";
        private string _lastSend = "none";
        private long _oldestSendAttemptTicks;
        private int _pendingSends;
        private int _writingToSocket;
        private int _subscribed;
        private int _idleReported;
        private int _slowRequestReported;
        private int _slowSendReported;
        private long _receivedCount;
        private long _sentCount;
        private string _closeReason = "unspecified";

        private static long ElapsedMilliseconds(long startTicks) =>
            startTicks == 0 ? -1 : (long)Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;

        // Runs on a timer thread, so a blocked TCP write or a stuck synchronous
        // dispatch can still be observed. Emits warnings only on threshold crossings.
        private void ObserveConnection(object? state) {
            // A diagnostic timer must never terminate Core on an exception.
            try {
                if (Volatile.Read(ref _closed) != 0)
                    return;

                long requestStarted = Volatile.Read(ref _requestStartedTicks);
                long sendStarted = Volatile.Read(ref _oldestSendAttemptTicks);
                long inboundAge = ElapsedMilliseconds(Volatile.Read(ref _lastInboundTicks));
                long outboundAge = ElapsedMilliseconds(Volatile.Read(ref _lastOutboundTicks));

                if (requestStarted != 0 && ElapsedMilliseconds(requestStarted) >= 5_000 &&
                    Interlocked.Exchange(ref _slowRequestReported, 1) == 0) {
                    _log.Warn("ConnectionDiagnostics", "DispatchStalled",
                        $"Session {SessionID} peer={_remoteEndpoint} request={Volatile.Read(ref _activeRequest)} " +
                        $"elapsedMs={ElapsedMilliseconds(requestStarted)} pendingSends={Volatile.Read(ref _pendingSends)}.");
                }

                if (sendStarted != 0 && ElapsedMilliseconds(sendStarted) >= 5_000 &&
                    Interlocked.Exchange(ref _slowSendReported, 1) == 0) {
                    _log.Warn("ConnectionDiagnostics", "SendStalled",
                        $"Session {SessionID} peer={_remoteEndpoint} elapsedMs={ElapsedMilliseconds(sendStarted)} " +
                        $"socketWriteActive={Volatile.Read(ref _writingToSocket) != 0} " +
                        $"pendingSends={Volatile.Read(ref _pendingSends)} lastSent={Volatile.Read(ref _lastSend)}.");
                }

                // Only warn on two-way silence: if notifications are flowing out,
                // the client may legitimately have no reason to send a heartbeat.
                // This is a symptom, not proof of a fault.
                if (Volatile.Read(ref _subscribed) != 0 && inboundAge >= 30_000 &&
                    outboundAge >= 30_000 &&
                    Interlocked.Exchange(ref _idleReported, 1) == 0) {
                    _log.Warn("ConnectionDiagnostics", "InboundIdle",
                        $"Session {SessionID} peer={_remoteEndpoint} inboundIdleMs={inboundAge} " +
                        $"outboundIdleMs={outboundAge} lastRequest={Volatile.Read(ref _lastRequest)} " +
                        $"pendingSends={Volatile.Read(ref _pendingSends)} " +
                        $"activeRequest={Volatile.Read(ref _activeRequest)}.");
                }
            } catch {
                // Diagnostic logging is best-effort and must not affect Core.
            }
        }

        public void MarkSubscribed() => Interlocked.Exchange(ref _subscribed, 1);
        public void MarkUnsubscribed() => Interlocked.Exchange(ref _subscribed, 0);

        public TcpClient Client { get; }
        public NetworkStream Stream { get; }
        public uint SessionID { get; set; }
        public bool IsCommander { get; set; }   // client can set this via messages

        public bool IsConnected {
            get {
                try {
                    if (Client == null || Client.Client == null)
                        return false;

                    // Check if the socket has been closed, reset, or disconnected
                    if (!Client.Client.Connected)
                        return false;

                    // Poll(SelectMode.SelectRead) returns true if:
                    //   - data is available to read, or
                    //   - the connection has been closed.
                    // Peek one byte to confirm.
                    bool disconnected = Client.Client.Poll(0, SelectMode.SelectRead) &&
                                        (Client.Client.Available == 0);
                    return !disconnected;
                } catch {
                    return false;
                }
            }
        }

        public ClientSession(TcpClient client, uint sessionId, IDispatcher dispatcher) {
            Client = client;
            Stream = client.GetStream();
            SessionID = sessionId;
            _dispatcher = dispatcher;
            _remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            _lastInboundTicks = _connectedTicks;
            _lastOutboundTicks = _connectedTicks;

            SetCommander();

            _reader = new StreamReader(Stream, Encoding.UTF8);
            _writer = new StreamWriter(Stream, Encoding.UTF8) { AutoFlush = true };

            _diagnosticTimer = new Timer(ObserveConnection, null, 5_000, 5_000);
            lock (_sessionsLock) {
                _sessions.Add(this);
            }
        }

        public void SetCommander() {
            lock (_sessionsLock) {
                IsCommander = !_sessions.Exists(s => s.IsCommander);
            }
        }

        public async Task RunAsync() {
            try {
                SigCoreMessage msg = SigCoreMessage.CreateConnect(SessionID, IsCommander);
                Send(msg);

                while (true) {
                    string? line = await _reader.ReadLineAsync();
                    if (line == null) {
                        _closeReason = "peer-eof";
                        break;
                    }
                    Interlocked.Exchange(ref _lastInboundTicks, Stopwatch.GetTimestamp());
                    Interlocked.Exchange(ref _idleReported, 0);
                    Interlocked.Increment(ref _receivedCount);

                    // Record the exact point at which processing starts. The
                    // diagnostic timer can see a dispatch that never returns.
                    long started = Stopwatch.GetTimestamp();
                    Interlocked.Exchange(ref _requestStartedTicks, started);
                    Volatile.Write(ref _activeRequest, "decoding");
                    try {
                        SigCoreMessage request = SigCoreProtocol.Decode(line);
                        string description = $"{request.Command}/msgId={request.MsgId}";
                        Volatile.Write(ref _activeRequest, description);
                        Volatile.Write(ref _lastRequest, description);

                        SigCoreMessage response;
                        try {
                            response = request.Dispatch(_dispatcher, this);
                        } catch (Exception ex) {
                            _log.Error("Protocol", "DispatchFailed",
                                $"Session {SessionID} peer={_remoteEndpoint} command={description} failed.", ex);
                            response = SigCoreMessage.CreateError(request.Command, ex.Message, request.MsgId);
                        }

                        long dispatchMs = ElapsedMilliseconds(started);
                        if (dispatchMs >= 500) {
                            _log.Warn("ConnectionDiagnostics", "DispatchSlow",
                                $"Session {SessionID} peer={_remoteEndpoint} command={description} " +
                                $"dispatchMs={dispatchMs} response={(response == null ? "none" : response.Command.ToString())}.");
                        }

                        if (response != null) {
                            if (response.MsgId == 0) response.MsgId = request.MsgId;
                            Send(response);
                        }
                    } finally {
                        Interlocked.Exchange(ref _requestStartedTicks, 0);
                        Volatile.Write(ref _activeRequest, "none");
                        Interlocked.Exchange(ref _slowRequestReported, 0);
                    }
                }
            } catch (IOException ex) {
                _closeReason = "read-io-exception";
                if (Volatile.Read(ref _subscribed) != 0)
                    _log.Info("ConnectionDiagnostics", "ReadEnded",
                        $"Session {SessionID} peer={_remoteEndpoint} read error: {ex.GetType().Name}: {ex.Message}.");
            } catch (ObjectDisposedException) {
                _closeReason = "socket-disposed";
                // A short-lived client/probe closing its socket is not a session failure.
            } catch (Exception ex) {
                _closeReason = "unexpected-exception";
                _log.Error("Session", "SessionFailed", $"Unexpected failure in session {SessionID} peer={_remoteEndpoint}.", ex);
            } finally {
                Close();
            }
        }

        public Task Send(SigCoreMessage msg) {
            // Count attempts before waiting for the existing synchronous send lock.
            // The timer distinguishes waiting for the lock from a blocked socket write.
            long started = Stopwatch.GetTimestamp();
            if (Interlocked.Increment(ref _pendingSends) == 1)
                Interlocked.Exchange(ref _oldestSendAttemptTicks, started);
            try {
                string encoded = SigCoreProtocol.Encode(msg);
                lock (_sendLock) {
                    Interlocked.Exchange(ref _writingToSocket, 1);
                    try {
                        _writer.WriteLine(encoded);
                        _writer.Flush();
                        Interlocked.Increment(ref _sentCount);
                        Interlocked.Exchange(ref _lastOutboundTicks, Stopwatch.GetTimestamp());
                        string description = $"{msg.Command}/msgId={msg.MsgId}";
                        Volatile.Write(ref _lastSend, description);

                    } finally {
                        Interlocked.Exchange(ref _writingToSocket, 0);
                    }
                }
                long sendMs = ElapsedMilliseconds(started);
                // Never invoke the logger while holding the socket send lock.
                if (sendMs >= 500)
                    _log.Warn("ConnectionDiagnostics", "SendSlow",
                        $"Session {SessionID} peer={_remoteEndpoint} command={msg.Command}/msgId={msg.MsgId} sendMs={sendMs}.");
            } catch (IOException ex) {
                _closeReason = "write-io-exception";
                _log.Warn("ConnectionDiagnostics", "SendIOFailure",
                    $"Session {SessionID} peer={_remoteEndpoint} command={msg.Command}/msgId={msg.MsgId}: {ex.Message}.");
                Close();
            } catch (ObjectDisposedException) {
                _closeReason = "write-disposed";
                Close();
            } catch (Exception ex) {
                _log.Error("Session", "SendFailed", $"Unexpected send failure for session {SessionID} peer={_remoteEndpoint}.", ex);
            } finally {
                if (Interlocked.Decrement(ref _pendingSends) == 0) {
                    Interlocked.Exchange(ref _oldestSendAttemptTicks, 0);
                    Interlocked.Exchange(ref _slowSendReported, 0);
                }
            }

            return Task.CompletedTask;
        }

        public void Close() {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
                return;

            _diagnosticTimer.Dispose();
            if (_dispatcher is CommandDispatcher dispatcher)
                dispatcher.RemoveSubscriber(this);

            lock (_sessionsLock) {
                _sessions.Remove(this);
            }

            _log.Info("ConnectionDiagnostics", "SessionSummary",
                $"Session {SessionID} peer={_remoteEndpoint} pid={Environment.ProcessId} " +
                $"durationMs={ElapsedMilliseconds(_connectedTicks)} reason={_closeReason} " +
                $"received={Interlocked.Read(ref _receivedCount)} sent={Interlocked.Read(ref _sentCount)} " +
                $"lastInboundAgeMs={ElapsedMilliseconds(Volatile.Read(ref _lastInboundTicks))} " +
                $"lastOutboundAgeMs={ElapsedMilliseconds(Volatile.Read(ref _lastOutboundTicks))} " +
                $"lastRequest={Volatile.Read(ref _lastRequest)} lastSent={Volatile.Read(ref _lastSend)} " +
                $"activeRequest={Volatile.Read(ref _activeRequest)} pendingSends={Volatile.Read(ref _pendingSends)}.");
            _log.Info("Session", "Disconnected", $"Session {SessionID} disconnected.");

            try {
                Client.Close();
            } catch {
                // Session cleanup must not fail because the socket is already closed.
            }
        }

        public static async Task SendToAsync(uint sessionId, SigCoreMessage msg) {
            ClientSession? target = null;
            lock (_sessionsLock) {
                target = _sessions.Find(s => s.SessionID == sessionId);
            }

            if (target != null)
                target.Send(msg);
        }

        public async Task SendOrDiscard(SigCoreMessage msg) {
            SendToAsync(SessionID, msg);
        }

    }
}
