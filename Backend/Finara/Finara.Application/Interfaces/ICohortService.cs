using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface ICohortService
{
    // Aktif segmenti olan (son 90 gün içinde işlemi bulunan) müşteriler üzerinden
    // kohortları hesaplar — bank-scope ICurrentUserContext üzerinden uygulanır.
    Task<IReadOnlyList<CohortSummary>> GetCohortsAsync(CancellationToken cancellationToken = default);
}
