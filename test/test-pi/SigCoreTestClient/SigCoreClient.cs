//using Newtonsoft.Json.Linq;
//using SigCoreCommon;
//using SigCoreTestClient;
//using System;
//using System.IO;
//using System.Net.Sockets;
//using System.Runtime.ConstrainedExecution;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Interop;
//using System.Windows.Threading;
//using static SigCoreCommon.A_IN;

//namespace SigCoreClientApp {
//    public class SigCoreClient : IDispatcher, ISession {
//        private TcpClient _client = null;
//        private NetworkStream _stream = null;
//        private StreamReader _reader = null;
//        private StreamWriter _writer = null;
//        private uint sessionID = 0;
//        private MainWindow window;
//        public bool isCommander=false;
//        public bool IsConnected => _client?.Connected ?? false;


//        bool ISession.IsCommander { get => isCommander; set => isCommander=value; }
//        uint ISession.SessionID { get => sessionID; set => sessionID=value; }

//        public SigCoreClient(SigCoreTestClient.MainWindow mainWindow) {
//            window = mainWindow;
//        }

//        public async Task ConnectAsync(string ipAddress, int port = 7020) {
//            _client = new TcpClient();
//            await _client.ConnectAsync(ipAddress, port);
//            _stream = _client.GetStream();
//            _reader = new StreamReader(_stream, Encoding.UTF8);
//            _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };

//            Console.WriteLine("Connected to SigCore server.");

//            // Start background receive loop
//            _ = Task.Run(() => ReceiveLoopAsync());
//        }

//        private async Task ReceiveLoopAsync() {

//            try {
//                Console.WriteLine("Client receive loop started.");
//                while (IsConnected) {
//                    string? line;
//                    try {
//                        line = await _reader.ReadLineAsync();
//                        if (line == null) break;
//                    } catch (Exception ex) {
//                        Console.WriteLine("Receive loop error: " + ex.Message);
//                        break;
//                    }

//                    if (line.Length > 0) {
//                        SigCoreMessage request = SigCoreProtocol.Decode(line);

//                        SigCoreMessage? response;
//                        try {
//                            response = request.Dispatch(this, this); // client doesn’t track sessions
//                        } catch (Exception ex) {
//                            Console.WriteLine($"Error: cmd:{request.Command}, ex:{ex.Message}");
//                            response = SigCoreMessage.CreateError(request.Command, ex.Message, request.MsgId);
//                        }

//                        if (response != null) {
//                            if (response.MsgId == 0) {
//                                response.MsgId = request.MsgId;
//                            }

//                            string encoded = SigCoreProtocol.Encode(response);
//                            await _writer.WriteAsync(encoded);
//                        }
//                    }
//                }
//            } catch (IOException ex) {
//                Console.WriteLine($"IO error: {ex.Message}");
//            } catch (Exception ex) {
//                Console.WriteLine($"unexpected error: {ex}");
//            }

//            Console.WriteLine("Client receive loop ending.");
//        }

//        public async Task SendAsync(SigCoreMessage msg) {
//            if (!IsConnected) throw new InvalidOperationException("Not connected.");
//            string encoded = SigCoreProtocol.Encode(msg);
//            await _writer.WriteAsync(encoded);
//        }

//        // Convenience helpers
//        public async Task PingAsync() {
//            SigCoreMessage pingCmd = SigCoreMessage.CreatePing();
//            await SendAsync(pingCmd);
//        }

//        public async Task SetRelayAsync(uint channel, bool state) {
//            SigCoreMessage cmd = SigCoreMessage.CreateSetRelay(channel, state);
//            Console.WriteLine("Relay state change");
//            await SendAsync(cmd);
//        }

//        public async Task SetAOut(uint chan, double val) {
//            Console.WriteLine($"chan: {chan}, val: {val}");
//            SigCoreMessage cmd = SigCoreMessage.CreateSetAOut(chan, val);
//            await SendAsync(cmd);
//        }

//        public async Task GetDin(uint chan) {
//            SigCoreMessage cmd = SigCoreMessage.CreateGetDIn(chan);
//            await SendAsync(cmd);
//        }

//        public void Close() {
//            try {
//                _reader?.Close();
//                _writer?.Close();
//                _stream?.Close();
//                _client?.Close();
//            } catch (Exception ex) {
//                Console.WriteLine("Close error: " + ex.Message);
//            }
//        }

//        public SigCoreMessage HandlePing(JObject payload, ulong msgId, ISession session) {
//            return null;
//        }

//        public SigCoreMessage HandleAck(JObject payload, ulong msgId, ISession session) {
//            Console.WriteLine("Acknowledged");
//            return null;
//        }


//        public SigCoreMessage HandleConnect(JObject payload, ulong msgId, ISession session, string ver) {
//            Console.WriteLine($"Payload: {payload}");
//            sessionID = payload.Value<uint>("sessionID");
//            isCommander = payload.Value<bool>("isCommander");
//            string _ver = payload.Value<string>("version")!;
//            if (ver != _ver) {
//                throw new Exception($"Version mismatch: Server={_ver}, Client={ver}");
//            }

//            string commanderStr = isCommander ? "COMMANDER" : "MONITOR";
//            Application.Current.Dispatcher.Invoke(() => {
//                window.Title = $"Client >>> {commanderStr}";
//            });
//            Console.WriteLine($"Session established: ID={sessionID}, IsCommander={isCommander}");
//            return null;
//        }

//        public SigCoreMessage HandlePong(JObject payload, ulong msgId, ISession session) {
//            Console.WriteLine("Pong");
//            return null;
//        }


//        public SigCoreMessage HandleAnalogInValue(JObject payload, ulong msgID, ISession session) {
//            uint chan = payload.Value<uint>("channel");
//            double val = payload.Value<double>("value");
//            return null;
//        }

//        public SigCoreMessage HandleAInConfig(JObject payload, ulong msgID, ISession session) {
//            uint channel = payload.Value<uint>("channel");
//            Application.Current.Dispatcher.Invoke(() => {
//                window.UpdateAnalogInConfig(channel, payload);
//            });
//            return null;
//        }


//        internal void GetConfig0() {
//            SigCoreMessage msg = SigCoreMessage.CreateGetAInConfig(0);
//        }


//        public SigCoreMessage HandleDIn(JObject payload, ulong msgID, ISession session) {
//            int channel = payload.Value<int>("channel");
//            bool state = payload.Value<bool>("state");
//            window.UpdateDigitalInput(channel, state);
//            return null;
//        }


//        public SigCoreMessage HandleAOut(JObject payload, ulong msgID, ISession session) {
//            int channel = payload.Value<int>("channel");
//            double val = payload.Value<double>("value");

//            Console.WriteLine($"Analog Output Value: ch:{channel}, val{val}");
//            return null;
//        }


//        public SigCoreMessage HandleAOutConfig(JObject payload, ulong msgID, ISession session) {
//            uint channel = payload.Value<uint>("channel");
//            Application.Current.Dispatcher.Invoke(() => {
//                window.UpdateAnalogOutConfig(channel, payload);
//            });
//            return null;
//        }


//        public async Task ConfigureChannel(ushort chan) {
//            SigCoreMessage msg = SigCoreMessage.CreateGetAInConfig(chan);
//            await SendAsync(msg);
//        }

//        public SigCoreMessage HandleGlobalConfig(JObject payload, ulong msgId, ISession session) {
//            Application.Current.Dispatcher.Invoke(() => {
//                window.UpdateGlobalConfig(payload);
//            });
//            return null;
//        }


//        public SigCoreMessage HandleRelayConfig(JObject payload, ulong msgId, ISession session) {
//            Application.Current.Dispatcher.Invoke(() => {
//                window.UpdateRelayConfig(payload);
//            });
//            return null;
//        }


//        public SigCoreMessage HandleDInConfig(JObject payload, ulong msgId, ISession session) {
//            Application.Current.Dispatcher.Invoke(() => {
//                window.UpdateDInConfig(payload);
//            });
//            return null;
//        }


//        public SigCoreMessage HandleDInChangeAlert(JObject payload, ulong msgId, ISession session) {
//            bool[] vals = payload["values"]?.ToObject<bool[]>();
//            if (vals == null)
//                return null;

//            Application.Current.Dispatcher.Invoke(() => {
//                window.DInChanged(vals);
//            });

//            return null;
//        }

//        public SigCoreMessage HandleAInChangeAlert(JObject payload, ulong msgId, ISession session) {
//            double[] vals = payload["values"]?.ToObject<double[]>();

//            Application.Current.Dispatcher.Invoke(() => {
//                window.AInChanged(vals);
//            });
//            return null;
//        }

//        public SigCoreMessage HandleRelayChangeAlert(JObject payload, ulong msgId, ISession session) {
//            bool val = payload.Value<bool>("value");
//            uint channel = payload.Value<uint>("channel");
//            Application.Current.Dispatcher.Invoke(() => {
//                window.RelayChanged(channel, val);
//            });
//            return null;
//        }

//        public SigCoreMessage HandleAOutChangeAlert(JObject payload, ulong msgId, ISession session) {
//            double val = payload.Value<double>("value");
//            uint channel = payload.Value<uint>("channel");
//            Application.Current.Dispatcher.Invoke(() => {
//                window.AOutChanged(channel, val);
//            });
//            return null;
//        }

//        public SigCoreMessage HandlePIDCurValChangeAlert(JObject payload, ulong msgId, ISession session) {
//            PID_LOOP.CurrentValues vals = new PID_LOOP.CurrentValues();
//            vals.FromPayload(payload);
//            uint channel = payload.Value<uint>("channel");
//            Application.Current.Dispatcher.Invoke(() => window.PIDChanged(channel, vals));
//            return null;
//        }

//        internal async Task Subscribe() {
//            SigCoreMessage msg = SigCoreMessage.CreateSubscribe();
//            await SendAsync(msg);
//        }

//        public SigCoreMessage HandleSetRelay(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleGetRelay(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleRelay(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleStatus(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleError(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleGetAnalogIn(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleGetAInConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleSetAInConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleGetDIn(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleGetAOut(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleSetAOut(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleGetAOutConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleSetAOutConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleGetGlobalConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleSetGlobalConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleGetRelayConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleSetRelayConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleGetDInConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleSetDInConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleGetPIDConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleSetPIDConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandlePIDConfig(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleGetPIDCurVal(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleSetPIDCurVal(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandlePIDCurVal(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public SigCoreMessage HandleSubscribe(JObject payload, ulong msgId, ISession session) => throw new NotImplementedException();
//        public Task NotifyDInChanged(bool[] vals) => throw new NotImplementedException();
//        public Task NotifyAInChanged(double[] vals) => throw new NotImplementedException();
//        public Task NotifyRelayChange(uint channel, bool val) => throw new NotImplementedException();
//        public Task NotifyAOutChanged(uint channel, double val) => throw new NotImplementedException();
//        public Task NotifyPIDChanged(uint channel, PID_LOOP.CurrentValues vals) => throw new NotImplementedException();
//    }
//}
