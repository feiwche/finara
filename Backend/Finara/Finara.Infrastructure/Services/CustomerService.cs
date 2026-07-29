using System.Globalization;
using System.Text;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Common;
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
            throw new DomainException(
                ErrorCodes.CustomerEmailAlreadyExists,
                $"'{request.Email}' e-posta adresiyle kayıtlı bir müşteri zaten var.",
                parameters: new Dictionary<string, object?> { ["email"] = request.Email });

        var customer = Customer.Create(bankId, request.FirstName, request.LastName, request.Email, request.DateOfBirth, request.PreferredLanguage ?? UiLanguage.Turkish);
        if (request.HasInvestmentAccount || request.HasInternationalTransfers)
            customer.UpdateFinancialFlags(request.HasInvestmentAccount, request.HasInternationalTransfers);
        if (request.EducationLevel is not null)
            customer.UpdateEducationLevel(request.EducationLevel);
        if (request.PhoneNumber is not null)
            customer.UpdatePhoneNumber(request.PhoneNumber);

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogRepository.InsertAsync(
            AuditLog.Create(bankId, customer.Id, AuditActionType.CustomerCreated, $"{customer.FirstName} {customer.LastName}", currentUser.UserId),
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

    public async Task<PagedResult<CustomerSummary>> GetCustomersPagedAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Customers.AsNoTracking().AsQueryable();
        if (!currentUser.IsSuperAdmin)
            query = query.Where(c => c.BankId == currentUser.BankId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.Like(c.FirstName, pattern) ||
                EF.Functions.Like(c.LastName, pattern) ||
                EF.Functions.Like(c.Email, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Sayfalama SQL Server'da OFFSET/FETCH'e çevrildiği için deterministik bir sıralama
        // şart — sıralama olmadan aynı filtre iki farklı sayfa isteğinde farklı satır
        // sırası döndürebilir (bazı sayfalarda tekrar/atlama). LastName+FirstName aynı olan
        // iki müşteri arasında da bir "berabere kalma" durumu yaşanabildiği için son bir
        // Id tie-break'i ekleniyor.
        var customers = await query
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerSummary>(customers.Select(ToSummary).ToList(), totalCount, page, pageSize);
    }

    public async Task<CustomerStats> GetCustomerStatsAsync(CancellationToken cancellationToken = default)
    {
        // Anonimleştirilmiş (IsErased) müşteriler istatistiklere dahil edilmiyor — bir KVKK
        // unutulma talebinden sonra "Harcamaya Yatkın" gibi davranışsal bir sayaca katkı
        // vermeye devam etmesi, unutulma niyetiyle çelişirdi (bkz. Customer.Erase, SegmentationService).
        var query = dbContext.Customers.AsNoTracking().Where(c => !c.IsErased).AsQueryable();
        if (!currentUser.IsSuperAdmin)
            query = query.Where(c => c.BankId == currentUser.BankId);

        var total = await query.CountAsync(cancellationToken);
        var highlyResponsive = await query.CountAsync(c => c.CurrentPropensity == SpendingPropensity.HighlyResponsive, cancellationToken);
        var needsEngagement = await query.CountAsync(c => c.CurrentPropensity == SpendingPropensity.NeedsEngagement, cancellationToken);

        return new CustomerStats(total, highlyResponsive, needsEngagement);
    }

    public async Task<TransactionSummary?> RecordTransactionAsync(Guid customerId, RecordTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        // Başka bankanın müşterisine erişim denemesi de "bulunamadı" gibi ele alınıyor —
        // 403 yerine 404 vermek, o customerId'nin başka bir bankada var olduğunu sızdırmaz.
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
            return null;

        if (customer.IsErased)
            throw new DomainException(ErrorCodes.CustomerAlreadyErased, "Bu müşteri KVKK unutulma hakkı kapsamında anonimleştirilmiş, yeni işlem eklenemez.");

        var transaction = Transaction.Create(
            customer.BankId, customerId, request.Amount, request.Category, request.MerchantName,
            request.OccurredAtUtc, request.Currency, request.IsInternational);

        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogRepository.InsertAsync(
            AuditLog.Create(customer.BankId, customerId, AuditActionType.TransactionRecorded, $"{transaction.MerchantName} — {transaction.Amount} {transaction.Currency}", currentUser.UserId),
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
                "Müşteri verileri KVKK unutulma hakkı kapsamında anonimleştirildi.", currentUser.UserId),
            cancellationToken);

        return ToSummary(customer);
    }

    // Beklenen CSV başlıkları (Türkçe, sıra önemsiz — NormalizeHeader ile eşleştiriliyor):
    // Ad, Soyad, E-posta, Doğum Tarihi zorunlu; Telefon, Eğitim Durumu, Yatırım Hesabı Var,
    // Yurtdışı Transfer Var, Dil opsiyonel (Dil boşsa Türkçe varsayılır — bkz.
    // ParsePreferredLanguage).
    public async Task<CustomerImportResult> ImportCustomersAsync(Stream csvStream, CancellationToken cancellationToken = default)
    {
        var bankId = currentUser.ResolveBankIdForWrite();
        using var reader = new StreamReader(csvStream);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
            throw new DomainException(ErrorCodes.CustomerCsvEmpty, "CSV dosyası boş.");

        var headers = ParseCsvLine(headerLine).Select(NormalizeHeader).ToList();
        var columnIndex = new Dictionary<string, int>();
        for (var i = 0; i < headers.Count; i++)
            columnIndex[headers[i]] = i;

        var requiredColumns = new[] { "AD", "SOYAD", "EPOSTA", "DOGUMTARIHI" };
        var missing = requiredColumns.Where(c => !columnIndex.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            throw new DomainException(
                ErrorCodes.CustomerCsvMissingColumns,
                $"CSV başlığında zorunlu sütun(lar) eksik: {string.Join(", ", missing)}. " +
                "Beklenen sütunlar: Ad, Soyad, E-posta, Doğum Tarihi, Telefon, Eğitim Durumu, Yatırım Hesabı Var, Yurtdışı Transfer Var, Dil.",
                parameters: new Dictionary<string, object?> { ["columns"] = string.Join(", ", missing) });

        var existingEmails = (await dbContext.Customers
            .Where(c => c.BankId == bankId)
            .Select(c => c.Email)
            .ToListAsync(cancellationToken)).ToHashSet();
        var seenInFile = new HashSet<string>();
        var errors = new List<CustomerImportRowError>();
        var imported = new List<Customer>();

        // Dosya boyutu (endpoint'te 5MB) tek başına yeterli değil — çok kısa satırlarla dolu
        // bir dosya milyonlarca satıra sığabilir, her biri parse+DB kontrolü tetikler. Satır
        // sayısına da ayrı bir üst sınır konuyor.
        const int maxRows = 5000;

        var rowNumber = 1; // 1 = başlık satırı
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            rowNumber++;
            if (rowNumber - 1 > maxRows)
                throw new DomainException(
                    ErrorCodes.CustomerCsvTooManyRows,
                    $"CSV dosyası çok fazla satır içeriyor (en fazla {maxRows} satır desteklenir).",
                    parameters: new Dictionary<string, object?> { ["maxRows"] = maxRows });

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

                // TryParse (kültüre bağlı, belirsiz format tahmini) yerine bilinçli olarak
                // TryParseExact kullanılıyor — "03/04/1990" gibi gün/ay sırası belirsiz bir
                // tarih, TryParse ile sessizce (hatasız) yanlış bir tarihe (ay/gün ters)
                // çözülebilirdi. Sadece belgelenen format kabul ediliyor.
                var dobRaw = Get("DOGUMTARIHI");
                if (!DateTime.TryParseExact(dobRaw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
                    throw new ArgumentException($"Geçersiz doğum tarihi formatı: '{dobRaw}' (beklenen: yyyy-aa-gg, ör. 1990-05-12).");

                var languageRaw = Get("DIL");
                var preferredLanguage = UiLanguage.Turkish;
                if (!string.IsNullOrWhiteSpace(languageRaw))
                {
                    preferredLanguage = ParsePreferredLanguage(languageRaw)
                        ?? throw new ArgumentException($"Geçersiz dil: '{languageRaw}' (beklenen: Türkçe/English/Deutsch veya TR/EN/DE).");
                }

                var customer = Customer.Create(bankId, Get("AD"), Get("SOYAD"), email, dob, preferredLanguage);

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
                    $"{imported.Count} müşteri CSV ile içe aktarıldı ({errors.Count} satır hatalı).", currentUser.UserId),
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

    // Kampanya SMS/e-postalarının hangi dilde gideceğini (Customer.PreferredLanguage)
    // CSV'den ayarlamak için — enum adının kendisinin (Turkish/English/German) yanı sıra
    // kısa kodları (TR/EN/DE) ve müşteri-dostu tam adları (Türkçe/İngilizce/Almanca/Deutsch)
    // da kabul ediyor, ParseEducationLevel'ın aksine tek bir normalize karşılaştırmasına
    // güvenmek yeterli olmuyordu.
    private static UiLanguage? ParsePreferredLanguage(string value)
    {
        var normalized = NormalizeHeader(value);
        return normalized switch
        {
            "TURKISH" or "TR" or "TURKCE" => UiLanguage.Turkish,
            "ENGLISH" or "EN" or "INGILIZCE" => UiLanguage.English,
            "GERMAN" or "DE" or "ALMANCA" or "DEUTSCH" => UiLanguage.German,
            _ => null
        };
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
            customer.PreferredLanguage, customer.IsErased);

    private static TransactionSummary ToSummary(Transaction transaction) =>
        new(transaction.Id, transaction.CustomerId, transaction.Amount, transaction.Category,
            transaction.MerchantName, transaction.OccurredAtUtc, transaction.Currency, transaction.IsInternational);
}
