namespace Finara.Domain.Common;

// Customer ve User entity'lerinde birebir aynı sekilde duplike edilmis e-posta dogrulama
// mantigi buraya tasindi (denetimde bulundu) — MailAddress'in kendi ayristiricisini
// kullanmak, elle yazilmis bir regex'ten daha guvenilir (RFC 5322'nin tuhaf ama gecerli
// kose durumlarini da dogru degerlendirir).
public static class EmailValidator
{
    public static bool IsValid(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }
}
