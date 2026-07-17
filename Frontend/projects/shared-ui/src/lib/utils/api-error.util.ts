import { ApiException, ProblemDetails } from '../api/finara-client';

// NSwag'ın ürettiği istemci, bir uçta .ProducesProblem(...) ile tipli bir hata yanıtı
// (ör. 404) tanımlıysa ApiException DEĞİL, ham ProblemDetails nesnesini fırlatıyor
// (bkz. finara-client.ts'teki throwException: result tanımlıysa onu, değilse
// ApiException'ı fırlatır). Bunu ele almazsak String(err) "[object Object]" üretiyordu —
// backend'in tüm 404'leri (müşteri/kampanya/teklif/segment bulunamadı vb.) etkileniyordu.
export function describeApiError(err: unknown): string {
  if (err instanceof ApiException) {
    try {
      const parsed = JSON.parse(err.response);
      return parsed.detail ?? parsed.title ?? err.message;
    } catch {
      return err.message;
    }
  }
  if (err instanceof ProblemDetails) {
    return err.detail ?? err.title ?? 'Bilinmeyen bir hata oluştu.';
  }
  return err instanceof Error ? err.message : String(err);
}

export function apiErrorStatus(err: unknown): number | undefined {
  if (err instanceof ApiException) return err.status;
  if (err instanceof ProblemDetails) return err.status;
  return undefined;
}
