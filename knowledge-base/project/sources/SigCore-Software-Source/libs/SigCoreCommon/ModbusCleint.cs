using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SigCoreCommon {
    public class ModbusClient : IDisposable {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private ushort _transactionId = 0;

        public ModbusClient() {
        }

        public bool IsConnected => _tcpClient?.Connected ?? false;

        public async Task<bool> ConnectAsync(string ipAddress, int port = 502) {
            try {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ipAddress, port);
                _stream = _tcpClient.GetStream();
                return true;
            } catch {
                return false;
            }
        }

        public void Disconnect() {
            _stream?.Close();
            _tcpClient?.Close();
        }

        public async Task<ushort[]> ReadHoldingRegistersAsync(byte unitId, ushort startAddress, ushort count) {
            byte[] request = BuildReadRequest(unitId, startAddress, count);
            await _stream.WriteAsync(request, 0, request.Length);

            byte[] response = new byte[5 + 2 + count * 2];
            await _stream.ReadAsync(response, 0, response.Length);

            ushort[] registers = new ushort[count];
            for (int i = 0; i < count; i++) {
                registers[i] = (ushort)(response[9 + i * 2] << 8 | response[10 + i * 2]);
            }
            return registers;
        }

        public async Task<bool> WriteSingleRegisterAsync(byte unitId, ushort address, ushort value) {
            byte[] request = BuildWriteRequest(unitId, address, value);
            await _stream.WriteAsync(request, 0, request.Length);

            byte[] response = new byte[12];
            int read = await _stream.ReadAsync(response, 0, response.Length);

            // Validate echo of sent data
            return read == 12 &&
                   response[7] == 6 &&
                   response[8] == (byte)(address >> 8) &&
                   response[9] == (byte)(address & 0xFF) &&
                   response[10] == (byte)(value >> 8) &&
                   response[11] == (byte)(value & 0xFF);
        }

        private byte[] BuildReadRequest(byte unitId, ushort startAddress, ushort count) {
            byte[] buffer = new byte[12];
            _transactionId++;

            buffer[0] = (byte)(_transactionId >> 8); // Transaction ID
            buffer[1] = (byte)(_transactionId & 0xFF);
            buffer[2] = 0; buffer[3] = 0;             // Protocol ID
            buffer[4] = 0; buffer[5] = 6;             // Length
            buffer[6] = unitId;                      // Unit ID
            buffer[7] = 3;                           // Function code: Read Holding Registers
            buffer[8] = (byte)(startAddress >> 8);
            buffer[9] = (byte)(startAddress & 0xFF);
            buffer[10] = (byte)(count >> 8);
            buffer[11] = (byte)(count & 0xFF);
            return buffer;
        }

        private byte[] BuildWriteRequest(byte unitId, ushort address, ushort value) {
            byte[] buffer = new byte[12];
            _transactionId++;

            buffer[0] = (byte)(_transactionId >> 8); // Transaction ID
            buffer[1] = (byte)(_transactionId & 0xFF);
            buffer[2] = 0; buffer[3] = 0;             // Protocol ID
            buffer[4] = 0; buffer[5] = 6;             // Length
            buffer[6] = unitId;                      // Unit ID
            buffer[7] = 6;                           // Function code: Write Single Register
            buffer[8] = (byte)(address >> 8);
            buffer[9] = (byte)(address & 0xFF);
            buffer[10] = (byte)(value >> 8);
            buffer[11] = (byte)(value & 0xFF);
            return buffer;
        }

        public void Dispose() {
            Disconnect();
        }
    }
}
