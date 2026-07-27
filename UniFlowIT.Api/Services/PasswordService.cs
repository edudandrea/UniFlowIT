using System.Security.Cryptography;

namespace UniFlowIT.Api.Services
{
    public static class PasswordService
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100_000;

        public static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

            return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        public static bool IsStrong(string? password)
        {
            return !string.IsNullOrWhiteSpace(password)
                && password.Length >= 8
                && password.Any(char.IsUpper)
                && password.Any(char.IsDigit)
                && password.Any(character => char.IsLetter(character))
                && password.Any(character => !char.IsLetterOrDigit(character));
        }

        public static bool Verify(string password, string hash)
        {
            var parts = hash.Split('$');
            if (parts.Length != 4 || parts[0] != "PBKDF2" || !int.TryParse(parts[1], out var iterations))
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expectedKey = Convert.FromBase64String(parts[3]);
            var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);

            return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
        }
    }
}
