using System;

namespace SigCoreUpdateBuilder {
    class Program {
        static void Main(string[] args) {
            if (args.Length < 4) {
                Console.WriteLine("Usage:");
                Console.WriteLine("  builder.exe <revision> <version> <notes> <private_key.pem> [--include-launcher]");
                return;
            }

            string revision = args[0];
            string version = args[1];
            string notes = args[2];
            string privKey = args[3];

            bool includeLauncher = false;

            Console.WriteLine($"Arg Count {args.Length}");

            if (args.Length >= 5 && args[4] == "--include-launcher") {
                includeLauncher = true;
                Console.WriteLine($"Inclulde launcher");
            } else {
                Console.WriteLine($"NO launcher");

            }

            UpdateBuilder builder = new UpdateBuilder(revision, version, notes, privKey, includeLauncher);
            builder.Run();
        }
    }
}
