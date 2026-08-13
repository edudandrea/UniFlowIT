using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniFlowIT.Api.Models;

namespace UniFlowIT.Api.Services
{
    public sealed record AuthSession(int UserId, int? EmpresaId, string Nome, string Email, string Role, long ExpiresAt);

    public sealed class AuthTokenService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly byte[] signingKey = ResolveSigningKey(configuration, environment);

        public TimeSpan TokenLifetime { get; } = TimeSpan.FromHours(8);

        public string Create(Users usuario)
        {
            var session = new AuthSession(
                usuario.Id,
                usuario.EmpresaId,
                usuario.Nome,
                usuario.Email,
                usuario.Role,
                DateTimeOffset.UtcNow.Add(TokenLifetime).ToUnixTimeSeconds());

            var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(session, JsonOptions));
            var signature = Sign(payload);
            return $"{payload}.{signature}";
        }

        public bool TryValidate(string? token, out AuthSession session)
        {
            session = default!;

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var parts = token.Split('.', 2);
            if (parts.Length != 2 || !FixedTimeEquals(Sign(parts[0]), parts[1]))
            {
                return false;
            }

            try
            {
                session = JsonSerializer.Deserialize<AuthSession>(Base64UrlDecode(parts[0]), JsonOptions)!;
            }
            catch
            {
                return false;
            }

            return session.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private string Sign(string payload)
        {
            using var hmac = new HMACSHA256(signingKey);
            return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left);
            var rightBytes = Encoding.UTF8.GetBytes(right);
            return leftBytes.Length == rightBytes.Length
                && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }

        private static byte[] ResolveSigningKey(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var configured = configuration["Security:TokenSigningKey"];
            if (!string.IsNullOrWhiteSpace(configured) && Encoding.UTF8.GetByteCount(configured) >= 32)
            {
                return Encoding.UTF8.GetBytes(configured);
            }

            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("Configure Security:TokenSigningKey com no minimo 32 caracteres.");
            }

            return SHA256.HashData(Encoding.UTF8.GetBytes($"{environment.ApplicationName}:{environment.EnvironmentName}:dev-only-key"));
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static byte[] Base64UrlDecode(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            return Convert.FromBase64String(padded);
        }
    }
}
