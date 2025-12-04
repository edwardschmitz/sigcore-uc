using SigCoreCommon;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SigCoreServer {
    public class ModbusClientSession {
        private readonly TcpClient _client;
        private readonly int _sessionId;
        private readonly HardwareManager _hardware;

        public ModbusClientSession(TcpClient client, int sessionId, HardwareManager hardware) {
            _client = client;
            _sessionId = sessionId;
            _hardware = hardware;
        }

        public async Task RunAsync() {
            try {
                using NetworkStream stream = _client.GetStream();
                byte[] buffer = new byte[260]; // Max Modbus TCP frame

                while (true) {
                    int length = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (length == 0) {
                        Console.WriteLine("Modbus client disconnected");
                        break;
                    }

                    if (length < 8) {
                        Console.WriteLine("Invalid Modbus request (too short)");
                        continue;
                    }

                    ushort transactionId = (ushort)((buffer[0] << 8) | buffer[1]);
                    byte functionCode = buffer[7];

                    byte[] response = HandleRequest(buffer, length, transactionId, functionCode);
                    if (response != null) {
                        await stream.WriteAsync(response, 0, response.Length);
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("Modbus session error: " + ex.Message);
            } finally {
                _client.Close();
            }
        }

        private byte[] HandleRequest(byte[] request, int length, ushort transactionId, byte functionCode) {
            switch (functionCode) {
                case 0x01: return HandleReadCoils(request, transactionId);
                case 0x02: return HandleReadDiscreteInputs(request, transactionId);
                case 0x03: return HandleReadHoldingRegisters(request, transactionId);
                case 0x04: return HandleReadInputRegisters(request, transactionId);
                case 0x05: return HandleWriteSingleCoil(request, transactionId);
                case 0x06: return HandleWriteSingleRegister(request, transactionId);
                case 0x0F: return HandleWriteMultipleCoils(request, transactionId);
                case 0x10: return HandleWriteMultipleRegisters(request, transactionId);
                default:
                    return BuildErrorResponse(transactionId, functionCode, 0x01); // Illegal Function
            }
        }

        private byte[] BuildErrorResponse(ushort transactionId, byte functionCode, byte errorCode) {
            byte[] response = new byte[9];
            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00; // Protocol ID
            response[3] = 0x00;
            response[4] = 0x00; // Length
            response[5] = 0x03;
            response[6] = 0x01; // Unit ID
            response[7] = (byte)(functionCode | 0x80); // Error response
            response[8] = errorCode;
            return response;
        }

        // === TODO: Implement these methods ===

        private byte[] HandleReadCoils(byte[] request, ushort transactionId) {
            // Extract start address and quantity
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            if (startAddress > 7 || quantity < 1 || (startAddress + quantity) > 8) {
                return BuildErrorResponse(transactionId, 0x01, 0x02); // Illegal Data Address
            }

            byte byteCount = (byte)((quantity + 7) / 8); // Number of bytes needed to hold coil bits
            byte[] coilStatus = new byte[byteCount];

            for (uint i = 0; i < quantity; i++) {
                uint coilIndex = startAddress + i;
                bool state = _hardware.GetRelayOutput(coilIndex); // Relay i = Coil i
                if (state) {
                    coilStatus[i / 8] |= (byte)(1 << ((int)i % 8));
                }
            }

            byte[] response = new byte[9 + byteCount];
            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00; // Protocol ID
            response[3] = 0x00;
            response[4] = 0x00;
            response[5] = (byte)(3 + byteCount); // Length
            response[6] = 0x01; // Unit ID
            response[7] = 0x01; // Function Code
            response[8] = byteCount;

            for (int i = 0; i < byteCount; i++) {
                response[9 + i] = coilStatus[i];
            }

            return response;
        }

        private byte[] HandleReadDiscreteInputs(byte[] request, ushort transactionId) {
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            if (startAddress > 7 || quantity < 1 || (startAddress + quantity) > 8) {
                return BuildErrorResponse(transactionId, 0x02, 0x02); // Illegal Data Address
            }

            byte byteCount = (byte)((quantity + 7) / 8);
            byte[] inputStatus = new byte[byteCount];

            for (int i = 0; i < quantity; i++) {
                uint inputIndex = (uint)(startAddress + i);
                bool state = _hardware.GetDIn(inputIndex);
                if (state) {
                    inputStatus[i / 8] |= (byte)(1 << (i % 8));
                }
            }

            byte[] response = new byte[9 + byteCount];
            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00;
            response[3] = 0x00;
            response[4] = 0x00;
            response[5] = (byte)(3 + byteCount);
            response[6] = 0x01;
            response[7] = 0x02; // Function Code
            response[8] = byteCount;

            for (int i = 0; i < byteCount; i++) {
                response[9 + i] = inputStatus[i];
            }

            return response;
        }

        private byte[] HandleReadHoldingRegisters(byte[] request, ushort transactionId) {
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            if (startAddress > 1 || quantity < 1 || (startAddress + quantity) > 2) {
                return BuildErrorResponse(transactionId, 0x03, 0x02); // Illegal Data Address
            }

            byte byteCount = (byte)(quantity * 2);
            byte[] response = new byte[9 + byteCount];

            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00;
            response[3] = 0x00;
            response[4] = 0x00;
            response[5] = (byte)(3 + byteCount);
            response[6] = 0x01;
            response[7] = 0x03;
            response[8] = byteCount;

            for (int i = 0; i < quantity; i++) {
                uint chan = (uint)(startAddress + i);
                double volts = _hardware.GetAOutValue(chan).Item1;
                ushort scaled = (ushort)Math.Clamp((int)(volts * 1000.0), 0, 10000);

                int byteIndex = 9 + (i * 2);
                response[byteIndex] = (byte)(scaled >> 8);
                response[byteIndex + 1] = (byte)(scaled & 0xFF);
            }

            return response;
        }

        private byte[] HandleReadInputRegisters(byte[] request, ushort transactionId) {
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            if (startAddress > 3 || quantity < 1 || (startAddress + quantity) > 4) {
                return BuildErrorResponse(transactionId, 0x04, 0x02); // Illegal Data Address
            }

            byte byteCount = (byte)(quantity * 2);
            byte[] response = new byte[9 + byteCount];

            // MBAP header
            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00;
            response[3] = 0x00;
            response[4] = (byte)((3 + byteCount) >> 8);
            response[5] = (byte)((3 + byteCount) & 0xFF);
            response[6] = 0x01;
            response[7] = 0x04; // Function Code
            response[8] = byteCount;

            for (int i = 0; i < quantity; i++) {
                uint chan = (uint)(startAddress + i);
                double volts = _hardware.GetAnalogIn(chan);
                ushort scaled = (ushort)Math.Clamp((int)(volts * 1000.0), 0, 10000);

                int byteIndex = 9 + (i * 2);
                response[byteIndex] = (byte)(scaled >> 8);
                response[byteIndex + 1] = (byte)(scaled & 0xFF);
            }

            return response;
        }

        private byte[] HandleWriteSingleCoil(byte[] request, ushort transactionId) {
            ushort coilAddress = (ushort)((request[8] << 8) | request[9]);
            ushort value = (ushort)((request[10] << 8) | request[11]);

            if (coilAddress > 7) {
                return BuildErrorResponse(transactionId, 0x05, 0x02); // Illegal Data Address
            }

            if (value != 0xFF00 && value != 0x0000) {
                return BuildErrorResponse(transactionId, 0x05, 0x03); // Illegal Data Value
            }

            bool state = (value == 0xFF00);
            //_hardware.SetRelay(coilAddress, state);

            // Echo back the request as confirmation (Modbus spec)
            byte[] response = new byte[12];
            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00;
            response[3] = 0x00;
            response[4] = 0x00;
            response[5] = 0x06;
            response[6] = 0x01; // Unit ID
            response[7] = 0x05; // Function Code
            response[8] = request[8]; // Echo address hi
            response[9] = request[9]; // Echo address lo
            response[10] = request[10]; // Echo value hi
            response[11] = request[11]; // Echo value lo

            return response;
        }

        private byte[] HandleWriteSingleRegister(byte[] request, ushort transactionId) {
            ushort registerAddress = (ushort)((request[8] << 8) | request[9]);
            ushort value = (ushort)((request[10] << 8) | request[11]);

            if (registerAddress > 1) {
                return BuildErrorResponse(transactionId, 0x06, 0x02); // Illegal Data Address
            }

            double volts = Math.Clamp(value / 1000.0, 0.0, 10.0);
            _hardware.SetAOutValue(registerAddress, volts);

            // Echo request back as confirmation
            byte[] response = new byte[12];
            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00;
            response[3] = 0x00;
            response[4] = 0x00;
            response[5] = 0x06;
            response[6] = 0x01; // Unit ID
            response[7] = 0x06; // Function Code
            response[8] = request[8]; // Echo addr hi
            response[9] = request[9]; // Echo addr lo
            response[10] = request[10]; // Echo value hi
            response[11] = request[11]; // Echo value lo

            return response;
        }

        private byte[] HandleWriteMultipleCoils(byte[] request, ushort transactionId) {
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);
            byte byteCount = request[12];

            if (startAddress > 7 || quantity < 1 || (startAddress + quantity) > 8 || byteCount < 1) {
                return BuildErrorResponse(transactionId, 0x0F, 0x02); // Illegal Data Address
            }

            for (int i = 0; i < quantity; i++) {
                int bitIndex = i;
                int byteIndex = bitIndex / 8;
                int bitPos = bitIndex % 8;

                bool state = (request[13 + byteIndex] & (1 << bitPos)) != 0;
                int relayIndex = startAddress + i;
                //_hardware.SetRelay((uint)relayIndex, state);
            }

            // Echo back address and quantity written
            byte[] response = new byte[12];
            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00;
            response[3] = 0x00;
            response[4] = 0x00;
            response[5] = 0x06;
            response[6] = 0x01; // Unit ID
            response[7] = 0x0F; // Function Code
            response[8] = request[8]; // Echo addr hi
            response[9] = request[9]; // Echo addr lo
            response[10] = request[10]; // Quantity hi
            response[11] = request[11]; // Quantity lo

            return response;
        }

        private byte[] HandleWriteMultipleRegisters(byte[] request, ushort transactionId) {
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);
            byte byteCount = request[12];

            if (startAddress > 1 || quantity < 1 || (startAddress + quantity) > 2 || byteCount != quantity * 2) {
                return BuildErrorResponse(transactionId, 0x10, 0x02); // Illegal Data Address or Length
            }

            for (int i = 0; i < quantity; i++) {
                int byteIndex = 13 + (i * 2);
                ushort rawValue = (ushort)((request[byteIndex] << 8) | request[byteIndex + 1]);
                double volts = Math.Clamp(rawValue / 1000.0, 0.0, 10.0);

                uint channel = (uint)(startAddress + i);
                _hardware.SetAOutValue(channel, volts);
            }

            byte[] response = new byte[12];
            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00;
            response[3] = 0x00;
            response[4] = 0x00;
            response[5] = 0x06;
            response[6] = 0x01;
            response[7] = 0x10;
            response[8] = request[8];  // Start address hi
            response[9] = request[9];  // Start address lo
            response[10] = request[10]; // Quantity hi
            response[11] = request[11]; // Quantity lo

            return response;
        }
    }
}
