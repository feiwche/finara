using Finara.Domain.Common;
using Finara.Domain.Enums;

namespace Finara.Domain.Entities;

public class Transaction : BaseEntity
{
    public Guid BankId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Customer? Customer { get; private set; }

    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public TransactionCategory Category { get; private set; }
    public string MerchantName { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public bool IsInternational { get; private set; }

    private Transaction() { }

    public static Transaction Create(
        Guid bankId,
        Guid customerId,
        decimal amount,
        TransactionCategory category,
        string merchantName,
        DateTime occurredAtUtc,
        string currency = "TRY",
        bool isInternational = false)
    {
        if (bankId == Guid.Empty)
            throw new ArgumentException("Geçerli bir banka kimliği gerekli.", nameof(bankId));

        if (customerId == Guid.Empty)
            throw new ArgumentException("Geçerli bir müşteri kimliği gerekli.", nameof(customerId));

        if (amount <= 0)
            throw new ArgumentException("İşlem tutarı sıfırdan büyük olmalı.", nameof(amount));

        if (string.IsNullOrWhiteSpace(merchantName))
            throw new ArgumentException("İşyeri adı boş olamaz.", nameof(merchantName));

        if (occurredAtUtc > DateTime.UtcNow)
            throw new ArgumentException("İşlem tarihi gelecekte olamaz.", nameof(occurredAtUtc));

        return new Transaction
        {
            BankId = bankId,
            CustomerId = customerId,
            Amount = amount,
            Currency = currency,
            Category = category,
            MerchantName = merchantName.Trim(),
            OccurredAtUtc = occurredAtUtc,
            IsInternational = isInternational
        };
    }
}
