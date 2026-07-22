using System.Globalization;
using System.Text;
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
        if (request.PhoneNumber is not null)
            customer.UpdatePhoneNumber(request.PhoneNumber);

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

        if (customer.IsErased)
            throw new InvalidOperationException("Bu müşteri KVKK unutulma hakkı kapsamında anonimleştirilmiş, yeni işlem eklenemez.");

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
            customer.UpdatePhoneNumber(data.PhoneNumber);
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

    public async Task<CustomerSummary?> EraseCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
            return null;

        customer.Erase();

        // Artık ulaşılamayan/anonim bir kişiye "rıza" kavramsal olarak anlamsız —
        // kalan tüm kanal rızaları da kaldırılıyor ki teklif dağıtımı consent-gate
        // üzerinden zaten sessizce atlansın (bkz. IConsentGate — ayrı bir guard eklemeye gerek kalmadı).
        var activeConsents = await dbContext.CustomerConsents
            .Where(c => c.CustomerId == customerId && c.IsGranted)
            .ToListAsync(cancellationToken);
        foreach (var consent in activeConsents)
            consent.Revoke();

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogRepository.InsertAsync(
            AuditLog.Create(customer.BankId, customer.Id, AuditActionType.CustomerErased,
                "Müşteri verileri KVKK unutulma hakkı kapsamında anonimleştirildi."),
            cancellationToken);

        return ToSummary(customer);
    }

    // Beklenen CSV başlıkları (Türkçe, sıra önemsiz — NormalizeHeader ile eşleştiriliyor):
    // Ad, Soyad, E-posta, Doğum Tarihi zorunlu; Telefon, Eğitim Durumu, Yatırım Hesabı Var,
    // Yurtdışı Transfer Var opsiyonel.
    public async Task<CustomerImportResult> ImportCustomersAsync(Stream csvStream, CancellationToken cancellationToken = default)
    {
        var bankId = currentUser.ResolveBankIdForWrite();
        using var reader = new StreamReader(csvStream);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
            throw new ArgumentException("CSV dosyası boş.");

        var headers = ParseCsvLine(headerLine).Select(NormalizeHeader).ToList();
        var columnIndex = new Dictionary<string, int>();
        for (var i = 0; i < headers.Count; i++)
            columnIndex[headers[i]] = i;

        var requiredColumns = new[] { "AD", "SOYAD", "EPOSTA", "DOGUMTARIHI" };
        var missing = requiredColumns.Where(c => !columnIndex.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            throw new ArgumentException(
                $"CSV başlığında zorunlu sütun(lar) eksik: {string.Join(", ", missing)}. " +
                "Beklenen sütunlar: Ad, Soyad, E-posta, Doğum Tarihi, Telefon, Eğitim Durumu, Yatırım Hesabı Var, Yurtdışı Transfer Var.");

        var existingEmails = (await dbContext.Customers
            .Where(c => c.BankId == bankId)
            .Select(c => c.Email)
            .ToListAsync(cancellationToken)).ToHashSet();
        var seenInFile = new HashSet<string>();
        var errors = new List<CustomerImportRowError>();
        var imported = new List<Customer>();

        var rowNumber = 1; // 1 = başlık satırı
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            string Get(string column) =>
                columnIndex.TryGetValue(column, out var idx) && idx < fields.Count ? fields[idx].Trim() : string.Empty;

            try
            {
                var email = Get("EPOSTA");
                var normalizedEmail = email.Trim().ToLowerInvariant();
                if (existingEmails.Contains(normalizedEmail) || !seenInFile.Add(normalizedEmail))
                    throw new InvalidOperationException($"'{email}' e-posta adresiyle kayıtlı bir müşteri zaten var.");

                var dobRaw = Get("DOGUMTARIHI");
                if (!DateTime.TryParse(dobRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
                    throw new ArgumentException($"Geçersiz doğum tarihi formatı: '{dobRaw}' (beklenen: yyyy-aa-gg).");

                var customer = Customer.Create(bankId, Get("AD"), Get("SOYAD"), email, dob);

                var hasInvestment = ParseBool(Get("YATIRIMHESABIVAR"));
                var hasInternational = ParseBool(Get("YURTDISITRANSFERVAR"));
                if (hasInvestment || hasInternational)
                    customer.UpdateFinancialFlags(hasInvestment, hasInternational);

                var educationRaw = Get("EGITIMDURUMU");
                if (!string.IsNullOrWhiteSpace(educationRaw))
                {
                    var educationLevel = ParseEducationLevel(educationRaw)
                        ?? throw new ArgumentException($"Geçersiz eğitim durumu: '{educationRaw}'.");
                    customer.UpdateEducationLevel(educationLevel);
                }

                var phone = Get("TELEFON");
                if (!string.IsNullOrWhiteSpace(phone))
                    customer.UpdatePhoneNumber(phone);

                imported.Add(customer);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
            {
                errors.Add(new CustomerImportRowError(rowNumber, ex.Message));
            }
        }

        if (imported.Count > 0)
        {
            dbContext.Customers.AddRange(imported);
            await dbContext.SaveChangesAsync(cancellationToken);

            await auditLogRepository.InsertAsync(
                AuditLog.Create(bankId, null, AuditActionType.CustomerBulkImported,
                    $"{imported.Count} müşteri CSV ile içe aktarıldı ({errors.Count} satır hatalı)."),
                cancellationToken);
        }

        return new CustomerImportResult(imported.Count + errors.Count, imported.Count, errors);
    }

    private static bool ParseBool(string value) =>
        value.Trim().ToLowerInvariant() is "evet" or "var" or "true" or "1";

    private static EducationLevel? ParseEducationLevel(string value)
    {
        var normalized = NormalizeHeader(value);
        foreach (var level in Enum.GetValues<EducationLevel>())
        {
            if (NormalizeHeader(level.ToString()) == normalized)
                return level;
        }
        return null;
    }

    // Türkçe karakterleri (İ/ı/ş/ğ/ü/ö/ç) ASCII karşılıklarına indirip büyük harfe çevirir,
    // harf/rakam dışındaki her şeyi (boşluk, '-') atar — CSV başlıkları ve eğitim durumu
    // değerleri kullanıcı elle girdiği için (aksanlı/aksansız, boşluklu/boşluksuz) esnek eşleşmeli.
    private static string NormalizeHeader(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value.Trim())
        {
            var mapped = c switch
            {
                'ı' or 'İ' or 'i' or 'I' => 'I',
                'ş' or 'Ş' => 'S',
                'ğ' or 'Ğ' => 'G',
                'ü' or 'Ü' => 'U',
                'ö' or 'Ö' => 'O',
                'ç' or 'Ç' => 'C',
                _ => char.ToUpperInvariant(c)
            };
            if (char.IsLetterOrDigit(mapped))
                sb.Append(mapped);
        }
        return sb.ToString();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }

    private static CustomerSummary ToSummary(Customer customer) =>
        new(customer.Id, customer.FirstName, customer.LastName, customer.Email, customer.GetAge(), customer.CurrentPropensity,
            customer.HasInvestmentAccount, customer.HasInternationalTransfers, customer.EducationLevel, customer.PhoneNumber,
            customer.IsErased);

    private static TransactionSummary ToSummary(Transaction transaction) =>
        new(transaction.Id, transaction.CustomerId, transaction.Amount, transaction.Category,
            transaction.MerchantName, transaction.OccurredAtUtc, transaction.Currency, transaction.IsInternational);
}
