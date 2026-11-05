using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreUpdater {
    // Runs only the tasks in the ACTIVE release, once per Updater process startup.
    public sealed class InstallationTaskRunner {
        private const string ActiveRoot = "/opt/sigcore/current";
        private const string TasksFile = "installation-tasks.json";
        private const string CompletionPath = "/opt/sigcore/completed-tasks.json";
        private readonly Log log = new Log();
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public async Task RunAsync(ReleaseConfig runningRelease, CancellationToken token) {
            string activeConfigPath = Path.Combine(ActiveRoot, "config.json");
            string tasksPath = Path.Combine(ActiveRoot, TasksFile);
            if (!File.Exists(tasksPath)) return;

            // Do not run the tasks of a different release while the updater is still
            // executing from an older release or before activation is complete.
            ReleaseConfig activeRelease = ReleaseConfig.Load(activeConfigPath);
            if (!string.Equals(activeRelease.Version, runningRelease.Version, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(activeRelease.Revision, runningRelease.Revision, StringComparison.OrdinalIgnoreCase)) {
                log.Warn("Installation", "ReleaseMismatch", "Active release differs from running Updater; installation tasks postponed.");
                return;
            }

            TaskFile tasks = JsonSerializer.Deserialize<TaskFile>(File.ReadAllText(tasksPath), JsonOptions)
                ?? throw new InvalidDataException("Installation task file is invalid.");
            Validate(tasks);

            CompletionFile completion = File.Exists(CompletionPath)
                ? JsonSerializer.Deserialize<CompletionFile>(File.ReadAllText(CompletionPath), JsonOptions)
                    ?? throw new InvalidDataException("Completion record is invalid.")
                : new CompletionFile();
            if (completion.CompletedTasks == null || completion.CompletedTasks.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("Completion record contains invalid task IDs.");

            HashSet<string> completed = new HashSet<string>(completion.CompletedTasks, StringComparer.Ordinal);
            foreach (InstallTask task in tasks.Tasks) {
                token.ThrowIfCancellationRequested();
                if (completed.Contains(task.Id)) continue;
                try {
                    if (task.Check != null && await ExecuteAsync(task.Check, token) == 0) {
                        log.Info("Installation", "AlreadySatisfied", $"Task {task.Id} already satisfied.");
                    } else {
                        log.Info("Installation", "Starting", $"Running one-time task {task.Id}.");
                        foreach (TaskCommand command in task.Commands) {
                            int exitCode = await ExecuteAsync(command, token);
                            if (exitCode != 0)
                                throw new InvalidOperationException($"Task {task.Id} command {command.File} exited with {exitCode}.");
                        }
                        if (task.Check != null && await ExecuteAsync(task.Check, token) != 0)
                            throw new InvalidOperationException($"Task {task.Id} post-installation verification failed.");
                    }

                    // Persist each success immediately. A crash between command success
                    // and this write causes a safe re-check on the next startup.
                    completion.CompletedTasks.Add(task.Id);
                    SaveAtomically(completion);
                    completed.Add(task.Id);
                    log.Info("Installation", "Completed", $"One-time task {task.Id} completed.");
                } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    throw;
                } catch (Exception ex) {
                    // Do not block Core or normal update checking on a failed task.
                    log.Error("Installation", "TaskFailed", $"One-time task {task.Id} failed; retry at next Updater startup.", ex);
                    // Continue to independent tasks; this task remains incomplete.
                }
            }
        }

        private async Task<int> ExecuteAsync(TaskCommand command, CancellationToken token) {
            ProcessStartInfo info = new ProcessStartInfo {
                FileName = command.File,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = ActiveRoot
            };
            foreach (string arg in command.Arguments) info.ArgumentList.Add(arg);
            using Process process = new Process { StartInfo = info };
            if (!process.Start()) throw new InvalidOperationException("Cannot launch " + command.File);
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            try {
                await process.WaitForExitAsync(token);
            } catch (OperationCanceledException) {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                throw;
            }
            // Both streams were drained concurrently to avoid a full-pipe deadlock.
            await Task.WhenAll(stdout, stderr);
            LogOutput(command.File, "stdout", stdout.Result, isError: false);
            LogOutput(command.File, "stderr", stderr.Result, isError: true);
            log.Info("Installation", "CommandExit", $"{command.File} exited with code {process.ExitCode}.");
            return process.ExitCode;
        }

        private void LogOutput(string command, string stream, string output, bool isError) {
            using StringReader reader = new StringReader(output);
            string? line;
            while ((line = reader.ReadLine()) != null) {
                if (line.Length == 0) continue;
                string message = $"{command} [{stream}]: {line}";
                if (isError)
                    log.Warn("Installation", "CommandOutput", message);
                else
                    log.Info("Installation", "CommandOutput", message);
            }
        }

        private static void SaveAtomically(CompletionFile completion) {
            string tempPath = CompletionPath + ".tmp";
            try {
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                    JsonSerializer.Serialize(stream, completion, JsonOptions);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(tempPath, CompletionPath, overwrite: true);
            } finally {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        private static void Validate(TaskFile file) {
            if (file.Tasks == null) throw new InvalidDataException("Missing installation tasks array.");
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (InstallTask task in file.Tasks) {
                if (string.IsNullOrWhiteSpace(task.Id) || !ids.Add(task.Id))
                    throw new InvalidDataException("Installation task IDs must be nonempty and unique.");
                if (task.Commands == null || task.Commands.Count == 0)
                    throw new InvalidDataException($"Task {task.Id} has no commands.");
                foreach (TaskCommand command in task.Commands) ValidateCommand(command);
                if (task.Check != null) ValidateCommand(task.Check);
            }
        }

        private static void ValidateCommand(TaskCommand command) {
            if (command == null || string.IsNullOrWhiteSpace(command.File) ||
                command.Arguments == null || command.Arguments.Any(x => x == null))
                throw new InvalidDataException("Invalid installation command.");
        }

        private sealed class TaskFile {
            [JsonPropertyName("tasks")]
            public List<InstallTask> Tasks { get; set; } = new List<InstallTask>();
        }
        private sealed class InstallTask {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            [JsonPropertyName("check")]
            public TaskCommand? Check { get; set; }
            [JsonPropertyName("commands")]
            public List<TaskCommand> Commands { get; set; } = new List<TaskCommand>();
        }
        private sealed class TaskCommand {
            [JsonPropertyName("command")]
            public string File { get; set; } = string.Empty;
            [JsonPropertyName("arguments")]
            public List<string> Arguments { get; set; } = new List<string>();
        }
        private sealed class CompletionFile {
            [JsonPropertyName("completedTasks")]
            public List<string> CompletedTasks { get; set; } = new List<string>();
        }
    }
}
