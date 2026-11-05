using SigCoreCommon;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreUpdater {
    // Reports verified update milestones to the separately hosted inventory system.
    // Device-specific configuration lives outside the release directories.
    internal sealed class UpdateEventReporter {
        private const string ConfigPath = "/opt/sigcore/update-telemetry.json";
        private const string PropertiesPath = "/opt/sigcore/properties.json";
        private static readonly HttpClient Client = new HttpClient {
            Timeout = TimeSpan.FromSeconds(5)
        };
        private readonly Log log = new Log();

        public async Task ReportDownloadedAsync(
            string revision, string version, DateTimeOffset occurredAt, CancellationToken token) {

            // Not provisioned? Updating remains functional, with no reporting.
            if (!File.Exists(ConfigPath)) return;

            try {
                using JsonDocument config = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                string url = config.RootElement.GetProperty("url").GetString() ?? "";
                string key = config.RootElement.GetProperty("token").GetString() ?? "";
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri endpoint) ||
                    endpoint.Scheme != Uri.UriSchemeHttps ||
                    key.Length < 32) {
                    log.Warn("Telemetry", "InvalidConfiguration",
                        "Update telemetry configuration requires an HTTPS URL and a valid token.");
                    return;
                }

                using JsonDocument properties = JsonDocument.Parse(File.ReadAllText(PropertiesPath));
                string serial = properties.RootElement
                    .GetProperty("global")
                    .GetProperty("systemSerialNumber")
                    .GetString() ?? "";
                if (string.IsNullOrWhiteSpace(serial)) {
                    log.Warn("Telemetry", "MissingSerial", "Cannot report an update without a device serial number.");
                    return;
                }

                string payload = JsonSerializer.Serialize(new {
                    serial,
                    revision,
                    version,
                    @event = "DOWNLOADED",
                    occurredAt = occurredAt.ToUniversalTime().ToString(
                        "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
                });

                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.TryAddWithoutValidation("X-SigCore-Telemetry-Key", key);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                using HttpResponseMessage response = await Client.SendAsync(request, token);
                if (!response.IsSuccessStatusCode) {
                    log.Warn("Telemetry", "ReportRejected",
                        $"Inventory server rejected DOWNLOADED for {version}: HTTP {(int)response.StatusCode}.");
                }
            } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                // Reporting is observational, never a prerequisite for installing an update.
                log.Warn("Telemetry", "ReportUnavailable",
                    $"Could not report DOWNLOADED for {version}: {ex.GetType().Name}.");
            }
        }
    }
}
