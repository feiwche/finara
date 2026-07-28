import { TranslateService } from '@ngx-translate/core';
import { ApiException, ProblemDetails } from '../api/finara-client';

// Backend'in GlobalExceptionHandler'ı, gerçek kullanıcı akışlarında düzenli karşılaşılan iş
// kuralı ihlallerinde (ör. "bu e-posta zaten kullanımda") artık ProblemDetails.Extensions
// içinde stabil bir "errorCode" (+ gerekirse "errorParams") dışa veriyor — Message alanı hâlâ
// hep Türkçe (loglama/geriye dönük uyumluluk için), o yüzden describeApiError() ARTIK Message'ı
// DEĞİL, kod varsa errors.<code> çeviri anahtarını tercih ediyor (yoksa Message'a düşüyor —
// backend'de kod eklenmemiş ~70 düşük görünürlüklü alan validasyonu için beklenen davranış,
// bkz. Finara.Domain.Common.ErrorCodes'daki kapsam notu). enum-labels.util.ts'teki
// registerTranslateServiceForEnumLabels ile AYNI desen — modül seviyesinde tek bir kayıt,
// düzinelerce mevcut describeApiError(err) çağrı noktasını değiştirmeye gerek kalmadan.
let translateServiceRef: TranslateService | null = null;

export function registerTranslateServiceForApiErrors(translate: TranslateService): void {
  translateServiceRef = translate;
}

function tryTranslateErrorCode(code: unknown, params: unknown): string | undefined {
  if (typeof code !== 'string' || !translateServiceRef) return undefined;
  const key = `errors.${code}`;
  const translated = translateServiceRef.instant(key, (params as Record<string, unknown>) ?? undefined);
  return translated && translated !== key ? translated : undefined;
}

export function describeApiError(err: unknown): string {
  if (err instanceof ApiException) {
    try {
      const parsed = JSON.parse(err.response);
      return tryTranslateErrorCode(parsed.errorCode, parsed.errorParams) ?? parsed.detail ?? parsed.title ?? err.message;
    } catch {
      return err.message;
    }
  }
  if (err instanceof ProblemDetails) {
    const errAny = err as unknown as { errorCode?: unknown; errorParams?: unknown };
    return tryTranslateErrorCode(errAny.errorCode, errAny.errorParams) ?? err.detail ?? err.title ?? 'Bilinmeyen bir hata oluştu.';
  }
  return err instanceof Error ? err.message : String(err);
}

export function apiErrorStatus(err: unknown): number | undefined {
  if (err instanceof ApiException) return err.status;
  if (err instanceof ProblemDetails) return err.status;
  return undefined;
}
