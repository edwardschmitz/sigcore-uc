using System;
using System.Diagnostics;

namespace SigCoreLauncher {
    public static class ServiceController {
        // =====================================================================
        // Stop a systemd service
        // =====================================================================
        public static bool StopService(string serviceName) {
            int code = Run("systemctl stop " + serviceName);

            if (code != 0)
                return false;

            // confirm service is inactive
            return IsInactive(serviceName);
        }

        // =====================================================================
        // Start a systemd service
        // =====================================================================
        public static bool StartService(string serviceName) {
            int code = Run("systemctl start " + serviceName);

            if (code != 0)
                return false;

            return IsActive(serviceName);
        }

        // =====================================================================
        // Check if service is active
        // =====================================================================
        public static bool IsActive(string serviceName) {
            int code = Run("systemctl is-active --quiet " + serviceName);
            return code == 0;
        }

        // =====================================================================
        // Check if service is inactive
        // =====================================================================
        public static bool IsInactive(string serviceName) {
            int code = Run("systemctl is-active --quiet " + serviceName);
            return code != 0;
        }

        // =====================================================================
        // Run shell command and return exit code
        // =====================================================================
        private static int Run(string cmd) {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "/bin/bash";
            psi.Arguments = "-c \"" + cmd + "\"";
            psi.RedirectStandardOutput = false;
            psi.RedirectStandardError = false;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            Process p = Process.Start(psi);
            p.WaitForExit();
            return p.ExitCode;
        }
    }
}
