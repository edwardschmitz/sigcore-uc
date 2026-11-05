using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace SigCoreCommon.Update
{
    public class ReleaseStateStore
    {
        public const string DefaultStatePath = "/opt/sigcore/releases.json";
        public const string DefaultLockPath = "/opt/sigcore/releases.lock";

        private readonly string statePath;
        private readonly string lockPath;

        public ReleaseStateStore(
            string statePath = DefaultStatePath,
            string lockPath = DefaultLockPath)
        {
            this.statePath = statePath;
            this.lockPath = lockPath;
        }

        public ReleaseState Load()
        {
            using FileStream lockStream = AcquireLock();
            return LoadUnlocked();
        }

        public ReleaseEntry? FindVersion(string version)
        {
            using FileStream lockStream = AcquireLock();

            ReleaseState state = LoadUnlocked();

            return state.Versions.FirstOrDefault(v =>
                string.Equals(
                    v.Version,
                    version,
                    StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<ReleaseEntry> GetVersions()
        {
            using FileStream lockStream = AcquireLock();

            ReleaseState state = LoadUnlocked();

            return state.Versions
                .OrderBy(v => ParseVersion(v.Version))
                .ToList();
        }

        public void AddVersion(
            string version,
            ReleaseDisposition disposition)
        {
            _ = ParseVersion(version);

            using FileStream lockStream = AcquireLock();

            ReleaseState state = LoadUnlocked();

            if (state.Versions.Any(v =>
                string.Equals(
                    v.Version,
                    version,
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Release {version} already exists.");
            }

            state.Versions.Add(new ReleaseEntry
            {
                Version = version,
                Disposition = disposition,
                CreatedUtc = DateTime.UtcNow
            });

            SaveUnlocked(state);
        }

        public void SetDisposition(
            string version,
            ReleaseDisposition disposition,
            string? reason = null)
        {
            using FileStream lockStream = AcquireLock();

            ReleaseState state = LoadUnlocked();

            ReleaseEntry? entry = state.Versions.FirstOrDefault(v =>
                string.Equals(
                    v.Version,
                    version,
                    StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                throw new InvalidOperationException(
                    $"Release {version} is not installed.");
            }

            entry.Disposition = disposition;
            entry.Reason = reason;

            SaveUnlocked(state);
        }


        private ReleaseState LoadUnlocked()
        {
            if (!File.Exists(statePath))
            {
                return new ReleaseState();
            }

            string json = File.ReadAllText(statePath);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new ReleaseState();
            }

            return JsonConvert.DeserializeObject<ReleaseState>(json)
                ?? new ReleaseState();
        }

        private void SaveUnlocked(ReleaseState state)
        {
            string? directory = Path.GetDirectoryName(statePath);

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(
                    $"Invalid release state path: {statePath}");
            }

            Directory.CreateDirectory(directory);

            string tempPath =
                statePath + ".tmp." + Guid.NewGuid().ToString("N");

            string json = JsonConvert.SerializeObject(
                state,
                Formatting.Indented);

            try
            {
                using (FileStream stream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                File.Move(tempPath, statePath, true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private FileStream AcquireLock()
        {
            string? directory = Path.GetDirectoryName(lockPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            while (true)
            {
                try
                {
                    return new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
            }
        }

        private static Version ParseVersion(string version)
        {
            if (!Version.TryParse(version, out Version? parsed))
            {
                throw new InvalidOperationException(
                    $"Invalid release version '{version}'.");
            }

            return parsed;
        }
    }
}
