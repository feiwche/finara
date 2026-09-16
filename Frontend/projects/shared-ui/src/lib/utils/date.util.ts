// PrimeNG DatePicker seçilen günü yerel saat diliminde gece yarısı olarak temsil eder.
// Buna doğrudan `.toISOString()` çağırmak (UTC'ye çevirir) TR gibi UTC+ dilimlerinde
// günü bir öncesine kaydırıyordu (bilinen, Faz 3.5'ten beri ertelenmiş hata). Bunun yerine
// takvimde görünen yıl/ay/gün'ü doğrudan UTC gece yarısına sabitliyoruz.
// CSV dışa aktarımda tarih biçimi 'tr-TR'ye SABİTLENMİŞTİ — İngilizce/Almanca arayüz
// kullanan personel Türk biçiminde tarih indiriyordu (denetimde bulundu). Aktif arayüz dili
// ngx-translate'in dil kodundan (tr/en/de) türetiliyor; bilinmeyen bir kod gelirse tarayıcı
// varsayılanına düşülür.
const LOCALE_BY_LANGUAGE: Record<string, string> = {
  tr: 'tr-TR',
  en: 'en-US',
  de: 'de-DE'
};

export function resolveLocale(language: string | null | undefined): string {
  return LOCALE_BY_LANGUAGE[language ?? ''] ?? 'tr-TR';
}

export function formatDateTimeForExport(value: string, language: string | null | undefined): string {
  return new Date(value).toLocaleString(resolveLocale(language));
}

export function formatDateForExport(value: string, language: string | null | undefined): string {
  return new Date(value).toLocaleDateString(resolveLocale(language));
}

export function toUtcDateOnlyIso(date: Date): string {
  return new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate())).toISOString();
}
