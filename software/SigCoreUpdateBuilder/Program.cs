using System;

namespace SigCoreUpdateBuilder {
    class Program {
        static void Main(string[] args) {
            if (args.Length < 4) {
                Console.WriteLine("Usage:");
                Console.WriteLine("  builder.exe <revision> <version> <notes> <private_key.pem>");
                return;
            }

            string revision = args[0];
            string version = args[1];
            string notes = args[2];
            string privKey = args[3];

            UpdateBuilder builder = new UpdateBuilder(revision, version, notes, privKey);
            builder.Run();
        }
    }
}
