using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface IBankService
{
    Task<IReadOnlyList<BankSummary>> GetBanksAsync(CancellationToken cancellationToken = default);

    // GetBanksAsync'in aksine SuperAdmin'e kilitli DEĞİL — BankAdmin/Analyst de kendi
    // bankasının adını/kodunu görebilmeli (şu ana kadar bunu ancak haftalık özet
    // e-postasının konu satırından dolaylı olarak öğrenebiliyorlardı). SuperAdmin'in
    // BankId'si null olduğu için bu uçtan her zaman null döner — onun zaten tam
    // Banka Yönetimi ekranı var.
    Task<BankSummary?> GetMyBankAsync(CancellationToken cancellationToken = default);

    // Sadece SuperAdmin çağırabilir — BankAdmin kendi bankasını değiştiremez/yenisini
    // oluşturamaz. Yetkisiz çağrıda InvalidOperationException fırlatır (→ 400,
    // GlobalExceptionHandler'daki mevcut iş kuralı ihlali eşlemesiyle tutarlı).
    Task<BankSummary> CreateBankAsync(CreateBankRequest request, CancellationToken cancellationToken = default);

    // Sadece SuperAdmin çağırabilir (aynı yetki kuralı). Banka bulunamazsa null
    // (endpoint bunu 404'e çevirir) — AdminUserService.DeactivateAdminAsync ile aynı desen.
    Task<BankSummary?> DeactivateBankAsync(Guid bankId, CancellationToken cancellationToken = default);
}
