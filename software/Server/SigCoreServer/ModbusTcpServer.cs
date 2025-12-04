using SigCoreCommon;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SigCoreServer {
    public class ModbusTcpServer {
        private readonly HardwareManager _hardware;
        private TcpListener _listener;
        private bool _isRunning;
        private int _nextSessionId = 1;

        public ModbusTcpServer(HardwareManager hardware) {
            _hardware = hardware;
        }

        public async Task StartAsync() {
            _listener = new TcpListener(IPAddress.Any, 1502);
            _listener.Start();
            _isRunning = true;

            Console.WriteLine("Modbus TCP Server started on port 1502");

            while (_isRunning) {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                Console.WriteLine("--- Modbus Client Connected ---");

                ModbusClientSession session = new ModbusClientSession(client, _nextSessionId++, _hardware);
                _ = session.RunAsync(); // fire and forget
            }
        }

        public async Task StopAsync() {
            _isRunning = false;
            _listener.Stop();
            await Task.CompletedTask;
        }
    }
}
