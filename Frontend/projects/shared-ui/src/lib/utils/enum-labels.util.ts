import { AuditActionType, SpendingPropensity, TransactionCategory, UserRole } from '../api/finara-client';

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
  TransactionRecorded: 'İşlem Kaydedildi'
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
