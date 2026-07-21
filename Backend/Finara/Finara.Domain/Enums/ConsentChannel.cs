namespace Finara.Domain.Enums;

// Faz 8.4 — KVKK/İYS gerekliliği: teklif dağıtımı, açık rıza olmadan hiçbir kanaldan
// yapılamaz (bkz. IConsentGate). InAppBanking (bankacılık uygulaması içi bildirim) için
// rıza takibi var ama gerçek bir INotificationDispatcher implementasyonu henüz yok —
// bu, bankaya veri geri gönderen bir OUTBOUND entegrasyon gerektiriyor, kapsam dışı
// (BankDataProviderType'ta "Real" değerinin henüz eklenmemesiyle aynı gerekçe).
public enum ConsentChannel
{
    Sms,
    Email,
    InAppBanking
}
