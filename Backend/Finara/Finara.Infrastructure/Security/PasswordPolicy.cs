namespace Finara.Infrastructure.Security;

// AuthService.RegisterAsync (public self-registration) ve AdminUserService.CreateAdminAsync
// (BackOffice'ten yönetici hesabı oluşturma) aynı şifre kuralını paylaşıyor.
public static class PasswordPolicy
{
    public static void Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Şifre en az 8 karakter olmalı.", nameof(password));

        if (!password.Any(char.IsDigit) || !password.Any(char.IsLetter))
            throw new ArgumentException("Şifre en az bir harf ve bir rakam içermeli.", nameof(password));
    }
}
