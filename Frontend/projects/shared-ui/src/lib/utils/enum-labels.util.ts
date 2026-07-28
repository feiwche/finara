import { AgeBracket, AuditActionType, BankDataProviderType, ConsentChannel, EducationLevel, SpendingPropensity, TransactionCategory, UserRole } from '../api/finara-client';
import { TranslateService } from '@ngx-translate/core';

// Bu modüldeki label fonksiyonları çoğu bileşende doğrudan şablonda ({{ xLabel(value) }})
// kullanıldığı için imzalarını (parametre/dönüş tipini) DEĞİŞTİRMEDEN dil değişimine tepki
// vermeleri gerekiyordu — bu yüzden DI enjeksiyonu yerine, provideFinaraUi() içindeki
// APP_INITIALIZER'ın registerTranslateServiceForEnumLabels() ile bir kere kaydettiği modül
// seviyesinde bir TranslateService referansı kullanılıyor. instant() her çağrıda GÜNCEL dili
// okuduğu için (Angular değişiklik algılama döngüsü zaten dil değişince tetikleniyor), 12'den
// fazla tüketici dosyayı computed/signal'a taşımaya gerek kalmadan reaktiflik sağlanıyor.
let translateServiceRef: TranslateService | null = null;

export function registerTranslateServiceForEnumLabels(translate: TranslateService): void {
  translateServiceRef = translate;
}

function translateEnumValue(namespace: string, value: string, fallbackMap: Record<string, string>): string {
  const key = `enums.${namespace}.${value}`;
  const translated = translateServiceRef?.instant(key);
  if (translated && translated !== key) return translated;
  return fallbackMap[value] ?? value;
}

// Backend enum'ları (İngilizce, API sözleşmesinin parçası) ile arayüzde gösterilen
// metin birbirinden ayrılıyor — [(ngModel)]/API çağrıları hep İngilizce değeri kullanır,
// kullanıcı sadece bu Türkçe karşılıkları görür.
const TRANSACTION_CATEGORY_LABELS: Record<string, string> = {
  Groceries: 'Market',
  Fuel: 'Akaryakıt',
  Travel: 'Seyahat',
  Entertainment: 'Eğlence',
  Healthcare: 'Sağlık',
  ECommerce: 'E-Ticaret',
  Motorcycle: 'Motosiklet',
  Investment: 'Yatırım',
  ForeignExchange: 'Döviz',
  Utilities: 'Faturalar',
  Other: 'Diğer'
};

const SPENDING_PROPENSITY_LABELS: Record<string, string> = {
  NeedsEngagement: 'Teşvik Gerekli',
  Neutral: 'Nötr',
  HighlyResponsive: 'Harcamaya Yatkın'
};

const CAMPAIGN_STATUS_LABELS: Record<string, string> = {
  Draft: 'Taslak',
  Active: 'Aktif',
  Paused: 'Duraklatıldı',
  Completed: 'Tamamlandı',
  Archived: 'Arşivlendi'
};

const OFFER_RESPONSE_LABELS: Record<string, string> = {
  Pending: 'Bekliyor',
  Accepted: 'Kabul Edildi',
  Declined: 'Reddedildi',
  Expired: 'Süresi Doldu'
};

const USER_ROLE_LABELS: Record<string, string> = {
  SuperAdmin: 'Süper Yönetici',
  BankAdmin: 'Banka Yöneticisi',
  Analyst: 'Analist (Salt Okunur)'
};

const AUDIT_ACTION_TYPE_LABELS: Record<string, string> = {
  SegmentAssigned: 'Segment Atandı',
  CampaignOfferPresented: 'Teklif Sunuldu',
  CampaignOfferAccepted: 'Teklif Kabul Edildi',
  CampaignOfferDeclined: 'Teklif Reddedildi',
  CustomerCreated: 'Müşteri Oluşturuldu',
  TransactionRecorded: 'İşlem Kaydedildi',
  OfferDistributed: 'Teklif Dağıtıldı',
  ConsentGranted: 'Rıza Verildi',
  ConsentRevoked: 'Rıza Kaldırıldı',
  AdminCreated: 'Yönetici Oluşturuldu',
  AdminDeactivated: 'Yönetici Pasifleştirildi',
  CustomerBulkImported: 'Müşteri Toplu İçe Aktarıldı',
  CustomerErased: 'Müşteri Verisi Anonimleştirildi (KVKK)',
  AdminInvited: 'Yönetici Daveti Gönderildi',
  AdminInvitationAccepted: 'Yönetici Daveti Kabul Edildi',
  AdminInvitationRevoked: 'Yönetici Daveti İptal Edildi',
  AdminSessionsRevoked: 'Yönetici Oturumları Sonlandırıldı'
};

const CONSENT_CHANNEL_LABELS: Record<string, string> = {
  Sms: 'SMS',
  Email: 'E-posta',
  InAppBanking: 'Bankacılık Uygulaması İçi'
};

const EDUCATION_LEVEL_LABELS: Record<string, string> = {
  Ilkogretim: 'İlköğretim',
  Lise: 'Lise',
  Onlisans: 'Önlisans',
  Lisans: 'Lisans',
  Lisansustu: 'Lisansüstü'
};

const BANK_DATA_PROVIDER_TYPE_LABELS: Record<string, string> = {
  Mock: 'Simüle Veri (Mock)',
  GarantiSandbox: 'Garanti BBVA Sandbox'
};

const AGE_BRACKET_LABELS: Record<string, string> = {
  Age18To25: '18-25',
  Age26To35: '26-35',
  Age36To45: '36-45',
  Age46To55: '46-55',
  Age56Plus: '56+'
};

export function transactionCategoryLabel(category: string): string {
  return translateEnumValue('transactionCategory', category, TRANSACTION_CATEGORY_LABELS);
}

export function spendingPropensityLabel(propensity: string): string {
  return translateEnumValue('spendingPropensity', propensity, SPENDING_PROPENSITY_LABELS);
}

export function campaignStatusLabel(status: string): string {
  return translateEnumValue('campaignStatus', status, CAMPAIGN_STATUS_LABELS);
}

export function offerResponseLabel(response: string): string {
  return translateEnumValue('offerResponse', response, OFFER_RESPONSE_LABELS);
}

export function auditActionTypeLabel(actionType: string): string {
  return translateEnumValue('auditActionType', actionType, AUDIT_ACTION_TYPE_LABELS);
}

export function userRoleLabel(role: string): string {
  return translateEnumValue('userRole', role, USER_ROLE_LABELS);
}

export function educationLevelLabel(level: string): string {
  return translateEnumValue('educationLevel', level, EDUCATION_LEVEL_LABELS);
}

export function ageBracketLabel(bracket: string): string {
  return translateEnumValue('ageBracket', bracket, AGE_BRACKET_LABELS);
}

export function consentChannelLabel(channel: string): string {
  return translateEnumValue('consentChannel', channel, CONSENT_CHANNEL_LABELS);
}

export function bankDataProviderTypeLabel(type: string): string {
  return translateEnumValue('bankDataProviderType', type, BANK_DATA_PROVIDER_TYPE_LABELS);
}

// p-select gibi dropdown'lar için: değer İngilizce enum olarak kalır (API'ye o gönderilir),
// kullanıcıya sadece label gösterilir (optionLabel="label" optionValue="value" ile).
// Fonksiyon olarak tanımlanıyor (modül yüklenirken bir kere hesaplanan sabit DEĞİL) —
// çağıran bileşen bunu translate.onLangChange'e bağlı bir computed() içinde çağırırsa
// (settings-page.component.ts'teki desenle aynı) dropdown seçenekleri de dil değişince
// güncellenir; sabit olsaydı ilk yüklemedeki dilde donup kalırdı.
export function getTransactionCategoryOptions() {
  return Object.values(TransactionCategory).map(value => ({ label: transactionCategoryLabel(value), value }));
}

export function getSpendingPropensityOptions() {
  return Object.values(SpendingPropensity).map(value => ({ label: spendingPropensityLabel(value), value }));
}

export function getAuditActionTypeOptions() {
  return Object.values(AuditActionType).map(value => ({ label: auditActionTypeLabel(value), value }));
}

export function getUserRoleOptions() {
  return Object.values(UserRole).map(value => ({ label: userRoleLabel(value), value }));
}

export function getEducationLevelOptions() {
  return Object.values(EducationLevel).map(value => ({ label: educationLevelLabel(value), value }));
}

export function getAgeBracketOptions() {
  return Object.values(AgeBracket).map(value => ({ label: ageBracketLabel(value), value }));
}

export function getConsentChannelOptions() {
  return Object.values(ConsentChannel).map(value => ({ label: consentChannelLabel(value), value }));
}
