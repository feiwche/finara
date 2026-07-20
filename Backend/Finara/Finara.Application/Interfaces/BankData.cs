using Finara.Domain.Enums;

namespace Finara.Application.Interfaces;

// IBankDataProvider'ın döndürdüğü ham veri şekli — domain entity değil.
// Domain entity'ye dönüşüm (ve iş kuralı validasyonu) her zaman Customer.Create()/
// Transaction.Create() üzerinden, bu veriyi tüketen Application servisinde yapılır.
public record CustomerData(
    string FirstName,
    string LastName,
    string Email,
    DateTime DateOfBirth,
    bool HasInvestmentAccount,
    bool HasInternationalTransfers,
    EducationLevel EducationLevel);

public record TransactionData(
    decimal Amount,
    TransactionCategory Category,
    string MerchantName,
    DateTime OccurredAtUtc,
    string Currency,
    bool IsInternational);
