using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class CustomerService(FinaraDbContext dbContext, IBankDataProvider bankDataProvider, IAuditLogRepository auditLogRepository)
    : ICustomerService
{
    public async Task<CustomerSummary> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var emailInUse = await dbContext.Customers.AnyAsync(c => c.Email == normalizedEmail, cancellationToken);
        if (emailInUse)
            throw new InvalidOperationException($"'{request.Email}' e-posta adresiyle kayıtlı bir müşteri zaten var.");

        var customer = Customer.Create(request.FirstName, request.LastName, request.Email, request.DateOfBirth);
        if (request.HasInvestmentAccount || request.HasInternationalTransfers)
            customer.UpdateFinancialFlags(request.HasInvestmentAccount, request.HasInternationalTransfers);

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogRepository.InsertAsync(
            AuditLog.Create(customer.Id, AuditActionType.CustomerCreated, $"{customer.FirstName} {customer.LastName}"),
            cancellationToken);

        return ToSummary(customer);
    }

    public async Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        var customers = await dbContext.Customers.AsNoTracking().ToListAsync(cancellationToken);
        return customers.Select(ToSummary).ToList();
    }

    public async Task<TransactionSummary?> RecordTransactionAsync(Guid customerId, RecordTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == customerId, cancellationToken);
        if (!customerExists)
            return null;

        var transaction = Transaction.Create(
            customerId, request.Amount, request.Category, request.MerchantName,
            request.OccurredAtUtc, request.Currency, request.IsInternational);

        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogRepository.InsertAsync(
            AuditLog.Create(customerId, AuditActionType.TransactionRecorded, $"{transaction.MerchantName} — {transaction.Amount} {transaction.Currency}"),
            cancellationToken);

        return ToSummary(transaction);
    }

    public async Task<IReadOnlyList<TransactionSummary>?> GetTransactionsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == customerId, cancellationToken);
        if (!customerExists)
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

        var customersCreated = 0;
        var transactionsCreated = 0;

        for (var i = 0; i < request.CustomerCount; i++)
        {
            var data = await bankDataProvider.GenerateCustomerAsync(cancellationToken);
            var customer = Customer.Create(data.FirstName, data.LastName, data.Email, data.DateOfBirth);
            customer.UpdateFinancialFlags(data.HasInvestmentAccount, data.HasInternationalTransfers);
            dbContext.Customers.Add(customer);
            customersCreated++;

            var transactionData = await bankDataProvider.GenerateTransactionsAsync(request.TransactionsPerCustomer, cancellationToken);
            foreach (var t in transactionData)
            {
                dbContext.Transactions.Add(Transaction.Create(
                    customer.Id, t.Amount, t.Category, t.MerchantName, t.OccurredAtUtc, t.Currency, t.IsInternational));
                transactionsCreated++;
            }
        }

        // Bulk üretimde satır başına audit kaydı tutulmuyor — bu bir geliştirme/test aracı,
        // gerçek bir kullanıcı eylemi değil. Manuel oluşturma/harcama uçları ayrı ayrı audit'leniyor.
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MockGenerationResult(customersCreated, transactionsCreated);
    }

    private static CustomerSummary ToSummary(Customer customer) =>
        new(customer.Id, customer.FirstName, customer.LastName, customer.Email, customer.GetAge(), customer.CurrentPropensity);

    private static TransactionSummary ToSummary(Transaction transaction) =>
        new(transaction.Id, transaction.CustomerId, transaction.Amount, transaction.Category,
            transaction.MerchantName, transaction.OccurredAtUtc, transaction.Currency, transaction.IsInternational);
}
