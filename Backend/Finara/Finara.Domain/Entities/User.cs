using Finara.Domain.Common;
using Finara.Domain.Enums;

namespace Finara.Domain.Entities;

// API'yi dışarıya karşı korumak için basit bir admin hesabı — bankacılık müşteri
// kimliği (Customer) ile bilerek karıştırılmadı, ayrı bir kavram.
public class User : BaseEntity
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }

    private User() { }

    public static User Create(string email, string passwordHash, UserRole role = UserRole.Admin)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("E-posta boş olamaz.", nameof(email));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Şifre hash'i boş olamaz.", nameof(passwordHash));

        return new User
        {
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = role
        };
    }
}
