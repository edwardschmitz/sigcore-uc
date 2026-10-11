using MySqlConnector;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace SigCoreCommon {
    /// <summary>
    /// Shared SigCore event logger.
    ///
    /// Each process sets its application name once at startup:
    ///
    ///     Log.Initialize("SigCoreServer");
    ///
    /// The Launcher owns the logging session:
    ///
    ///     Log.Initialize("SigCoreLauncher", ownsSession: true);
    ///
    /// Child processes locate the newest non-stale session for this host.
    /// Application threads only enqueue log records. Database work happens on
    /// a dedicated background worker so a slow or unreachable logging database
    /// cannot stall normal application operation.
    /// </summary>
    public sealed class Log {
        private const string PropertiesPath = "/opt/sigcore/properties.json";
        private const string FallbackDirectory = "/tmp/sigcore-log";
        private const string FallbackPath = "/tmp/sigcore-log/log.txt";

        private const int SessionHeartbeatSeconds = 10;
        private const int SessionStaleSeconds = 60;
        private const int SessionValidationSeconds = 10;
        private const int MissingSessionRetrySeconds = 5;
        private const int DatabaseRetrySeconds = 30;

        private static readonly object InitializationLock = new object();
        private static readonly object SessionLock = new object();
        private static readonly object FallbackLock = new object();
        private static readonly object WorkerLock = new object();
        private static readonly object DatabaseStateLock = new object();

        private static readonly ConcurrentQueue<LogRecord> PendingWrites =
            new ConcurrentQueue<LogRecord>();

        private static readonly AutoResetEvent WorkerSignal =
            new AutoResetEvent(false);

        private static string _app;
        private static LoggerDatabaseConfig _databaseConfig;
        private static bool _configurationLoaded;
        private static bool _ownsSession;
        private static long? _sessionId;
        private static DateTimeOffset _lastSessionCheck = DateTimeOffset.MinValue;
        private static Timer _heartbeatTimer;
        private static Thread _workerThread;
        private static bool _workerStarted;

        private static DateTimeOffset _databaseRetryAfter = DateTimeOffset.MinValue;
        private static bool _databaseWasUnavailable;

        public Log(TimeSpan? repeatInterval = null) {
            // repeatInterval is retained for API compatibility.
            // Event throttling is intentionally disabled.
            _ = repeatInterval;
        }

        /// <summary>
        /// Sets the application identity for this process.
        ///
        /// The Launcher must pass ownsSession=true. Session creation and all
        /// database access are performed off the application's normal execution
        /// path. Failure falls back locally and is retried after a cooldown.
        /// </summary>
        public static void Initialize(string app, bool ownsSession = false) {
            if (string.IsNullOrWhiteSpace(app))
                throw new ArgumentException(
                    "An application name is required.",
                    nameof(app));

            string normalizedApp = app.Trim();

            lock (InitializationLock) {
                if (_app != null &&
                    !string.Equals(_app, normalizedApp, StringComparison.Ordinal)) {

                    throw new InvalidOperationException(
                        $"Log is already initialized for application '{_app}'.");
                }

                _app = normalizedApp;
                _ownsSession = _ownsSession || ownsSession;

                EnsureConfigurationLoaded();
                StartWorker();

                if (_ownsSession)
                    StartHeartbeat();

                WriteStartupRecord();
            }
        }

        private static void WriteStartupRecord() {
            try {
                Log startupLog = new Log();
                startupLog.Info(
                    "Lifecycle",
                    "Startup",
                    "Application starting.");
            } catch {
                // Startup logging is optional.
            }
        }

        public void Info(
            string category,
            string eventName,
            string message,
            [CallerFilePath] string sourceFile = "") {

            Write("INFO", category, eventName, message, sourceFile);
        }

        public void Warn(
            string category,
            string eventName,
            string message,
            [CallerFilePath] string sourceFile = "") {

            Write("WARN", category, eventName, message, sourceFile);
        }

        public void Error(
            string category,
            string eventName,
            string message,
            [CallerFilePath] string sourceFile = "") {

            Write("ERROR", category, eventName, message, sourceFile);
        }

        public void Error(
            string category,
            string eventName,
            string message,
            Exception ex,
            [CallerFilePath] string sourceFile = "") {

            if (ex == null) {
                Write("ERROR", category, eventName, message, sourceFile);
                return;
            }

            Write(
                "ERROR",
                category,
                eventName,
                $"{message} ({ex.GetType().Name}: {ex.Message})",
                sourceFile);
        }

        private void Write(
            string level,
            string category,
            string eventName,
            string message,
            string sourceFile) {

            try {
                if (string.IsNullOrWhiteSpace(category) ||
                    string.IsNullOrWhiteSpace(eventName) ||
                    string.IsNullOrWhiteSpace(message)) {

                    return;
                }

                LogRecord record = new LogRecord {
                    Timestamp = DateTimeOffset.Now,
                    Level = level,
                    App = string.IsNullOrWhiteSpace(_app) ? "unknown" : _app,
                    SourceFile = string.IsNullOrWhiteSpace(sourceFile)
                        ? "unknown"
                        : Path.GetFileName(sourceFile.Replace('\\', '/')),
                    Category = OneLine(category),
                    EventName = OneLine(eventName),
                    Message = OneLine(message)
                };

                StartWorker();
                PendingWrites.Enqueue(record);
                WorkerSignal.Set();
            } catch {
                // Logging must never interfere with application operation.
            }
        }

        private static void StartWorker() {
            if (_workerStarted)
                return;

            lock (WorkerLock) {
                if (_workerStarted)
                    return;

                _workerThread = new Thread(WorkerLoop) {
                    IsBackground = true,
                    Name = $"SigCoreLog-{_app ?? "unknown"}"
                };

                _workerStarted = true;
                _workerThread.Start();
            }
        }

        private static void WorkerLoop() {
            while (true) {
                try {
                    WorkerSignal.WaitOne();

                    while (PendingWrites.TryDequeue(out LogRecord record))
                        ProcessRecord(record);
                } catch (Exception ex) {
                    TryWriteDiagnosticFallback(
                        $"Logger worker failure: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static void ProcessRecord(LogRecord record) {
            try {
                if (CanAttemptDatabase() &&
                    TryWriteDatabase(
                        record.Timestamp,
                        record.Level,
                        record.App,
                        record.SourceFile,
                        record.Category,
                        record.EventName,
                        record.Message)) {

                    MarkDatabaseRecovered();
                    return;
                }
            } catch (Exception ex) {
                MarkDatabaseUnavailable("Database logging", ex);
            }

            TryWriteFallback(
                record.Timestamp,
                record.Level,
                record.App,
                record.SourceFile,
                record.Category,
                record.EventName,
                record.Message);
        }

        private static bool CanAttemptDatabase() {
            lock (DatabaseStateLock) {
                return DateTimeOffset.UtcNow >= _databaseRetryAfter;
            }
        }

        private static void MarkDatabaseUnavailable(string operation, Exception ex) {
            bool writeDiagnostic = false;

            lock (DatabaseStateLock) {
                DateTimeOffset now = DateTimeOffset.UtcNow;

                if (!_databaseWasUnavailable || now >= _databaseRetryAfter)
                    writeDiagnostic = true;

                _databaseWasUnavailable = true;
                _databaseRetryAfter = now.AddSeconds(DatabaseRetrySeconds);
            }

            if (writeDiagnostic) {
                TryWriteDiagnosticFallback(
                    $"{operation} unavailable: {ex.GetType().Name}: {ex.Message}. " +
                    $"Retrying in {DatabaseRetrySeconds} seconds.");
            }
        }

        private static void MarkDatabaseRecovered() {
            bool writeDiagnostic = false;

            lock (DatabaseStateLock) {
                if (_databaseWasUnavailable)
                    writeDiagnostic = true;

                _databaseWasUnavailable = false;
                _databaseRetryAfter = DateTimeOffset.MinValue;
            }

            if (writeDiagnostic)
                TryWriteDiagnosticFallback("Logger database connection recovered.");
        }

        private static bool TryWriteDatabase(
            DateTimeOffset timestamp,
            string level,
            string app,
            string sourceFile,
            string category,
            string eventName,
            string message) {

            EnsureConfigurationLoaded();

            LoggerDatabaseConfig config = _databaseConfig;
            if (config == null)
                return false;

            long? sessionId = TryGetCurrentSessionId();
            if (!sessionId.HasValue)
                return false;

            try {
                using MySqlConnection connection = new MySqlConnection(config.ConnectionString);
                connection.Open();

                const string sql = @"
INSERT INTO logrecord
    (sessionid, timestamp, level, app, source, category, event, message)
VALUES
    (@sessionid, @timestamp, @level, @app, @source, @category, @event, @message);";

                using MySqlCommand command = new MySqlCommand(sql, connection);
                command.CommandTimeout = config.CommandTimeoutSeconds;
                command.Parameters.AddWithValue("@sessionid", sessionId.Value);
                command.Parameters.AddWithValue("@timestamp", timestamp.UtcDateTime);
                command.Parameters.AddWithValue("@level", level);
                command.Parameters.AddWithValue("@app", app);
                command.Parameters.AddWithValue("@source", sourceFile);
                command.Parameters.AddWithValue("@category", category);
                command.Parameters.AddWithValue("@event", eventName);
                command.Parameters.AddWithValue("@message", message);

                return command.ExecuteNonQuery() == 1;
            } catch (Exception ex) {
                MarkDatabaseUnavailable("Database write", ex);
                return false;
            }
        }

        private static long? TryGetCurrentSessionId() {
            if (!CanAttemptDatabase())
                return null;

            lock (SessionLock) {
                if (_ownsSession) {
                    if (!_sessionId.HasValue)
                        TryEnsureOwnedSession();

                    return _sessionId;
                }

                DateTimeOffset now = DateTimeOffset.UtcNow;
                int checkInterval = _sessionId.HasValue
                    ? SessionValidationSeconds
                    : MissingSessionRetrySeconds;

                if (now - _lastSessionCheck < TimeSpan.FromSeconds(checkInterval))
                    return _sessionId;

                _lastSessionCheck = now;
                _sessionId = TryFindCurrentSession();
                return _sessionId;
            }
        }

        private static void TryEnsureOwnedSession() {
            if (!CanAttemptDatabase())
                return;

            lock (SessionLock) {
                if (_sessionId.HasValue)
                    return;

                EnsureConfigurationLoaded();

                LoggerDatabaseConfig config = _databaseConfig;
                if (config == null)
                    return;

                try {
                    using MySqlConnection connection = new MySqlConnection(config.ConnectionString);
                    connection.Open();

                    const string sql = @"
INSERT INTO logsession
    (startedat, lastheartbeatat, hostname, serialnumber, launcherpid)
VALUES
    (UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), @hostname, @serialnumber, @launcherpid);";

                    using MySqlCommand command = new MySqlCommand(sql, connection);
                    command.CommandTimeout = config.CommandTimeoutSeconds;
                    command.Parameters.AddWithValue("@hostname", config.HostName);
                    command.Parameters.AddWithValue("@serialnumber", config.SerialNumber);
                    command.Parameters.AddWithValue("@launcherpid", Process.GetCurrentProcess().Id);

                    if (command.ExecuteNonQuery() == 1)
                        _sessionId = command.LastInsertedId;
                } catch (Exception ex) {
                    MarkDatabaseUnavailable("Session creation", ex);
                    // Session creation failure is never fatal.
                }
            }
        }

        private static long? TryFindCurrentSession() {
            if (!CanAttemptDatabase())
                return null;

            EnsureConfigurationLoaded();

            LoggerDatabaseConfig config = _databaseConfig;
            if (config == null)
                return null;

            try {
                using MySqlConnection connection = new MySqlConnection(config.ConnectionString);
                connection.Open();

                const string sql = @"
SELECT id
FROM logsession
WHERE hostname = @hostname
  AND serialnumber = @serialnumber
  AND lastheartbeatat >= UTC_TIMESTAMP(6) - INTERVAL 60 SECOND
ORDER BY id DESC
LIMIT 1;";

                using MySqlCommand command = new MySqlCommand(sql, connection);
                command.CommandTimeout = config.CommandTimeoutSeconds;
                command.Parameters.AddWithValue("@hostname", config.HostName);
                command.Parameters.AddWithValue("@serialnumber", config.SerialNumber);

                object result = command.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return null;

                return Convert.ToInt64(result);
            } catch (Exception ex) {
                MarkDatabaseUnavailable("Session lookup", ex);
                return null;
            }
        }

        private static void StartHeartbeat() {
            if (_heartbeatTimer != null)
                return;

            _heartbeatTimer = new Timer(
                _ => Heartbeat(),
                null,
                TimeSpan.FromSeconds(SessionHeartbeatSeconds),
                TimeSpan.FromSeconds(SessionHeartbeatSeconds));
        }

        private static void Heartbeat() {
            if (!CanAttemptDatabase())
                return;

            try {
                lock (SessionLock) {
                    if (!_ownsSession)
                        return;

                    if (!_sessionId.HasValue) {
                        TryEnsureOwnedSession();
                        return;
                    }

                    LoggerDatabaseConfig config = _databaseConfig;
                    if (config == null)
                        return;

                    using MySqlConnection connection = new MySqlConnection(config.ConnectionString);
                    connection.Open();

                    const string sql = @"
UPDATE logsession
SET lastheartbeatat = UTC_TIMESTAMP(6)
WHERE id = @id;";

                    using MySqlCommand command = new MySqlCommand(sql, connection);
                    command.CommandTimeout = config.CommandTimeoutSeconds;
                    command.Parameters.AddWithValue("@id", _sessionId.Value);
                    command.ExecuteNonQuery();
                }

                MarkDatabaseRecovered();
            } catch (Exception ex) {
                MarkDatabaseUnavailable("Session heartbeat", ex);
                // Heartbeat failure affects logging only.
            }
        }

        private static void EnsureConfigurationLoaded() {
            if (_configurationLoaded)
                return;

            lock (InitializationLock) {
                if (_configurationLoaded)
                    return;

                _configurationLoaded = true;
                _databaseConfig = TryLoadDatabaseConfig();
            }
        }

        private static LoggerDatabaseConfig TryLoadDatabaseConfig() {
            try {
                if (!File.Exists(PropertiesPath)) {
                    TryWriteDiagnosticFallback(
                        $"Logger configuration file not found: {PropertiesPath}");
                    return null;
                }

                string json = File.ReadAllText(PropertiesPath, Encoding.UTF8);
                JObject root = JObject.Parse(json);

                string hostName = root["network"]?["hostname"]?.Value<string>();
                string serialNumber = root["global"]?["systemSerialNumber"]?.Value<string>();
                JToken logger = root["logger"];

                string server = logger?["server"]?.Value<string>();
                int port = logger?["port"]?.Value<int?>() ?? 3306;
                string database = logger?["database"]?.Value<string>();
                string user = logger?["user"]?.Value<string>();
                string password = logger?["password"]?.Value<string>();
                int connectionTimeout = logger?["connectionTimeoutSec"]?.Value<int?>() ?? 5;
                int commandTimeout = logger?["commandTimeoutSec"]?.Value<int?>() ?? 10;

                if (string.IsNullOrWhiteSpace(hostName) ||
                    string.IsNullOrWhiteSpace(serialNumber) ||
                    string.IsNullOrWhiteSpace(server) ||
                    string.IsNullOrWhiteSpace(database) ||
                    string.IsNullOrWhiteSpace(user) ||
                    string.IsNullOrWhiteSpace(password)) {

                    TryWriteDiagnosticFallback(
                        "Logger database configuration is incomplete in properties.json.");
                    return null;
                }

                MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder {
                    Server = server.Trim(),
                    Port = (uint)Math.Max(1, port),
                    Database = database.Trim(),
                    UserID = user.Trim(),
                    Password = password,
                    ConnectionTimeout = (uint)Math.Max(1, connectionTimeout),
                    DefaultCommandTimeout = (uint)Math.Max(1, commandTimeout)
                };

                TryWriteDiagnosticFallback(
                    $"Logger database configuration loaded: host={hostName.Trim()} serial={serialNumber.Trim()} server={server.Trim()} port={port} database={database.Trim()} user={user.Trim()}.");

                return new LoggerDatabaseConfig {
                    HostName = hostName.Trim(),
                    SerialNumber = serialNumber.Trim(),
                    ConnectionString = builder.ConnectionString,
                    CommandTimeoutSeconds = Math.Max(1, commandTimeout)
                };
            } catch (Exception ex) {
                TryWriteDiagnosticFallback(
                    $"Logger configuration load failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static void TryWriteFallback(
            DateTimeOffset timestamp,
            string level,
            string app,
            string sourceFile,
            string category,
            string eventName,
            string message) {

            try {
                Directory.CreateDirectory(FallbackDirectory);

                string line =
                    $"{timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} " +
                    $"[{level}] " +
                    $"app={OneLine(app)} " +
                    $"source={OneLine(sourceFile)} " +
                    $"category={category} " +
                    $"event={eventName} " +
                    $"message={message}";

                lock (FallbackLock) {
                    File.AppendAllText(
                        FallbackPath,
                        line + Environment.NewLine,
                        Encoding.UTF8);
                }
            } catch {
                // Local logging failure is also nonfatal.
            }
        }

        private static void TryWriteDiagnosticFallback(string message) {
            try {
                Directory.CreateDirectory(FallbackDirectory);

                string app = string.IsNullOrWhiteSpace(_app) ? "unknown" : _app;
                string line =
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} " +
                    "[LOGGER-DEBUG] " +
                    $"app={OneLine(app)} " +
                    $"message={OneLine(message)}";

                lock (FallbackLock) {
                    File.AppendAllText(
                        FallbackPath,
                        line + Environment.NewLine,
                        Encoding.UTF8);
                }
            } catch {
                // Debug logging must never affect application operation.
            }
        }

        private static string OneLine(string value) {
            if (value == null)
                return string.Empty;

            return value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }


        private sealed class LogRecord {
            public DateTimeOffset Timestamp { get; set; }
            public string Level { get; set; }
            public string App { get; set; }
            public string SourceFile { get; set; }
            public string Category { get; set; }
            public string EventName { get; set; }
            public string Message { get; set; }
        }

        private sealed class LoggerDatabaseConfig {
            public string HostName { get; set; }
            public string SerialNumber { get; set; }
            public string ConnectionString { get; set; }
            public int CommandTimeoutSeconds { get; set; }
        }
    }
}
