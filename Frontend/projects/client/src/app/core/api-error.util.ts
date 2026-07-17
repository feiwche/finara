import { ApiException } from '../api/finara-client';

export function describeApiError(err: unknown): string {
  if (err instanceof ApiException) {
    try {
      const parsed = JSON.parse(err.response);
      return parsed.detail ?? parsed.title ?? err.message;
    } catch {
      return err.message;
    }
  }
  return err instanceof Error ? err.message : String(err);
}
