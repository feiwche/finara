namespace Finara.Domain.Enums;

// Müşterinin kampanyaya tepki verme eğilimi — segmentasyon motorunun ürettiği sınıflandırma
public enum SpendingPropensity
{
    NeedsEngagement,   // teşvik gerekli
    Neutral,           // üzerine durmaya lüzum yok
    HighlyResponsive   // harcamaya eğilimli
}
