namespace Finara.Domain.Common;

// Faz 8.1 migration'ı bu sabit kimlikle bir Bank satırı seed ediyor ve o ana kadar
// var olan tüm Customer/Transaction/Segment/Campaign/CampaignOffer/AuditLog satırlarını
// buna bağlıyor. SuperAdmin (BankId'si null olan kullanıcılar) yeni kayıt oluştururken de
// — henüz banka seçme arayüzü olmadığı için — bu bankayı kullanır (bkz. ICurrentUserContext).
public static class DefaultBank
{
    public static readonly Guid Id = new("00000000-0000-0000-0000-000000000001");
    public const string Name = "Varsayılan Banka";
    public const string Code = "DEFAULT";
}
