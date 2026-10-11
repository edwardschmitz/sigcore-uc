using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SigCoreUpdater {
    public sealed class ReleaseConfig {
        [JsonPropertyName("executables")]
        public List<string> Executables { get; set; } = new List<string>();

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("revision")]
        public string Revision { get; set; } = string.Empty;

        public static ReleaseConfig Load(string path) {
            if (!File.Exists(path)) {
                throw new FileNotFoundException("Release config not found.", path);
            }

            string json = File.ReadAllText(path);
            ReleaseConfig config = JsonSerializer.Deserialize<ReleaseConfig>(
                json,
                new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true
                }
            );

            if (config == null) {
                throw new InvalidDataException("Release config could not be parsed.");
            }

            config.Validate();
            return config;
        }

        public void Validate() {
            if (string.IsNullOrWhiteSpace(Version)) {
                throw new InvalidDataException("Release config is missing version.");
            }

            if (string.IsNullOrWhiteSpace(Revision)) {
                throw new InvalidDataException("Release config is missing revision.");
            }

            if (Executables == null || Executables.Count == 0) {
                throw new InvalidDataException("Release config contains no executables.");
            }

            foreach (string executable in Executables) {
                if (string.IsNullOrWhiteSpace(executable)) {
                    throw new InvalidDataException("Release config contains an empty executable path.");
                }

                if (Path.IsPathRooted(executable)) {
                    throw new InvalidDataException(
                        $"Release executable path must be relative to the version directory: {executable}"
                    );
                }

                if (executable.Contains("..", StringComparison.Ordinal)) {
                    throw new InvalidDataException(
                        $"Release executable path may not escape the version directory: {executable}"
                    );
                }
            }
        }
    }
}
