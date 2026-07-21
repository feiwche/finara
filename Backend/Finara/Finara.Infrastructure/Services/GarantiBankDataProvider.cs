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
// sandbox API'lerine bağlanıyor. IBankDataProviderFactory'yi tüketen kod (CustomerService)
// hiç değişmedi — Faz 8.2'nin tam da bunun için tasarlandığı an bu.
//
// ÖNEMLİ — bilinen sınır: API'nin tam yanıt şeması (alan adları) sadece geliştirici
// portalında hesap açılıp "Account Information"/"Account Transactions" API'lerinin
// "More Info" dokümantasyonuna girildiğinde görülebiliyor (login arkasında). Aşağıdaki
// GarantiAccountResponse/GarantiTransaction record'ları makul bir varsayıma dayanıyor —
// gerçek hesap açılıp ilk canlı çağrı denendiğinde alan adları doğrulanmalı/düzeltilmeli.
// Banka hesap API'si e-posta/telefon/eğitim durumu gibi Finara'ya özel alanları hiç
// döndürmüyor — bunlar placeholder değerlerle dolduruluyor, içe aktarma sonrası
// BackOffice/CRM'den elle tamamlanmalı. KVKK gereği rıza da VARSAYILAMAZ, hepsi false.
public class GarantiBankDataProvider(IHttpClientFactory httpClientFactory, IGarantiTokenProvider tokenProvider, IOptions<GarantiApiOptions> options) : IBankDataProvider
{
    private readonly GarantiApiOptions _options = options.Value;

    public async Task<CustomerData> GenerateCustomerAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            $"{_options.BaseUrl}/balancesandmovements/accountinformation/account/v1/{_options.TestAccountNumber}",
            cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GarantiAccountResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Garanti hesap bilgisi yanıtı okunamadı.");

        var (firstName, lastName) = SplitName(payload.AccountHolderName ?? "Garanti Müşteri");

        return new CustomerData(
            FirstName: firstName,
            LastName: lastName,
            Email: $"{payload.AccountNumber ?? Guid.NewGuid().ToString("N")}@garanti-import.local",
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
        var response = await SendAsync(
            $"{_options.BaseUrl}/balancesandmovements/accountinformation/transaction/v1/gettransactions?accountNumber={_options.TestAccountNumber}",
            cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GarantiTransactionListResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Garanti işlem listesi yanıtı okunamadı.");

        return payload.Transactions
            .Take(count)
            .Select(t => new TransactionData(
                Amount: Math.Abs(t.Amount),
                Category: MapCategory(t.Description),
                MerchantName: t.Description ?? "Garanti İşlemi",
                OccurredAtUtc: DateTime.SpecifyKind(t.TransactionDate, DateTimeKind.Utc),
                Currency: t.Currency ?? "TRY",
                IsInternational: false))
            .ToList();
    }

    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var client = httpClientFactory.CreateClient("Garanti");
        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], "-");
    }

    // Garanti'nin işlem açıklamasında yapılandırılmış bir kategori alanı yok — anahtar
    // kelime eşlemesiyle kaba bir tahmin yapılıyor, gerçek API dokümantasyonu geldiğinde
    // (muhtemelen bir MCC/kategori kodu döner) iyileştirilmeli.
    private static TransactionCategory MapCategory(string? description)
    {
        var text = (description ?? string.Empty).ToUpperInvariant();
        return text switch
        {
            var d when d.Contains("MARKET") || d.Contains("MIGROS") || d.Contains("BIM") || d.Contains("A101") => TransactionCategory.Groceries,
            var d when d.Contains("AKARYAKIT") || d.Contains("PETROL") || d.Contains("SHELL") || d.Contains("OPET") => TransactionCategory.Fuel,
            var d when d.Contains("SEYAHAT") || d.Contains("HAVAYOLLARI") || d.Contains("UCAK") || d.Contains("OTEL") => TransactionCategory.Travel,
            var d when d.Contains("SIGORTA") || d.Contains("HASTANE") || d.Contains("ECZANE") => TransactionCategory.Healthcare,
            var d when d.Contains("FATURA") || d.Contains("ELEKTRIK") || d.Contains("DOGALGAZ") || d.Contains("TELEKOM") => TransactionCategory.Utilities,
            _ => TransactionCategory.Other
        };
    }

    private record GarantiAccountResponse(
        [property: JsonPropertyName("accountNumber")] string? AccountNumber,
        [property: JsonPropertyName("accountHolderName")] string? AccountHolderName);

    private record GarantiTransactionListResponse(
        [property: JsonPropertyName("transactions")] IReadOnlyList<GarantiTransaction> Transactions);

    private record GarantiTransaction(
        [property: JsonPropertyName("transactionDate")] DateTime TransactionDate,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("description")] string? Description);
}
