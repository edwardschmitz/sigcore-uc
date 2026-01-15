using System;
using System.IO;
using System.Text;

namespace SigCoreCommon {
    public sealed class LogFile {
        private readonly string _path;
        private readonly object _lock = new object();

        public LogFile(string path = "/opt/log.txt") {
            _path = path ?? throw new ArgumentNullException(nameof(path));

            string directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
        }

        public void Info(string message) {
            Write("INFO", message);
        }

        public void Warn(string message) {
            Write("WARN", message);
        }

        public void Error(string message) {
            Write("ERROR", message);
        }

        public void Error(string message, Exception ex) {
            Write("ERROR", $"{message}\n{ex}");
        }

        private void Write(string level, string message) {
            // Local time with offset keeps logs human-readable AND debuggable
            string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");

            string line = $"{timestamp} [{level}] {message}";

            lock (_lock) {
                File.AppendAllText(
                    _path,
                    line + Environment.NewLine,
                    Encoding.UTF8
                );
            }
        }
    }
}
