using Finara.Domain.Enums;

namespace Finara.Infrastructure.Services;

// Bankanın kendi müşterisine giden kampanya teklifi SMS/e-postası (OfferDistributionService) —
// EmailTemplates.cs'teki (personel e-postaları — şifre sıfırlama, davet, haftalık özet) ile
// AYNI çeviri deseni ama BİLİNÇLİ OLARAK ayrı bir sınıf: personel dili User.Preferences.Language,
// müşteri dili Customer.PreferredLanguage'dir — ikisi birbirine karışmamalı (bkz. Customer.cs'teki
// PreferredLanguage yorumu).
public static class CampaignMessageTemplates
{
    public static string OfferDistributionMessage(string campaignName, double confidenceScore, UiLanguage language) => language switch
    {
        UiLanguage.English => $"Your {campaignName} offer is waiting for you! Confidence score: {confidenceScore:0}%",
        UiLanguage.German => $"Ihr {campaignName}-Angebot wartet auf Sie! Vertrauenswert: %{confidenceScore:0}",
        _ => $"{campaignName} teklifiniz sizi bekliyor! Güven skoru: %{confidenceScore:0}"
    };
}
