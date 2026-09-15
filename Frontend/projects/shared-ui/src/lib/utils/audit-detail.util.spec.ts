import { auditDetailLabel } from './audit-detail.util';

// TranslateService kaydedilmediği için translateOrFallback/translateEnumValue Türkçe
// yedeklere düşüyor — bu testler tam olarak o yedek davranışı ve ayrıştırma mantığını
// sabitliyor (çeviri dosyalarının içeriğini değil).
describe('audit-detail.util', () => {
  it('segment atama detayındaki ham enum değerlerini çevirir', () => {
    expect(auditDetailLabel('Neutral / baskın kategori: Travel'))
      .toBe('Nötr / baskın kategori: Seyahat');
  });

  it('otomasyon ön ekini koruyup gerisini çevirir', () => {
    expect(auditDetailLabel('[Otomasyon] HighlyResponsive / baskın kategori: ECommerce'))
      .toBe('[Otomasyon] Harcamaya Yatkın / baskın kategori: E-Ticaret');
  });

  it('rıza kaydındaki kanal adını çevirir', () => {
    expect(auditDetailLabel('Sms')).toBe('SMS');
  });

  it('yönetici hesabı detayındaki rolü çevirir, e-postaya dokunmaz', () => {
    expect(auditDetailLabel('admin@finara.dev (BankAdmin)'))
      .toBe('admin@finara.dev (Banka Yöneticisi)');
  });

  // Asıl risk: serbest metinde kör arama yapılırsa, adında enum değeri geçen bir kampanya
  // yanlışlıkla çevrilir. Bu yüzden desen tutsa bile yakalanan parçanın BİLİNEN bir enum
  // değeri olması şart.
  it('bilinen bir enum olmayan parantezli metni olduğu gibi bırakır', () => {
    expect(auditDetailLabel('Yaz Kampanyası (Taslak)')).toBe('Yaz Kampanyası (Taslak)');
  });

  it('kampanya adı enum değerine benzese bile dokunmaz', () => {
    expect(auditDetailLabel('Travel / gönderildi')).toBe('Travel / gönderildi');
  });

  it('tanınmayan serbest metni olduğu gibi döner', () => {
    expect(auditDetailLabel('12 müşteri CSV ile içe aktarıldı (0 satır hatalı).'))
      .toBe('12 müşteri CSV ile içe aktarıldı (0 satır hatalı).');
  });

  it('boş/null detay için boş metin döner', () => {
    expect(auditDetailLabel(null)).toBe('');
    expect(auditDetailLabel(undefined)).toBe('');
    expect(auditDetailLabel('')).toBe('');
  });
});
