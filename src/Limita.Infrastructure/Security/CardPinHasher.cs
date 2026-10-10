
using System.Globalization;
using System.Security.Cryptography;
using Limita.Application.Common.Interfaces;

namespace Limita.Infrastructure.Security;

/// <summary>
/// Hashes card PINs using PBKDF2-SHA256.
/// Plaintext PINs are never stored.
/// </summary>
public sealed class CardPinHasher : ICardPinHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;

    // Version 1 hashing configuration.
    private const int Iterations = 600_000;

    // Prevent invalid or manipulated hashes from consuming excessive CPU.
    private const int MinAllowedIterations = 100_000;
    private const int MaxAllowedIterations = 1_000_000;

    public string Hash(string pin)
    {
        ValidatePin(pin);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            pin,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"v1.{Iterations}." +
               $"{Convert.ToBase64String(salt)}." +
               $"{Convert.ToBase64String(key)}";
    }

    public bool Verify(string pin, string hash)
    {
        if (string.IsNullOrEmpty(pin) ||
            string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        string[] parts = hash.Split('.');

        if (parts.Length != 4 || parts[0] != "v1")
        {
            return false;
        }

        if (!int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int iterations) ||
            iterations < MinAllowedIterations ||
            iterations > MaxAllowedIterations)
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);

            if (salt.Length != SaltSize ||
                expected.Length != KeySize)
            {
                return false;
            }

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                pin,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                KeySize);

            return CryptographicOperations.FixedTimeEquals(
                actual,
                expected);
        }
        catch (FormatException)
        {
            // The stored hash contains invalid Base64 data.
            return false;
        }
    }

    private static void ValidatePin(string pin)
    {
        if (string.IsNullOrEmpty(pin))
        {
            throw new ArgumentException(
                "PIN cannot be null or empty.",
                nameof(pin));
        }
    }
}