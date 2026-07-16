using System.Security.Cryptography;

namespace Finara.Infrastructure.Security;

// PBKDF2-SHA256, ekstra paket gerektirmeden (Microsoft.AspNetCore.Identity'nin tamamını
// eklemeden) güvenli parola hash'leme. Format: "{salt-base64}.{hash-base64}".
public static class PasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string hashedPassword)
    {
        var parts = hashedPassword.Split('.');
        if (parts.Length != 2)
            return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);

        // Zamanlama saldırılarına karşı sabit-zamanlı karşılaştırma — erken çıkışlı
        // (ilk farklı byte'ta duran) bir karşılaştırma, hash'in ne kadarının doğru
        // olduğunu yanıt süresinden sızdırabilir.
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
