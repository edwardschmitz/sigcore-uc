using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace SigCoreServer {
    public class ClientSession : ISession {
        // ---- Static list of active sessions ----
        private static readonly List<ClientSession> _sessions = new List<ClientSession>();
        private static readonly object _sessionsLock = new object();
        private int _lossySendInProgress = 0;

        private readonly object _sendLock = new object();
        private readonly IDispatcher _dispatcher;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;

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

            Client.Client.SendBufferSize = 4 * 1024;   
            Client.Client.NoDelay = true;              

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
            Console.WriteLine($"Session {SessionID} IsCommander set to {IsCommander}");
        }

        public async Task RunAsync() {
            try {
                while (true) {
                    string? line = await _reader.ReadLineAsync();
                    if (line == null) break;

                    SigCoreMessage request = SigCoreProtocol.Decode(line);

                    SigCoreMessage response;
                    try {
                        response = request.Dispatch(_dispatcher, this);
                    } catch (Exception ex) {
                        Console.WriteLine($"Dispatch error (Session {SessionID}): {ex}");
                        response = SigCoreMessage.CreateError(request.Command, ex.Message, request.MsgId);
                    }

                    if (response != null) {
                        if (response.MsgId == 0) response.MsgId = request.MsgId;
                        Send(response);
                    }
                }
            } catch (IOException ex) {
                Console.WriteLine($"Session {SessionID} IO error: {ex.Message}");
            } catch (Exception ex) {
                Console.WriteLine($"Session {SessionID} unexpected error: {ex}");
            } finally {
                Close();
            }
        }

        public async Task Send(SigCoreMessage msg) {
            string encoded = SigCoreProtocol.Encode(msg);
            lock (_sendLock) {
                _writer.WriteLine(encoded);
            }
        }

        public async Task SendOrDiscard(SigCoreMessage msg) {
            if (!IsConnected)
                return;

            string encoded = SigCoreProtocol.Encode(msg);
            byte[] buffer = Encoding.UTF8.GetBytes(encoded);

            using CancellationTokenSource cts = new CancellationTokenSource(1); // ~1ms "basically free" window

            try {
                await Stream.WriteAsync(buffer, 0, buffer.Length, cts.Token);
            } catch (OperationCanceledException) {
                // Expected: socket under pressure → drop message
            } catch {
                // Connection error or other failure → ignore for lossy traffic
            }
        }

        public void Close() {
            lock (_sessionsLock) {
                _sessions.Remove(this);
            }
            Client.Close();
            Console.WriteLine($"Session {SessionID} closed");
        }

        public static async Task SendToAsync(uint sessionId, SigCoreMessage msg) {
            ClientSession? target = null;
            lock (_sessionsLock) {
                target = _sessions.Find(s => s.SessionID == sessionId);
            }

            if (target != null)
                target.Send(msg);
        }

    }
}
