
using SigCoreCommon;
using System;
using System.Collections.Generic;
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

            SetCommander();

            _reader = new StreamReader(Stream, Encoding.UTF8);
            _writer = new StreamWriter(Stream, Encoding.UTF8) { AutoFlush = true };

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
                    if (line == null) break;
                    SigCoreMessage request = SigCoreProtocol.Decode(line);

                    SigCoreMessage response;
                    try {
                        response = request.Dispatch(_dispatcher, this);
                    } catch (Exception ex) {
                        _log.Error("Protocol", "DispatchFailed", $"Command {request.Command} failed for session {SessionID}.", ex);
                        response = SigCoreMessage.CreateError(request.Command, ex.Message, request.MsgId);
                    }

                    if (response != null) {
                        if (response.MsgId == 0) response.MsgId = request.MsgId;
                        Send(response);
                    }
                }
            } catch (IOException) {
                // Normal client disconnects can surface as I/O exceptions.
            } catch (ObjectDisposedException) {
                // A short-lived client or probe can close while ReadLineAsync is still pending.
                // That is a normal disconnect, not a session failure.
            } catch (Exception ex) {
                _log.Error("Session", "SessionFailed", $"Unexpected failure in session {SessionID}.", ex);
            } finally {
                Close();
            }
        }

        public Task Send(SigCoreMessage msg) {
            try {
                string encoded = SigCoreProtocol.Encode(msg);

                lock (_sendLock) {
                    _writer.WriteLine(encoded);
                    _writer.Flush();
                }
            } catch (IOException) {
                Close();
            } catch (ObjectDisposedException) {
                Close();
            } catch (Exception ex) {
                _log.Error("Session", "SendFailed", $"Unexpected send failure for session {SessionID}.", ex);
            }

            return Task.CompletedTask;
        }

        public void Close() {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
                return;

            if (_dispatcher is CommandDispatcher dispatcher)
                dispatcher.RemoveSubscriber(this);

            lock (_sessionsLock) {
                _sessions.Remove(this);
            }

            _log.Info(
                "Session",
                "Disconnected",
                $"Session {SessionID} disconnected.");

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
