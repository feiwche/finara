import { ApiException, ProblemDetails } from '../api/finara-client';
import { apiErrorStatus, describeApiError } from './api-error.util';

describe('describeApiError', () => {
  it('ApiException içindeki JSON gövdenin detail alanını döner', () => {
    const err = new ApiException('mesaj', 404, JSON.stringify({ detail: 'Müşteri bulunamadı.' }), {}, undefined);

    expect(describeApiError(err)).toBe('Müşteri bulunamadı.');
  });

  it('detail yoksa title alanına düşer', () => {
    const err = new ApiException('mesaj', 400, JSON.stringify({ title: 'Geçersiz istek' }), {}, undefined);

    expect(describeApiError(err)).toBe('Geçersiz istek');
  });

  it('gövde JSON değilse ApiException.message döner', () => {
    const err = new ApiException('ham mesaj', 500, 'geçerli olmayan json', {}, undefined);

    expect(describeApiError(err)).toBe('ham mesaj');
  });

  it('ProblemDetails için detail/title döner', () => {
    const err = new ProblemDetails({ detail: 'Yetkisiz.' });

    expect(describeApiError(err)).toBe('Yetkisiz.');
  });

  it('ProblemDetails hiçbir alan yoksa jenerik mesaj döner', () => {
    const err = new ProblemDetails({});

    expect(describeApiError(err)).toBe('Bilinmeyen bir hata oluştu.');
  });

  it('düz Error için message döner', () => {
    expect(describeApiError(new Error('beklenmeyen'))).toBe('beklenmeyen');
  });

  it('bilinmeyen bir değer için String() döner', () => {
    expect(describeApiError('sıradan string')).toBe('sıradan string');
  });
});

describe('apiErrorStatus', () => {
  it('ApiException için status döner', () => {
    const err = new ApiException('mesaj', 404, '{}', {}, undefined);

    expect(apiErrorStatus(err)).toBe(404);
  });

  it('ProblemDetails için status döner', () => {
    const err = new ProblemDetails({ status: 401 });

    expect(apiErrorStatus(err)).toBe(401);
  });

  it('bilinmeyen bir hata için undefined döner', () => {
    expect(apiErrorStatus(new Error('x'))).toBeUndefined();
  });
});
