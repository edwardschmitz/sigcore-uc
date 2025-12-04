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
        private TcpClient _client = null;
        private NetworkStream _stream = null;
        private StreamReader _reader = null;
        private StreamWriter _writer = null;
        private uint sessionID = 0;
        private readonly IDispatcher _dispatcher;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        private CancellationTokenSource _cts = null;
        private Task _receiveTask = null;

        public string IPAddress { get; private set; } = "";
        public bool isCommander { get; private set; } = false;
        public bool IsConnected { get { return _client != null && _client.Connected; } }

        bool ISession.IsCommander { get { return isCommander; } set { isCommander = value; } }
        uint ISession.SessionID { get { return sessionID; } set { sessionID = value; } }

        public SigCoreClient(IDispatcher system) {
            _dispatcher = system;
        }

        // -------------------------------------------------------------
        // CONNECT  (safe for failure and reconnection)
        // -------------------------------------------------------------
        public async Task<bool> ConnectAsync(string ipAddress, int port = 7020) {
            Close();   // always reset previous state
            IPAddress = ipAddress;
            _cts = new CancellationTokenSource();

            try {
                _client = new TcpClient();

                // Attempt connection with timeout
                Task connectTask = _client.ConnectAsync(ipAddress, port);
                if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask) {
                    throw new TimeoutException("Connection attempt timed out.");
                }

                _stream = _client.GetStream();
                _reader = new StreamReader(_stream, Encoding.UTF8);
                _writer = new StreamWriter(_stream, Encoding.UTF8);
                _writer.AutoFlush = true;

                Console.WriteLine("Connected to SigCore server.");

                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
                return true;
            } catch (Exception ex) {
                Console.WriteLine("ConnectAsync failed: " + ex.Message);
                // Ensure full cleanup if anything failed mid-connection
                SafeDispose();
                return false;
            }
        }

        // -------------------------------------------------------------
        // RECEIVE LOOP
        // -------------------------------------------------------------
        private async Task ReceiveLoopAsync(CancellationToken token) {
            try {
                Console.WriteLine("Client receive loop started.");
                while (!token.IsCancellationRequested && IsConnected) {
                    string line = string.Empty;
                    try {
                        line = await _reader.ReadLineAsync();
                        if (line == null) break;
                    } catch (IOException) { break; } catch (ObjectDisposedException) { break; } catch (Exception ex) {
                        Console.WriteLine("Receive loop error: " + ex.Message);
                        break;
                    }

                    if (line.Length == 0) continue;

                    SigCoreMessage request;
                    try {
                        request = SigCoreProtocol.Decode(line);
                    } catch (Exception ex) {
                        Console.WriteLine("Decode error: " + ex.Message);
                        continue;
                    }

                    SigCoreMessage response = null;
                    try {
                        response = request.Dispatch(_dispatcher, this);
                    } catch (Exception ex) {
                        Console.WriteLine("Dispatch error: " + ex.Message);
                        response = SigCoreMessage.CreateError(request.Command, ex.Message, request.MsgId);
                    }

                    if (response != null) {
                        if (response.MsgId == 0) response.MsgId = request.MsgId;
                        try {
                            string encoded = SigCoreProtocol.Encode(response);
                            await _sendLock.WaitAsync(token);
                            try {
                                await _writer.WriteAsync(encoded);
                            } finally {
                                _sendLock.Release();
                            }
                        } catch (Exception ex) {
                            Console.WriteLine("Send error: " + ex.Message);
                            break;
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("Unexpected receive loop error: " + ex.Message);
            } finally {
                Console.WriteLine("Client receive loop ending.");
                // connection lost unexpectedly
                SafeDispose();
            }
        }

        // -------------------------------------------------------------
        // SEND
        // -------------------------------------------------------------
        public async Task SendAsync(SigCoreMessage msg) {
            if (_writer == null || !IsConnected) {
                Console.WriteLine("SendAsync aborted: not connected.");
                return;
            }

            string encoded = SigCoreProtocol.Encode(msg);

            await _sendLock.WaitAsync();
            try {
                if (_writer == null) return;
                await _writer.WriteAsync(encoded);
                await _writer.FlushAsync();
            } catch (ObjectDisposedException) {
                Console.WriteLine("SendAsync failed: connection closed.");
            } catch (IOException ioEx) {
                Console.WriteLine("SendAsync I/O error: " + ioEx.Message);
            } catch (Exception ex) {
                Console.WriteLine("SendAsync error: " + ex.Message);
            } finally {
                _sendLock.Release();
            }
        }

        public async Task Send(SigCoreMessage message) {
            await SendAsync(message);
        }

        // -------------------------------------------------------------
        // CLOSE  (manual or after failure)
        // -------------------------------------------------------------
        public void Close() {
            try {
                _cts?.Cancel();
                if (_receiveTask != null && !_receiveTask.IsCompleted) {
                    try { _receiveTask.Wait(1000); } catch { }
                }

                // Wait for any active send to finish
                _sendLock.Wait();
                _sendLock.Release();
            } catch { }

            SafeDispose();
        }

        // -------------------------------------------------------------
        // INTERNAL CLEANUP
        // -------------------------------------------------------------
        private void SafeDispose() {
            try { _reader?.Close(); } catch { }
            try { _writer?.Close(); } catch { }
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }

            _reader = null;
            _writer = null;
            _stream = null;
            _client = null;

            if (_cts != null) {
                _cts.Dispose();
                _cts = null;
            }

            Console.WriteLine("Client disconnected/closed.");
        }
    }
}
