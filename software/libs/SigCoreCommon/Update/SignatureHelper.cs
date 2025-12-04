using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SigCoreCommon.Update {
    public static class SignatureHelper {
        // =====================================================================
        // SIGN A FILE → output signature bytes
        // =====================================================================
        public static void SignFile(string privateKeyPemPath, string inputFile, string signatureOutputFile) {
            byte[] data = File.ReadAllBytes(inputFile);
            byte[] signature = SignData(data, privateKeyPemPath);
            File.WriteAllBytes(signatureOutputFile, signature);
        }

        // =====================================================================
        // VERIFY A FILE with signature
        // =====================================================================
        public static bool VerifyFile(string publicKeyPemPath, string inputFile, string signatureFile) {
            byte[] data = File.ReadAllBytes(inputFile);
            byte[] signature = File.ReadAllBytes(signatureFile);
            return VerifyData(data, signature, publicKeyPemPath);
        }

        // =====================================================================
        // SIGNING LOGIC
        // =====================================================================
        public static byte[] SignData(byte[] data, string privateKeyPemPath) {
            string pem = File.ReadAllText(privateKeyPemPath);
            RSA rsa = LoadPrivateKeyFromPem(pem);

            byte[] signature = rsa.SignData(
                data,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            rsa.Dispose();
            return signature;
        }

        // =====================================================================
        // VERIFICATION LOGIC
        // =====================================================================
        public static bool VerifyData(byte[] data, byte[] signature, string publicKeyPemPath) {
            string pem = File.ReadAllText(publicKeyPemPath);
            RSA rsa = LoadPublicKeyFromPem(pem);

            bool ok = rsa.VerifyData(
                data,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            rsa.Dispose();
            return ok;
        }

        // =====================================================================
        // LOAD PRIVATE KEY FROM PEM
        // =====================================================================
        private static RSA LoadPrivateKeyFromPem(string pem) {
            RSA rsa = RSA.Create();

            // Detect encrypted PEM
            if (pem.Contains("ENCRYPTED")) {
                // TODO: you can change this password string whenever you want.
                string password = "EddSigCore-3.c";  // or whatever your actual PEM passphrase is

                rsa.ImportFromEncryptedPem(pem.ToCharArray(), password);
            } else {
                rsa.ImportFromPem(pem.ToCharArray());
            }

            return rsa;
        }

        // =====================================================================
        // LOAD PUBLIC KEY FROM PEM
        // =====================================================================
        private static RSA LoadPublicKeyFromPem(string pem) {
            RSA rsa = RSA.Create();
            rsa.ImportFromPem(pem.ToCharArray());
            return rsa;
        }
    }
}
