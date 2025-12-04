using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreLauncher {
    public static class ManifestDownloader {

        public static async Task DownloadAsync(
            string manifestUrl,
            string manifestSigUrl,
            string manifestLocalPath,
            string manifestSigLocalPath,
            CancellationToken token) {

            HttpClient client = new HttpClient();

            byte[] manifestBytes = await client.GetByteArrayAsync(manifestUrl, token);
            File.WriteAllBytes(manifestLocalPath, manifestBytes);

            byte[] sigBytes = await client.GetByteArrayAsync(manifestSigUrl, token);
            File.WriteAllBytes(manifestSigLocalPath, sigBytes);
        }
    }
}
