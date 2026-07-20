namespace Finara.Domain.Enums;

// Faz 8.3 — Kohort motoru için demografik boyutlardan biri. Customer.EducationLevel
// nullable: gerçek veri kaynağında (ya da elle girilen müşterilerde) her zaman
// bilinmeyebilir, CohortEngine bilinmeyeni ayrı bir grup olarak ele alır.
public enum EducationLevel
{
    Ilkogretim,
    Lise,
    Onlisans,
    Lisans,
    Lisansustu
}
