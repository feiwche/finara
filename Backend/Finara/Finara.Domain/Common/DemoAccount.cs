namespace Finara.Domain.Common;

// POST /api/auth/demo-login tarafından kullanılan, herkese açık, salt-okunur, paylaşılan
// Analyst hesabı. DefaultBank.Id'ye bağlı (izole/yeni bir "demo banka" YOK — bilinçli
// tercih, mevcut biriken mock veriyi kullanır). Şifre hash'i gerçek biçimli ama hiçbir
// yerde kullanılmıyor çünkü demo-login akışı PasswordHasher.Verify'ı tamamen atlıyor.
public static class DemoAccount
{
    public static readonly Guid Id = new("00000000-0000-0000-0000-000000000002");
    public const string Email = "demo@finara.dev";
}
