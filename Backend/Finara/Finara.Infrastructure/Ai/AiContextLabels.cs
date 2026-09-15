using Finara.Domain.Entities;
using Finara.Domain.Enums;

namespace Finara.Infrastructure.Ai;

// Modele verilen context'teki enum değerlerinin kullanıcının diline çevrilmiş karşılıkları.
//
// NEDEN GEREKLİ: context ham enum değerleriyle (HighlyResponsive, ECommerce…) veriliyordu ve
// model bunları olduğu gibi yanıtına kopyalıyordu — kullanıcı "Harcama profili
// HighlyResponsive'dır" gibi yarı İngilizce bir cevap görüyordu (canlı testte bulundu).
//
// BİLİNEN İKİLİK: aynı etiketler frontend'de de var (shared-ui/utils/enum-labels.util.ts +
// public/i18n/{tr,en,de}.json). C# ile TypeScript arasında kod üretmeden paylaşmanın pratik
// bir yolu yok; bu yüzden metinler oradaki i18n dosyalarından BİREBİR kopyalandı. Bir enum
// değeri eklenir/yeniden adlandırılırsa iki tarafın da güncellenmesi gerekir — aşağıdaki
// sözlüklerde karşılığı olmayan değer, ham enum adına düşer (sessizce patlamaz).
internal static class AiContextLabels
{
    public static string Label(TransactionCategory category, UiLanguage language) =>
        Lookup(TransactionCategories, language, category.ToString());

    public static string Label(SpendingPropensity propensity, UiLanguage language) =>
        Lookup(SpendingPropensities, language, propensity.ToString());

    public static string Label(OfferResponse response, UiLanguage language) =>
        Lookup(OfferResponses, language, response.ToString());

    private static string Lookup(
        IReadOnlyDictionary<UiLanguage, IReadOnlyDictionary<string, string>> table,
        UiLanguage language,
        string value) =>
        table.TryGetValue(language, out var labels) && labels.TryGetValue(value, out var label) ? label : value;

    private static readonly IReadOnlyDictionary<UiLanguage, IReadOnlyDictionary<string, string>> TransactionCategories =
        new Dictionary<UiLanguage, IReadOnlyDictionary<string, string>>
        {
            [UiLanguage.Turkish] = new Dictionary<string, string>
            {
                ["Groceries"] = "Market", ["Fuel"] = "Akaryakıt", ["Travel"] = "Seyahat",
                ["Entertainment"] = "Eğlence", ["Healthcare"] = "Sağlık", ["ECommerce"] = "E-Ticaret",
                ["Motorcycle"] = "Motosiklet", ["Investment"] = "Yatırım", ["ForeignExchange"] = "Döviz",
                ["Utilities"] = "Faturalar", ["Other"] = "Diğer"
            },
            [UiLanguage.English] = new Dictionary<string, string>
            {
                ["Groceries"] = "Groceries", ["Fuel"] = "Fuel", ["Travel"] = "Travel",
                ["Entertainment"] = "Entertainment", ["Healthcare"] = "Healthcare", ["ECommerce"] = "E-Commerce",
                ["Motorcycle"] = "Motorcycle", ["Investment"] = "Investment", ["ForeignExchange"] = "Foreign Exchange",
                ["Utilities"] = "Utilities", ["Other"] = "Other"
            },
            [UiLanguage.German] = new Dictionary<string, string>
            {
                ["Groceries"] = "Lebensmittel", ["Fuel"] = "Kraftstoff", ["Travel"] = "Reisen",
                ["Entertainment"] = "Unterhaltung", ["Healthcare"] = "Gesundheit", ["ECommerce"] = "E-Commerce",
                ["Motorcycle"] = "Motorrad", ["Investment"] = "Investition", ["ForeignExchange"] = "Devisen",
                ["Utilities"] = "Rechnungen", ["Other"] = "Sonstiges"
            }
        };

    private static readonly IReadOnlyDictionary<UiLanguage, IReadOnlyDictionary<string, string>> SpendingPropensities =
        new Dictionary<UiLanguage, IReadOnlyDictionary<string, string>>
        {
            [UiLanguage.Turkish] = new Dictionary<string, string>
            {
                ["NeedsEngagement"] = "Teşvik Gerekli", ["Neutral"] = "Nötr", ["HighlyResponsive"] = "Harcamaya Yatkın"
            },
            [UiLanguage.English] = new Dictionary<string, string>
            {
                ["NeedsEngagement"] = "Needs Engagement", ["Neutral"] = "Neutral", ["HighlyResponsive"] = "Highly Responsive"
            },
            [UiLanguage.German] = new Dictionary<string, string>
            {
                ["NeedsEngagement"] = "Ansprache Nötig", ["Neutral"] = "Neutral", ["HighlyResponsive"] = "Kaufbereit"
            }
        };

    private static readonly IReadOnlyDictionary<UiLanguage, IReadOnlyDictionary<string, string>> OfferResponses =
        new Dictionary<UiLanguage, IReadOnlyDictionary<string, string>>
        {
            [UiLanguage.Turkish] = new Dictionary<string, string>
            {
                ["Pending"] = "Bekliyor", ["Accepted"] = "Kabul Edildi", ["Declined"] = "Reddedildi", ["Expired"] = "Süresi Doldu"
            },
            [UiLanguage.English] = new Dictionary<string, string>
            {
                ["Pending"] = "Pending", ["Accepted"] = "Accepted", ["Declined"] = "Declined", ["Expired"] = "Expired"
            },
            [UiLanguage.German] = new Dictionary<string, string>
            {
                ["Pending"] = "Ausstehend", ["Accepted"] = "Angenommen", ["Declined"] = "Abgelehnt", ["Expired"] = "Abgelaufen"
            }
        };
}
