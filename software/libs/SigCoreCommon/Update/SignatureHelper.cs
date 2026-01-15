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
        public static bool VerifyFile(
            string publicKeyPemPath,
            string inputFile,
            string signatureFile) {
            LogFile log = new LogFile();

            log.Info("VerifyFile: starting");
            log.Info($"  inputFile       = '{inputFile}'");
            log.Info($"  signatureFile   = '{signatureFile}'");
            log.Info($"  publicKeyPem    = '{publicKeyPemPath}'");

            if (!File.Exists(inputFile)) {
                log.Error("VerifyFile: input file does not exist");
                return false;
            }

            if (!File.Exists(signatureFile)) {
                log.Error("VerifyFile: signature file does not exist");
                return false;
            }

            if (!File.Exists(publicKeyPemPath)) {
                log.Error("VerifyFile: public key file does not exist");
                return false;
            }

            byte[] data = File.ReadAllBytes(inputFile);
            byte[] signature = File.ReadAllBytes(signatureFile);

            log.Info($"VerifyFile: input size      = {data.Length} bytes");
            log.Info($"VerifyFile: signature size  = {signature.Length} bytes");

            string dataHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(data));

            log.Info($"VerifyFile: input SHA256    = {dataHash}");

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
        public static bool VerifyData(
            byte[] data,
            byte[] signature,
            string publicKeyPemPath) {
            LogFile log = new LogFile();

            log.Info("VerifyData: starting");

            string pem = File.ReadAllText(publicKeyPemPath);
            log.Info($"VerifyData: public key PEM length = {pem.Length} chars");

            RSA rsa = null;

            try {
                rsa = LoadPublicKeyFromPem(pem);

                int keySize = rsa.KeySize;
                log.Info($"VerifyData: RSA key size = {keySize} bits");

                bool ok = rsa.VerifyData(
                    data,
                    signature,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                if (ok) {
                    log.Info("VerifyData: signature verification PASSED");
                } else {
                    log.Error("VerifyData: signature verification FAILED");
                }

                return ok;
            } catch (Exception ex) {
                log.Error("VerifyData: exception during verification", ex);
                return false;
            } finally {
                rsa?.Dispose();
            }
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
