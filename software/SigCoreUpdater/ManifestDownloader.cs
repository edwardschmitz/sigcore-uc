using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreUpdater {
    public static class ManifestDownloader {
        private static readonly HttpClient client = new HttpClient();

        public static async Task DownloadAsync(
            string manifestUrl,
            string manifestSigUrl,
            string manifestLocalPath,
            string manifestSigLocalPath,
            CancellationToken token) {

            EnsureParentDirectory(manifestLocalPath);
            EnsureParentDirectory(manifestSigLocalPath);

            byte[] manifestBytes = await client.GetByteArrayAsync(manifestUrl, token);
            await File.WriteAllBytesAsync(manifestLocalPath, manifestBytes, token);

            byte[] sigBytes = await client.GetByteArrayAsync(manifestSigUrl, token);
            await File.WriteAllBytesAsync(manifestSigLocalPath, sigBytes, token);
        }

        private static void EnsureParentDirectory(string path) {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
