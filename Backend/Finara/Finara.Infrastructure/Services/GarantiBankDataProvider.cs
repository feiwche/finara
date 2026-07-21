using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Finara.Application.Interfaces;
using Finara.Domain.Enums;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Finara.Infrastructure.Services;

// Faz 8'in gerçek banka entegrasyonu — Garanti BBVA'nın geliştirici portalındaki
// (developers.garantibbva.com.tr) "Account Information" ve "Account Transactions"
// sandbox API'lerine bağlanıyor. Şema, portaldaki "Documentation" sekmesinden elle
// doğrulandı (2026-07-21) — GET değil POST, JSON gövdeli, consentId ZORUNLU.
//
// ÖNEMLİ: Account Information yanıtı müşteri adı DÖNDÜRMÜYOR (sadece bakiye/IBAN/hesap
// tipi) — müşteri adı Account Transactions yanıtındaki "customerName" alanından
// alınıyor, bu yüzden GenerateCustomerAsync önce bir işlem sayfası çekiyor. E-posta/
// telefon/eğitim durumu gibi Finara'ya özel alanları hiçbir Garanti API'si döndürmüyor —
// placeholder değerlerle dolduruluyor, içe aktarma sonrası elle tamamlanmalı. KVKK
// gereği rıza da varsayılamıyor, hepsi false.
public class GarantiBankDataProvider(IHttpClientFactory httpClientFactory, IGarantiTokenProvider tokenProvider, IOptions<GarantiApiOptions> options) : IBankDataProvider
{
    private readonly GarantiApiOptions _options = options.Value;

    public async Task<CustomerData> GenerateCustomerAsync(CancellationToken cancellationToken = default)
    {
        // Account Information müşteri adı döndürmüyor — sadece hesap/bakiye bilgisi
        // teyit etmek için çağrılıyor, isim Account Transactions'tan geliyor.
        await PostAsync<GarantiAccountInformationResponse>(
            "/balancesandmovements/accountinformation/account/v1/getaccountinformation",
            new GarantiAccountInformationRequest(_options.ConsentId, _options.UnitNum, _options.AccountNum, _options.Iban),
            cancellationToken);

        var transactions = await FetchTransactionsAsync(cancellationToken);
        var customerName = transactions.FirstOrDefault()?.CustomerName?.Trim();
        var (firstName, lastName) = SplitName(string.IsNullOrWhiteSpace(customerName) ? "Garanti Müşteri" : customerName);

        return new CustomerData(
            FirstName: firstName,
            LastName: lastName,
            Email: $"{_options.AccountNum}@garanti-import.local",
            DateOfBirth: DateTime.UtcNow.AddYears(-30),
            HasInvestmentAccount: false,
            HasInternationalTransfers: false,
            EducationLevel: EducationLevel.Lisans,
            PhoneNumber: string.Empty,
            Consents: new Dictionary<ConsentChannel, bool>
            {
                [ConsentChannel.Sms] = false,
                [ConsentChannel.Email] = false,
                [ConsentChannel.InAppBanking] = false
            });
    }

    public async Task<IReadOnlyList<TransactionData>> GenerateTransactionsAsync(int count, CancellationToken cancellationToken = default)
    {
        var transactions = await FetchTransactionsAsync(cancellationToken);

        return transactions
            .Take(count)
            .Select(t => new TransactionData(
                Amount: Math.Abs(t.Amount),
                Category: MapCategory(t.Explanation),
                MerchantName: string.IsNullOrWhiteSpace(t.Explanation) ? "Garanti İşlemi" : t.Explanation.Trim(),
                OccurredAtUtc: DateTime.SpecifyKind(t.ActivityDate, DateTimeKind.Utc),
                Currency: string.IsNullOrWhiteSpace(t.CurrencyCode) ? "TRY" : NormalizeCurrency(t.CurrencyCode),
                IsInternational: t.ClasificationCode?.Contains("DOV", StringComparison.OrdinalIgnoreCase) == true))
            .ToList();
    }

    private async Task<IReadOnlyList<GarantiTransaction>> FetchTransactionsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var request = new GarantiTransactionsRequest(
            ConsentId: _options.ConsentId,
            UnitNum: _options.UnitNum,
            AccountNum: _options.AccountNum,
            Iban: _options.Iban,
            // Garanti'nin kendi kısıtı: aralık 30 günden fazla olamaz.
            StartDate: now.AddDays(-30).ToString("yyyy-MM-ddTHH:mm:ss.fff"),
            EndDate: now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
            PageIndex: 1,
            PageSize: 100);

        var response = await PostAsync<GarantiTransactionListResponse>(
            "/balancesandmovements/accountinformation/transaction/v1/gettransactions",
            request,
            cancellationToken);

        return response.Transactions ?? [];
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}{path}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var client = httpClientFactory.CreateClient("Garanti");
        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Garanti '{path}' yanıtı okunamadı.");
    }

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], "-");
    }

    // Garanti "TL " gibi boşluklu/kısaltılmış kodlar döndürebiliyor (bkz. örnek yanıt) —
    // Transaction.Create() ISO 4217 formatı beklemiyor ama tutarlılık için normalize ediliyor.
    private static string NormalizeCurrency(string currencyCode) =>
        currencyCode.Trim().ToUpperInvariant() switch
        {
            "TL" => "TRY",
            var c => c
        };

    // Garanti'nin "explanation" (işlem açıklaması) serbest metin — yapılandırılmış bir
    // kategori/MCC alanı yok, bu yüzden gerçek örnek açıklamalara (MAAŞ ÖDEMESİ, Para
    // Transferi, DÖVİZ İŞLEMLERİ vb. — Documentation sekmesindeki örnek yanıttan) göre
    // anahtar kelime eşlemesi yapılıyor.
    private static TransactionCategory MapCategory(string? explanation)
    {
        var text = (explanation ?? string.Empty).ToUpperInvariant();
        return text switch
        {
            var d when d.Contains("MAAŞ") || d.Contains("MAAS") => TransactionCategory.Other,
            var d when d.Contains("DÖVİZ") || d.Contains("DOVIZ") => TransactionCategory.ForeignExchange,
            var d when d.Contains("TRANSFER") || d.Contains("EFT") || d.Contains("HAVALE") => TransactionCategory.Other,
            var d when d.Contains("MARKET") || d.Contains("MIGROS") || d.Contains("BIM") || d.Contains("A101") => TransactionCategory.Groceries,
            var d when d.Contains("AKARYAKIT") || d.Contains("PETROL") || d.Contains("SHELL") || d.Contains("OPET") => TransactionCategory.Fuel,
            var d when d.Contains("SEYAHAT") || d.Contains("HAVAYOLLARI") || d.Contains("UCAK") || d.Contains("OTEL") => TransactionCategory.Travel,
            var d when d.Contains("SIGORTA") || d.Contains("HASTANE") || d.Contains("ECZANE") => TransactionCategory.Healthcare,
            var d when d.Contains("FATURA") || d.Contains("ELEKTRIK") || d.Contains("DOGALGAZ") || d.Contains("TELEKOM") => TransactionCategory.Utilities,
            _ => TransactionCategory.Other
        };
    }

    private record GarantiAccountInformationRequest(
        [property: JsonPropertyName("consentId")] string ConsentId,
        [property: JsonPropertyName("unitNum")] string UnitNum,
        [property: JsonPropertyName("accountNum")] string AccountNum,
        [property: JsonPropertyName("IBAN")] string Iban);

    private record GarantiAccountInformationResponse(
        [property: JsonPropertyName("result")] GarantiResult? Result,
        [property: JsonPropertyName("accounts")] IReadOnlyList<GarantiAccount>? Accounts);

    private record GarantiAccount(
        [property: JsonPropertyName("accountNum")] long AccountNum,
        [property: JsonPropertyName("IBAN")] string? Iban,
        [property: JsonPropertyName("currencyCode")] string? CurrencyCode,
        [property: JsonPropertyName("status")] string? Status);

    private record GarantiTransactionsRequest(
        [property: JsonPropertyName("consentId")] string ConsentId,
        [property: JsonPropertyName("unitNum")] string UnitNum,
        [property: JsonPropertyName("accountNum")] string AccountNum,
        [property: JsonPropertyName("IBAN")] string Iban,
        [property: JsonPropertyName("startDate")] string StartDate,
        [property: JsonPropertyName("endDate")] string EndDate,
        [property: JsonPropertyName("pageIndex")] int PageIndex,
        [property: JsonPropertyName("pageSize")] int PageSize);

    private record GarantiTransactionListResponse(
        [property: JsonPropertyName("result")] GarantiResult? Result,
        [property: JsonPropertyName("transactions")] IReadOnlyList<GarantiTransaction>? Transactions);

    private record GarantiTransaction(
        [property: JsonPropertyName("customerName")] string? CustomerName,
        [property: JsonPropertyName("activityDate")] DateTime ActivityDate,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("currencyCode")] string? CurrencyCode,
        [property: JsonPropertyName("explanation")] string? Explanation,
        [property: JsonPropertyName("clasificationCode")] string? ClasificationCode,
        [property: JsonPropertyName("txnCreditDebitIndicator")] string? TxnCreditDebitIndicator);

    private record GarantiResult(
        [property: JsonPropertyName("returnCode")] int ReturnCode,
        [property: JsonPropertyName("reasonCode")] int ReasonCode,
        [property: JsonPropertyName("messageText")] string? MessageText);
}
