using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace WebApplication1.Services
{
    public class EbookAccessService
    {
        private readonly ConcurrentDictionary<string, DateTime> _tokens = new();

        public string CreateToken()
        {
            var token = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(32)
            )
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

            _tokens[token] = DateTime.UtcNow.AddMinutes(15);

            return token;
        }

        public bool ValidateAndConsumeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            if (!_tokens.TryGetValue(token, out var expiry))
                return false;

            if (DateTime.UtcNow > expiry)
            {
                _tokens.TryRemove(token, out _);
                return false;
            }

            // Token can be used only once
            _tokens.TryRemove(token, out _);

            return true;
        }
    }
}