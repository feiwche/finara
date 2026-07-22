using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface ICustomerService
{
    Task<CustomerSummary> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken = default);

    Task<TransactionSummary?> RecordTransactionAsync(Guid customerId, RecordTransactionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionSummary>?> GetTransactionsAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<MockGenerationResult> GenerateMockDataAsync(GenerateMockDataRequest request, CancellationToken cancellationToken = default);

    Task<CustomerImportResult> ImportCustomersAsync(Stream csvStream, CancellationToken cancellationToken = default);

    // KVKK/GDPR unutulma hakkı — müşteri bulunamazsa/başka bankadaysa null (404).
    // Zaten anonimleştirilmişse InvalidOperationException (400) fırlatır.
    Task<CustomerSummary?> EraseCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}
