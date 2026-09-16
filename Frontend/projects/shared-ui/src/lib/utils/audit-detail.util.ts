import { ConsentChannel, SpendingPropensity, TransactionCategory, UserRole } from '../api/finara-client';
import {
  consentChannelLabel,
  spendingPropensityLabel,
  transactionCategoryLabel,
  translateOrFallback,
  userRoleLabel
} from './enum-labels.util';

// Denetim Kaydı'ndaki "Detay" sütunu, backend'in AuditLog.Details alanına yazdığı serbest
// metni olduğu gibi gösteriyordu — içinde ham İngilizce enum değerleri geçiyordu ("Neutral /
// baskın kategori: Travel", "x@y.com (BankAdmin)", "Sms"). Canlı testte bulundu; projenin
// "enum'lar asla ham İngilizce gösterilmez" kuralını ihlal ediyordu.
//
// Çeviri BİLEREK gösterim anında yapılıyor, veritabanına yazarken değil: bir denetim kaydı
// değiştirilemez ve dilden bağımsız olmalı (kanıt değeri bundan geliyor), ayrıca aynı kayıt
// üç farklı dildeki kullanıcıya kendi dilinde gösterilebilmeli. Bu yaklaşım veritabanındaki
// MEVCUT kayıtlar için de çalışır, göç gerektirmez.
//
// Yanlış eşleşmeye karşı korunma: desen tutmak TEK BAŞINA yeterli değil, yakalanan parçanın
// gerçekten BİLİNEN bir enum değeri olması da şart (ör. adı "Travel" olan bir kampanya
// yanlışlıkla çevrilmesin). Bu sayede fonksiyonun aksiyon tipini bilmesine gerek kalmıyor —
// PrimeNG'in exportFunction'ı satırın tamamını vermediği için CSV dışa aktarımında da
// aynı çeviri kullanılabiliyor.

// Otomasyon kaynaklı kayıtların başına backend'in eklediği sabit işaret (bkz.
// SegmentationService/NextBestOfferService).
const AUTOMATION_PREFIX = '[Otomasyon]';

const SEGMENT_ASSIGNED_PATTERN = /^(\S+) \/ baskın kategori: (\S+)$/;
const EMAIL_WITH_ROLE_PATTERN = /^(.+) \(([A-Za-z]+)\)$/;
// OfferDistributed detayı backend'de dilden bağımsız yazılıyor: "<kampanya> / SENT" ya da
// "<kampanya> / SKIPPED: <SEBEP_KODU>" (bkz. OfferDistributionService).
const OFFER_SENT_PATTERN = /^(.+) \/ SENT$/;
const OFFER_SKIPPED_PATTERN = /^(.+) \/ SKIPPED: (.+)$/;

// Bildirimin neden gönderilmediğini açıklayan stabil kodlar (backend: NotificationSkipReasons).
// Tanınmayan bir değer olduğu gibi gösterilir — bu sayede hem veritabanındaki ESKİ Türkçe
// kayıtlar hem de çevrilemez teknik hata metinleri ("SMTP hatası: ...") bozulmadan görünür.
// Türkçe yedekler enum-labels.util.ts'teki aynı gerekçeyle var: çeviri anahtarı bulunamazsa
// kullanıcı ham bir kod ("CONSENT_NOT_GRANTED") yerine anlamlı bir metin görsün.
const SKIP_REASON_FALLBACKS: Record<string, string> = {
  CONSENT_NOT_GRANTED: 'Rıza verilmemiş',
  CUSTOMER_HAS_NO_EMAIL: 'Müşterinin e-posta adresi yok',
  CUSTOMER_HAS_NO_PHONE: 'Müşterinin telefon numarası yok',
  CAMPAIGN_NOT_ACTIVE: 'Kampanya artık aktif değil',
  CHANNEL_NOT_INTEGRATED: 'Bu kanal için henüz gönderim entegrasyonu yok'
};

// Kodlara geçilmeden ÖNCE yazılmış kayıtlar veritabanında düz Türkçe metin olarak duruyor.
// Bunlar koda eşlenmezse, arayüz İngilizce/Almanca'yken geçmiş kayıtlar Türkçe görünmeye
// devam ederdi — yani düzeltme yalnızca yeni veride işe yarardı. Eşleme tek yönlü ve
// yalnızca GÖSTERİM için; veritabanındaki kayıt (denetim izi olduğu için) değiştirilmiyor.
const LEGACY_REASON_CODES: Record<string, string> = {
  'Rıza verilmemiş': 'CONSENT_NOT_GRANTED',
  'Müşterinin e-posta adresi yok.': 'CUSTOMER_HAS_NO_EMAIL',
  'Müşterinin telefon numarası yok.': 'CUSTOMER_HAS_NO_PHONE',
  'Kampanya artık aktif değil.': 'CAMPAIGN_NOT_ACTIVE',
  'Bu kanal için henüz bir gönderim entegrasyonu yok.': 'CHANNEL_NOT_INTEGRATED'
};

export function notificationSkipReasonLabel(reason: string | null | undefined): string {
  if (!reason) return '';

  const code = LEGACY_REASON_CODES[reason] ?? reason;
  return translateOrFallback(`notificationLog.skipReason.${code}`, SKIP_REASON_FALLBACKS[code] ?? code);
}

function isKnown(enumObject: Record<string, string>, value: string): boolean {
  return Object.values(enumObject).includes(value);
}

export function auditDetailLabel(detail: string | null | undefined): string {
  if (!detail) return '';

  const automated = detail.startsWith(AUTOMATION_PREFIX);
  const body = automated ? detail.slice(AUTOMATION_PREFIX.length).trim() : detail;
  const translated = translateDetailBody(body);

  if (!automated) return translated;

  return `[${translateOrFallback('auditLog.detail.automated', 'Otomasyon')}] ${translated}`;
}

function translateDetailBody(body: string): string {
  // ConsentGranted/ConsentRevoked: detay alanı sadece kanal adından ibaret.
  if (isKnown(ConsentChannel, body))
    return consentChannelLabel(body);

  // SegmentAssigned: "<eğilim> / baskın kategori: <kategori>"
  const segmentMatch = SEGMENT_ASSIGNED_PATTERN.exec(body);
  if (segmentMatch && isKnown(SpendingPropensity, segmentMatch[1]) && isKnown(TransactionCategory, segmentMatch[2])) {
    const propensity = spendingPropensityLabel(segmentMatch[1]);
    const category = transactionCategoryLabel(segmentMatch[2]);
    return translateOrFallback(
      'auditLog.detail.segmentAssigned',
      `${propensity} / baskın kategori: ${category}`,
      { propensity, category });
  }

  // OfferDistributed: "<kampanya> / SENT" ya da "<kampanya> / SKIPPED: <SEBEP_KODU>"
  const sentMatch = OFFER_SENT_PATTERN.exec(body);
  if (sentMatch)
    return translateOrFallback('auditLog.detail.offerSent', `${sentMatch[1]} / gönderildi`, { campaign: sentMatch[1] });

  const skippedMatch = OFFER_SKIPPED_PATTERN.exec(body);
  if (skippedMatch) {
    const reason = notificationSkipReasonLabel(skippedMatch[2]);
    return translateOrFallback(
      'auditLog.detail.offerSkipped',
      `${skippedMatch[1]} / atlandı: ${reason}`,
      { campaign: skippedMatch[1], reason });
  }

  // AdminCreated/AdminInvited/AdminInvitationAccepted: "<e-posta> (<rol>)"
  const roleMatch = EMAIL_WITH_ROLE_PATTERN.exec(body);
  if (roleMatch && isKnown(UserRole, roleMatch[2]))
    return `${roleMatch[1]} (${userRoleLabel(roleMatch[2])})`;

  return body;
}
