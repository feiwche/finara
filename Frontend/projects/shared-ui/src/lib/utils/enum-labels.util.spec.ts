import {
  ageBracketLabel,
  auditActionTypeLabel,
  bankDataProviderTypeLabel,
  campaignStatusLabel,
  consentChannelLabel,
  educationLevelLabel,
  getTransactionCategoryOptions,
  getUserRoleOptions,
  offerResponseLabel,
  spendingPropensityLabel,
  transactionCategoryLabel,
  userRoleLabel
} from './enum-labels.util';
import { TransactionCategory, UserRole } from '../api/finara-client';

describe('enum-labels.util', () => {
  it('bilinen bir işlem kategorisini Türkçeye çevirir', () => {
    expect(transactionCategoryLabel('Groceries')).toBe('Market');
  });

  it('bilinmeyen bir işlem kategorisi için ham değeri döner (silinmemiş yeni bir backend değeri)', () => {
    expect(transactionCategoryLabel('BilinmeyenKategori')).toBe('BilinmeyenKategori');
  });

  it('harcama eğilimini Türkçeye çevirir', () => {
    expect(spendingPropensityLabel('HighlyResponsive')).toBe('Harcamaya Yatkın');
  });

  it('kampanya durumunu Türkçeye çevirir', () => {
    expect(campaignStatusLabel('Active')).toBe('Aktif');
  });

  it('teklif yanıtını Türkçeye çevirir', () => {
    expect(offerResponseLabel('Accepted')).toBe('Kabul Edildi');
  });

  it('denetim kaydı aksiyon tipini Türkçeye çevirir', () => {
    expect(auditActionTypeLabel('CustomerErased')).toBe('Müşteri Verisi Anonimleştirildi (KVKK)');
  });

  it('kullanıcı rolünü Türkçeye çevirir', () => {
    expect(userRoleLabel('Analyst')).toBe('Analist (Salt Okunur)');
  });

  it('eğitim seviyesini Türkçeye çevirir', () => {
    expect(educationLevelLabel('Lisans')).toBe('Lisans');
  });

  it('yaş aralığını olduğu gibi döner (zaten Türkçe/rakam)', () => {
    expect(ageBracketLabel('Age26To35')).toBe('26-35');
  });

  it('rıza kanalını Türkçeye çevirir', () => {
    expect(consentChannelLabel('Sms')).toBe('SMS');
  });

  it('banka veri sağlayıcı tipini Türkçeye çevirir', () => {
    expect(bankDataProviderTypeLabel('GarantiSandbox')).toBe('Garanti BBVA Sandbox');
  });

  it('getTransactionCategoryOptions() enum değerleriyle birebir aynı sayıda ve doğru label ile üretilir', () => {
    const options = getTransactionCategoryOptions();
    expect(options.length).toBe(Object.values(TransactionCategory).length);
    const groceries = options.find(o => o.value === TransactionCategory.Groceries);
    expect(groceries?.label).toBe('Market');
  });

  it('getUserRoleOptions() her rol için doğru label üretir', () => {
    const analyst = getUserRoleOptions().find(o => o.value === UserRole.Analyst);
    expect(analyst?.label).toBe('Analist (Salt Okunur)');
  });
});
