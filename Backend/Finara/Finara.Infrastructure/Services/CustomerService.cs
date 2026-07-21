using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class CustomerService(
    FinaraDbContext dbContext,
    IBankDataProviderFactory bankDataProviderFactory,
    IAuditLogRepository auditLogRepository,
    ICurrentUserContext currentUser)
    : ICustomerService
{
    public async Task<CustomerSummary> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var bankId = currentUser.ResolveBankIdForWrite();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        // E-posta benzersizliği banka bazlı — iki farklı banka aynı gerçek kişiyi
        // (aynı e-posta) kendi müşterisi olarak ayrı ayrı kaydedebilir (bkz. CustomerConfiguration'daki
        // (BankId, Email) bileşik unique index).
        var emailInUse = await dbContext.Customers.AnyAsync(c => c.BankId == bankId && c.Email == normalizedEmail, cancellationToken);
        if (emailInUse)
            throw new InvalidOperationException($"'{request.Email}' e-posta adresiyle kayıtlı bir müşteri zaten var.");

        var customer = Customer.Create(bankId, request.FirstName, request.LastName, request.Email, request.DateOfBirth);
        if (request.HasInvestmentAccount || request.HasInternationalTransfers)
            customer.UpdateFinancialFlags(request.HasInvestmentAccount, request.HasInternationalTransfers);
        if (request.EducationLevel is not null)
            customer.UpdateEducationLevel(request.EducationLevel);

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogRepository.InsertAsync(
            AuditLog.Create(bankId, customer.Id, AuditActionType.CustomerCreated, $"{customer.FirstName} {customer.LastName}"),
            cancellationToken);

        return ToSummary(customer);
    }

    public async Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        var query = dbContext.Customers.AsNoTracking().AsQueryable();
        if (!currentUser.IsSuperAdmin)
            query = query.Where(c => c.BankId == currentUser.BankId);

        var customers = await query.ToListAsync(cancellationToken);
        return customers.Select(ToSummary).ToList();
    }

    public async Task<TransactionSummary?> RecordTransactionAsync(Guid customerId, RecordTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        // Başka bankanın müşterisine erişim denemesi de "bulunamadı" gibi ele alınıyor —
        // 403 yerine 404 vermek, o customerId'nin başka bir bankada var olduğunu sızdırmaz.
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
            return null;

        var transaction = Transaction.Create(
            customer.BankId, customerId, request.Amount, request.Category, request.MerchantName,
            request.OccurredAtUtc, request.Currency, request.IsInternational);

        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogRepository.InsertAsync(
            AuditLog.Create(customer.BankId, customerId, AuditActionType.TransactionRecorded, $"{transaction.MerchantName} — {transaction.Amount} {transaction.Currency}"),
            cancellationToken);

        return ToSummary(transaction);
    }

    public async Task<IReadOnlyList<TransactionSummary>?> GetTransactionsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
            return null;

        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        return transactions.Select(ToSummary).ToList();
    }

    public async Task<MockGenerationResult> GenerateMockDataAsync(GenerateMockDataRequest request, CancellationToken cancellationToken = default)
    {
        if (request.CustomerCount <= 0 || request.TransactionsPerCustomer < 0)
            throw new ArgumentException("customerCount pozitif, transactionsPerCustomer negatif olmayan bir sayı olmalı.");

        var bankId = currentUser.ResolveBankIdForWrite();
        var bankDataProvider = await bankDataProviderFactory.GetProviderAsync(bankId, cancellationToken);
        var customersCreated = 0;
        var transactionsCreated = 0;

        for (var i = 0; i < request.CustomerCount; i++)
        {
            var data = await bankDataProvider.GenerateCustomerAsync(cancellationToken);
            var customer = Customer.Create(bankId, data.FirstName, data.LastName, data.Email, data.DateOfBirth);
            customer.UpdateFinancialFlags(data.HasInvestmentAccount, data.HasInternationalTransfers);
            customer.UpdateEducationLevel(data.EducationLevel);
            dbContext.Customers.Add(customer);
            customersCreated++;

            var transactionData = await bankDataProvider.GenerateTransactionsAsync(request.TransactionsPerCustomer, cancellationToken);
            foreach (var t in transactionData)
            {
                dbContext.Transactions.Add(Transaction.Create(
                    bankId, customer.Id, t.Amount, t.Category, t.MerchantName, t.OccurredAtUtc, t.Currency, t.IsInternational));
                transactionsCreated++;
            }

            foreach (var (channel, isGranted) in data.Consents)
                dbContext.CustomerConsents.Add(CustomerConsent.Create(bankId, customer.Id, channel, isGranted));
        }

        // Bulk üretimde satır başına audit kaydı tutulmuyor — bu bir geliştirme/test aracı,
        // gerçek bir kullanıcı eylemi değil. Manuel oluşturma/harcama uçları ayrı ayrı audit'leniyor.
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MockGenerationResult(customersCreated, transactionsCreated);
    }

    private static CustomerSummary ToSummary(Customer customer) =>
        new(customer.Id, customer.FirstName, customer.LastName, customer.Email, customer.GetAge(), customer.CurrentPropensity,
            customer.HasInvestmentAccount, customer.HasInternationalTransfers, customer.EducationLevel);

    private static TransactionSummary ToSummary(Transaction transaction) =>
        new(transaction.Id, transaction.CustomerId, transaction.Amount, transaction.Category,
            transaction.MerchantName, transaction.OccurredAtUtc, transaction.Currency, transaction.IsInternational);
}
