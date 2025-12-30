using System.Security.Cryptography;
using System.Text;

namespace Declarify.Helper
{
    public class DataProtection
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("8fG!hJ3kL5mN7pQ9rS2tV4wY6zA8bC0dE1fG3hJ5kL7mN9pQ0rS2tV4wY6zA8bC!dE");

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText),Entropy,DataProtectionScope.LocalMachine);

            return Convert.ToBase64String(encrypted);
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            byte[] decrypted = ProtectedData.Unprotect(Convert.FromBase64String(cipherText),Entropy,DataProtectionScope.LocalMachine);

            return Encoding.UTF8.GetString(decrypted);
        }
    }
}
