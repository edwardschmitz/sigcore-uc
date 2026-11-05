using SigCoreCommon;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreServer {
    class Program {
        private static readonly Log log = new Log();
        static async Task Main(string[] args) {
            Log.Initialize("SigCoreServer");
            SigCoreServer server = new SigCoreServer();

            await server.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }
    }
}
