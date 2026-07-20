namespace Finara.Domain.Enums;

// Faz 8.1 — multi-tenancy. SuperAdmin BİLİNÇLİ OLARAK 0 değerinde bırakıldı: eski
// tek değerli enum'da Admin=0'dı, DB'de zaten int olarak saklı olan tüm mevcut
// kullanıcılar (admin@finara.dev fixture'ı dahil) bu sıralama sayesinde ekstra bir
// veri migrasyonuna gerek kalmadan otomatik SuperAdmin olarak okunuyor.
// SuperAdmin: User.BankId null, tüm bankaları görür/yönetir.
// BankAdmin: User.BankId dolu, sadece kendi bankasına scope'lu.
public enum UserRole
{
    SuperAdmin,
    BankAdmin
}
