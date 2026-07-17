using System.Security.Cryptography;
using System.Text;

namespace Finara.Infrastructure.Security;

// Refresh token'lar (parolaların aksine) zaten yüksek entropili, rastgele üretilmiş
// 64 byte'lık değerler — brute-force/sözlük riski yok, bu yüzden PBKDF2 yerine tek
// yönlü deterministik SHA-256 yeterli ve sorgu (WHERE Token = @hash) yapılabilmesini
// sağlıyor (PBKDF2'nin rastgele salt'ı bunu engellerdi). DB'de artık çalınabilir düz
// metin token yerine sadece hash tutuluyor.
public static class RefreshTokenHasher
{
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }
}
