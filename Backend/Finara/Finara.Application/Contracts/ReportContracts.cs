namespace Finara.Application.Contracts;

public record DailyOfferTrendPoint(DateTime Date, int Accepted, int Declined);

public record DailyCustomerGrowthPoint(DateTime Date, int NewCustomers);

// Dashboard'daki anlık KPI'ları (müşteri/kampanya sayısı) tamamlayan, "kural motoru/AI
// gerçekten ne kadar iş yapıyor" sorusuna cevap veren tek satırlık bir özet — tüm zamanlar
// için (30 günlük pencereyle sınırlı değil), CampaignOffers tablosundan hesaplanıyor.
public record OfferImpactSummary(
    int TotalOffersGenerated,
    int AcceptedOffers,
    double AverageConfidenceScore,
    int EngagedCustomers);
