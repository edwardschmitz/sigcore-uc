using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SigCoreCommon.Update {
    public static class HashHelper {
        // =====================================================================
        // Computes SHA256 of a file and returns lowercase hex string
        // =====================================================================
        public static string Sha256File(string path) {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (SHA256 sha = SHA256.Create()) {
                byte[] hash = sha.ComputeHash(fs);
                return BytesToHex(hash);
            }
        }

        // =====================================================================
        // Computes SHA256 of a byte array and returns lowercase hex string
        // =====================================================================
        public static string Sha256Bytes(byte[] data) {
            using (SHA256 sha = SHA256.Create()) {
                byte[] hash = sha.ComputeHash(data);
                return BytesToHex(hash);
            }
        }

        // =====================================================================
        // Converts byte[] to lowercase hex
        // =====================================================================
        private static string BytesToHex(byte[] bytes) {
            StringBuilder sb = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++) {
                sb.Append(bytes[i].ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
