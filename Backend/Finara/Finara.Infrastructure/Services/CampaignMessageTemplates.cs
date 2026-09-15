using Finara.Domain.Enums;

namespace Finara.Infrastructure.Services;

// Bankanın kendi müşterisine giden kampanya teklifi SMS/e-postası (OfferDistributionService) —
// EmailTemplates.cs'teki (personel e-postaları — şifre sıfırlama, davet, haftalık özet) ile
// AYNI çeviri deseni ama BİLİNÇLİ OLARAK ayrı bir sınıf: personel dili User.Preferences.Language,
// müşteri dili Customer.PreferredLanguage'dir — ikisi birbirine karışmamalı (bkz. Customer.cs'teki
// PreferredLanguage yorumu).
public static class CampaignMessageTemplates
{
    // Güven skoru BİLEREK bu mesajda YOK: 0-100 arası skor, NextBestOfferScorer'ın bankaya
    // özel iç metriği — personel BackOffice'te görüyor, müşteriye gitmesinin bir anlamı yok.
    // Önceden "Güven skoru: %49" olarak müşteriye SMS/e-posta ile gönderiliyordu (denetimde
    // bulundu); hem iç puanlama mantığını dışarı sızdırıyor hem de düşük bir sayı müşteride
    // haklı olarak "demek ki bana uygun değil" etkisi yaratıp teklifin kendisini baltalıyordu.
    public static string OfferDistributionMessage(string campaignName, UiLanguage language) => language switch
    {
        UiLanguage.English => $"Your {campaignName} offer is waiting for you!",
        UiLanguage.German => $"Ihr {campaignName}-Angebot wartet auf Sie!",
        _ => $"{campaignName} teklifiniz sizi bekliyor!"
    };
}
