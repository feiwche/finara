import { AgeBracket, AuditActionType, BankDataProviderType, ConsentChannel, EducationLevel, SpendingPropensity, TransactionCategory, UserRole } from '../api/finara-client';

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
  BankAdmin: 'Banka Yöneticisi'
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
  ConsentRevoked: 'Rıza Kaldırıldı'
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
  return TRANSACTION_CATEGORY_LABELS[category] ?? category;
}

export function spendingPropensityLabel(propensity: string): string {
  return SPENDING_PROPENSITY_LABELS[propensity] ?? propensity;
}

export function campaignStatusLabel(status: string): string {
  return CAMPAIGN_STATUS_LABELS[status] ?? status;
}

export function offerResponseLabel(response: string): string {
  return OFFER_RESPONSE_LABELS[response] ?? response;
}

export function auditActionTypeLabel(actionType: string): string {
  return AUDIT_ACTION_TYPE_LABELS[actionType] ?? actionType;
}

export function userRoleLabel(role: string): string {
  return USER_ROLE_LABELS[role] ?? role;
}

export function educationLevelLabel(level: string): string {
  return EDUCATION_LEVEL_LABELS[level] ?? level;
}

export function ageBracketLabel(bracket: string): string {
  return AGE_BRACKET_LABELS[bracket] ?? bracket;
}

export function consentChannelLabel(channel: string): string {
  return CONSENT_CHANNEL_LABELS[channel] ?? channel;
}

export function bankDataProviderTypeLabel(type: string): string {
  return BANK_DATA_PROVIDER_TYPE_LABELS[type] ?? type;
}

// p-select gibi dropdown'lar için: değer İngilizce enum olarak kalır (API'ye o gönderilir),
// kullanıcıya sadece label gösterilir (optionLabel="label" optionValue="value" ile).
export const TRANSACTION_CATEGORY_OPTIONS = Object.values(TransactionCategory).map(value => ({
  label: transactionCategoryLabel(value),
  value
}));

export const SPENDING_PROPENSITY_OPTIONS = Object.values(SpendingPropensity).map(value => ({
  label: spendingPropensityLabel(value),
  value
}));

export const AUDIT_ACTION_TYPE_OPTIONS = Object.values(AuditActionType).map(value => ({
  label: auditActionTypeLabel(value),
  value
}));

export const USER_ROLE_OPTIONS = Object.values(UserRole).map(value => ({
  label: userRoleLabel(value),
  value
}));

export const EDUCATION_LEVEL_OPTIONS = Object.values(EducationLevel).map(value => ({
  label: educationLevelLabel(value),
  value
}));

export const AGE_BRACKET_OPTIONS = Object.values(AgeBracket).map(value => ({
  label: ageBracketLabel(value),
  value
}));

export const CONSENT_CHANNEL_OPTIONS = Object.values(ConsentChannel).map(value => ({
  label: consentChannelLabel(value),
  value
}));
