using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SigCoreCommon;

namespace SigCoreCleint {
    public class SigCoreClient {
        private TcpClient _client;
        private NetworkStream _stream;
        private uint _sessionId;
        private bool _isCommander;
        private uint _msgIdCounter = 1;

        public bool IsConnected => _client?.Connected ?? false;
        public bool IsCommander => _isCommander;

        public async Task ConnectAsync(string ipAddress, int port = 7020) {
            _client = new TcpClient();
            await _client.ConnectAsync(ipAddress, port);
            _stream = _client.GetStream();
            Console.WriteLine("Connected to SigCore server.");
        }

        public async Task<SigCoreMessage> SendCommandAsync(SigCoreMessage cmd) {
            cmd.MsgId = _msgIdCounter++;

            string json = SigCoreProtocol.Encode(cmd);
            byte[] payload = Encoding.UTF8.GetBytes(json);

            await _stream.WriteAsync(payload, 0, payload.Length);

            string responseJson = await ReadMessageAsync();
            SigCoreMessage response = SigCoreProtocol.Decode(responseJson);
            return response;
        }

        private async Task<string> ReadMessageAsync() {
            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[1024];

            Console.WriteLine("start ReadMessageAsync");

            while (true) {
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) throw new Exception("Disconnected");

                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                string current = sb.ToString();

                int newlineIndex = current.IndexOf('\n');
                if (newlineIndex >= 0) {
                    string message = current.Substring(0, newlineIndex);
                    sb.Remove(0, newlineIndex + 1);
                    Console.WriteLine("message: " + message);
                    return message;
                }
            }
        }

        public async Task PingAsync() {
            SigCoreMessage pingCmd = SigCoreMessage.CreatePing(_msgIdCounter++);
            SigCoreMessage response = await SendCommandAsync(pingCmd);

            if (response.Status == "ok") {
                Console.WriteLine("Ping OK: " + response.Message);
            } else {
                Console.WriteLine("Ping ERROR: " + response.Message);
            }
        }

        public async Task SetRelayAsync(int channel, bool state) {
            if (!_isCommander) {
                Console.WriteLine("ERROR: This session is not the commander.");
                return;
            }

            SigCoreMessage cmd = SigCoreMessage.CreateSetRelay(_msgIdCounter++, channel, state);
            SigCoreMessage response = await SendCommandAsync(cmd);

            if (response.Status == "ok") {
                Console.WriteLine("OK: " + response.Message);
            } else {
                Console.WriteLine("ERROR: " + response.Message);
            }
        }

        public void Close() {
            try {
                _stream?.Close();
                _client?.Close();
            } catch (Exception ex) {
                Console.WriteLine("Close error: " + ex.Message);
            }
        }
    }
}
