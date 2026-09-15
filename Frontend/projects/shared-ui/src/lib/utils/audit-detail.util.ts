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

  // AdminCreated/AdminInvited/AdminInvitationAccepted: "<e-posta> (<rol>)"
  const roleMatch = EMAIL_WITH_ROLE_PATTERN.exec(body);
  if (roleMatch && isKnown(UserRole, roleMatch[2]))
    return `${roleMatch[1]} (${userRoleLabel(roleMatch[2])})`;

  return body;
}
