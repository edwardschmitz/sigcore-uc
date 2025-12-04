using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using SigCoreCommon.Update;

namespace SigCoreLauncher {
    public static class ScuVerifier {
        // =====================================================================
        // Downloads SCU + signature
        // =====================================================================
        public static async Task DownloadAsync(
            string scuUrl,
            string scuSigUrl,
            string scuLocalPath,
            string scuSigLocalPath) {

            Console.WriteLine($"{scuUrl}\n{scuSigUrl}\n{scuLocalPath}\n{scuSigLocalPath}");

            HttpClient client = new HttpClient();

            byte[] scuBytes = await client.GetByteArrayAsync(scuUrl);
            File.WriteAllBytes(scuLocalPath, scuBytes);

            byte[] sigBytes = await client.GetByteArrayAsync(scuSigUrl);
            File.WriteAllBytes(scuSigLocalPath, sigBytes);
        }

        // =====================================================================
        // Fully validate SCU integrity
        // =====================================================================
        public static bool Validate(
            string scuPath,
            string scuSigPath,
            string publicKeyPemPath,
            string expectedSha256) {
            // 1 — Check hash
            string hash = HashHelper.Sha256File(scuPath);

            if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                return false;

            // 2 — Check signature
            bool sigOk = SignatureHelper.VerifyFile(publicKeyPemPath, scuPath, scuSigPath);
            if (!sigOk)
                return false;

            return true;
        }
    }
}
