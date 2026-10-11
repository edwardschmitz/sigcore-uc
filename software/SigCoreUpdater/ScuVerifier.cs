using SigCoreCommon.Update;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreUpdater {
    public static class ScuVerifier {
        private static readonly HttpClient client = new HttpClient();

        public static async Task DownloadAsync(
            string scuUrl,
            string scuSigUrl,
            string scuLocalPath,
            string scuSigLocalPath,
            CancellationToken token) {

            EnsureParentDirectory(scuLocalPath);
            EnsureParentDirectory(scuSigLocalPath);

            byte[] scuBytes = await client.GetByteArrayAsync(scuUrl, token);
            await File.WriteAllBytesAsync(scuLocalPath, scuBytes, token);

            byte[] sigBytes = await client.GetByteArrayAsync(scuSigUrl, token);
            await File.WriteAllBytesAsync(scuSigLocalPath, sigBytes, token);
        }

        public static bool Validate(
            string scuPath,
            string scuSigPath,
            string publicKeyPemPath,
            string expectedSha256) {

            string hash = HashHelper.Sha256File(scuPath);

            if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            return SignatureHelper.VerifyFile(
                publicKeyPemPath,
                scuPath,
                scuSigPath
            );
        }

        private static void EnsureParentDirectory(string path) {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
