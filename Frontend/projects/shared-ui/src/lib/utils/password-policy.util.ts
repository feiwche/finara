// Backend'deki PasswordPolicy.Validate ile BİREBİR aynı kural (en az 8 karakter, en az bir
// harf ve bir rakam). İstemci tarafında hiç doğrulama yoktu (denetimde bulundu): kullanıcı
// "12" gibi bir şifre girip formu gönderiyor, "yükleniyor" bekliyor ve backend'den anlaşılması
// güç genel bir hata alıyordu. Burada erken uyarmak sunucu doğrulamasının YERİNE geçmez —
// gerçek kontrol hâlâ backend'de, bu yalnızca kullanıcıya hızlı geri bildirim.
//
// Kural değişirse İKİ tarafın da güncellenmesi gerekir; bu ikilik AiContextLabels'taki
// C#/TypeScript paylaşım sorununun aynısı.
//
// Geçerliyse null, değilse i18n anahtarı döner.
export function passwordPolicyError(password: string | null | undefined): string | null {
  if (!password || password.length < 8) return 'auth.password.tooShort';
  if (!/[0-9]/.test(password) || !/[a-zA-Z]/.test(password)) return 'auth.password.needsLetterAndDigit';
  return null;
}
