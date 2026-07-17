// PrimeNG DatePicker seçilen günü yerel saat diliminde gece yarısı olarak temsil eder.
// Buna doğrudan `.toISOString()` çağırmak (UTC'ye çevirir) TR gibi UTC+ dilimlerinde
// günü bir öncesine kaydırıyordu (bilinen, Faz 3.5'ten beri ertelenmiş hata). Bunun yerine
// takvimde görünen yıl/ay/gün'ü doğrudan UTC gece yarısına sabitliyoruz.
export function toUtcDateOnlyIso(date: Date): string {
  return new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate())).toISOString();
}
