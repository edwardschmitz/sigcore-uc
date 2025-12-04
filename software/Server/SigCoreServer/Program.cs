using SigCoreCommon;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreServer {
    class Program {
        static async Task Main(string[] args) {
            SigCoreServer server = new SigCoreServer();

            await server.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }
    }
}
