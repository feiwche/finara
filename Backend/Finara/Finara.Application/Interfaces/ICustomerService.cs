using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface ICustomerService
{
    Task<CustomerSummary> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken = default);

    // Banka scope dışındaysa ya da bulunamazsa null (404 deseni). Müşteri detay ekranı
    // önceden TÜM müşteri listesini çekip istemcide filtreliyordu (denetimde bulundu).
    Task<CustomerSummary?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<TransactionSummary?> RecordTransactionAsync(Guid customerId, RecordTransactionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionSummary>?> GetTransactionsAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<MockGenerationResult> GenerateMockDataAsync(GenerateMockDataRequest request, CancellationToken cancellationToken = default);

    Task<CustomerImportResult> ImportCustomersAsync(Stream csvStream, CancellationToken cancellationToken = default);

    Task<PagedResult<CustomerSummary>> GetCustomersPagedAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);

    Task<CustomerStats> GetCustomerStatsAsync(CancellationToken cancellationToken = default);

    // KVKK/GDPR unutulma hakkı — müşteri bulunamazsa/başka bankadaysa null (404).
    // Zaten anonimleştirilmişse InvalidOperationException (400) fırlatır.
    Task<CustomerSummary?> EraseCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}
