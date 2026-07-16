using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

public record CreateCustomerRequest(
    string FirstName,
    string LastName,
    string Email,
    DateTime DateOfBirth,
    bool HasInvestmentAccount = false,
    bool HasInternationalTransfers = false);

public record RecordTransactionRequest(
    decimal Amount,
    TransactionCategory Category,
    string MerchantName,
    DateTime OccurredAtUtc,
    string Currency = "TRY",
    bool IsInternational = false);

public record GenerateMockDataRequest(int CustomerCount, int TransactionsPerCustomer);

public record CustomerSummary(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    int Age,
    SpendingPropensity Propensity);

public record TransactionSummary(
    Guid Id,
    Guid CustomerId,
    decimal Amount,
    TransactionCategory Category,
    string MerchantName,
    DateTime OccurredAtUtc,
    string Currency,
    bool IsInternational);

public record MockGenerationResult(int CustomersCreated, int TransactionsCreated);
