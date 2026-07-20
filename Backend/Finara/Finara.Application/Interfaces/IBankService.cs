using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface IBankService
{
    Task<IReadOnlyList<BankSummary>> GetBanksAsync(CancellationToken cancellationToken = default);

    // Sadece SuperAdmin çağırabilir — BankAdmin kendi bankasını değiştiremez/yenisini
    // oluşturamaz. Yetkisiz çağrıda InvalidOperationException fırlatır (→ 400,
    // GlobalExceptionHandler'daki mevcut iş kuralı ihlali eşlemesiyle tutarlı).
    Task<BankSummary> CreateBankAsync(CreateBankRequest request, CancellationToken cancellationToken = default);
}
