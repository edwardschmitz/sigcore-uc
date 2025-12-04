using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SigCoreCommon.Update {
    public static class FingerprintHelper {

        // Compute SHA256 fingerprint of the RSA public key (DER form)
        public static string Compute(string publicKeyPemPath) {
            string pem = File.ReadAllText(publicKeyPemPath);

            RSA rsa = RSA.Create();
            rsa.ImportFromPem(pem.ToCharArray());

            // Export DER-encoded public key
            byte[] der = rsa.ExportSubjectPublicKeyInfo();

            // Hash DER
            using (SHA256 sha = SHA256.Create()) {
                byte[] hash = sha.ComputeHash(der);

                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) {
                    sb.Append(hash[i].ToString("x2"));
                }

                Console.WriteLine($"PEM: Path: {publicKeyPemPath}, Finger Print: {sb}");
                return sb.ToString();
            }
        }
    }
}
