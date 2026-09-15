import { Mocked, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';

import { CustomerListComponent } from './customer-list.component';
import { AuthService, Client, CustomerImportResult, CustomerSummaryPagedResult, UiLanguage } from '@finara/shared-ui';

// Enum seçenekleri/langTick artık TranslateService'e bağlı (bkz. component'teki langTick
// signal deseni) — auth.service.spec.ts'teki aynı sahte servis, sadece anahtarı olduğu gibi
// geri döndürüyor (gerçek çeviri metnini test etmiyoruz, sadece DI'ın çökmemesini istiyoruz).
const translateStub = {
  instant: (key: string) => key,
  onLangChange: { subscribe: () => ({ unsubscribe: () => {} }) },
  currentLang: () => 'tr'
};

describe('CustomerListComponent', () => {
  let client: Mocked<Pick<Client, 'customers' | 'paged2' | 'stats' | 'import'>>;
  let router: Mocked<Pick<Router, 'navigate'>>;
  let authService: Partial<AuthService>;
  let component: CustomerListComponent;

  beforeEach(() => {
    client = {
      customers: vi.fn(),
      paged2: vi.fn(),
      stats: vi.fn(),
      import: vi.fn()
    };
    router = { navigate: vi.fn() };
    authService = { canWrite: signal(true), isDemoAccount: signal(false) } as unknown as Partial<AuthService>;

    TestBed.configureTestingModule({
      providers: [
        CustomerListComponent,
        { provide: Client, useValue: client },
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: authService },
        { provide: TranslateService, useValue: translateStub },
        MessageService
      ]
    });
    component = TestBed.inject(CustomerListComponent);
  });

  it('initials() ad ve soyadın baş harflerini büyük harfle döner', () => {
    expect(component['initials']({ firstName: 'ayşe', lastName: 'yılmaz' } as any)).toBe('AY');
  });

  it('initials() eksik ad/soyad ile de çökmez', () => {
    expect(component['initials']({ firstName: undefined, lastName: undefined } as any)).toBe('');
  });

  it('exportFn() eğilim/eğitim durumu enum değerlerini Türkçe etikete çevirir', () => {
    expect(component['exportFn']({ data: 'HighlyResponsive', field: 'propensity' })).toBe('Harcamaya Yatkın');
    expect(component['exportFn']({ data: 'Lisans', field: 'educationLevel' })).toBe('Lisans');
  });

  it('exportFn() bilinmeyen alanlarda ham değeri CSV için sanitize eder', () => {
    expect(component['exportFn']({ data: '=cmd', field: 'email' })).toBe("'=cmd");
  });

  it('exportFn() null veri için boş string döner', () => {
    expect(component['exportFn']({ data: null, field: 'email' })).toBe('');
  });

  it('openDetail() müşteri detay sayfasına yönlendirir', () => {
    component.openDetail({ id: 'abc-123' } as any);

    expect(router.navigate).toHaveBeenCalledWith(['/customers', 'abc-123']);
  });

  it('loadCustomers() sunucudan gelen sayfayı customers/totalRecords sinyallerine yazar', async () => {
    client.paged2.mockReturnValue(of(new CustomerSummaryPagedResult({ items: [{ id: '1' } as any], totalCount: 1 })));

    await component.loadCustomers();

    expect(component['customers']()).toHaveLength(1);
    expect(component['totalRecords']()).toBe(1);
    expect(component['loading']()).toBe(false);
  });

  it('loadCustomers() hata durumunda toast gösterir ve loading durumunu kapatır', async () => {
    client.paged2.mockReturnValue(throwError(() => new Error('boom')));
    const messageService = TestBed.inject(MessageService);
    const addSpy = vi.spyOn(messageService, 'add');

    await component.loadCustomers();

    expect(addSpy).toHaveBeenCalledWith(expect.objectContaining({ severity: 'error' }));
    expect(component['loading']()).toBe(false);
  });

  it('createCustomer() zorunlu alanlar eksikken API çağırmadan geri döner', async () => {
    await component.createCustomer();

    expect(client.customers).not.toHaveBeenCalled();
    expect(component['createSubmitted']).toBe(true);
  });

  it("createCustomer() geçerli veriyle API'yi çağırır ve dialogu kapatır", async () => {
    component['newCustomer'] = {
      firstName: 'Ayşe',
      lastName: 'Yılmaz',
      email: 'ayse@example.com',
      phoneNumber: '',
      dateOfBirth: new Date('1990-01-01'),
      hasInvestmentAccount: false,
      hasInternationalTransfers: false,
      educationLevel: null,
      preferredLanguage: UiLanguage.Turkish
    };
    client.customers.mockReturnValue(of({} as any));
    client.paged2.mockReturnValue(of(new CustomerSummaryPagedResult({ items: [], totalCount: 0 })));
    client.stats.mockReturnValue(of({} as any));

    await component.createCustomer();

    expect(client.customers).toHaveBeenCalled();
    expect(component['dialogVisible']()).toBe(false);
  });

  it('onImportFileSelected() dosya seçilmediyse hiçbir şey yapmaz', async () => {
    const event = { target: { files: [], value: '' } } as unknown as Event;

    await component.onImportFileSelected(event);

    expect(client.import).not.toHaveBeenCalled();
  });

  it('onImportFileSelected() dosya seçilince içe aktarma sonucunu gösterir', async () => {
    const file = new File(['content'], 'musteriler.csv');
    const event = { target: { files: [file], value: '' } } as unknown as Event;
    client.import.mockReturnValue(of(new CustomerImportResult({ totalRows: 1, successCount: 0, errors: [] })));

    await component.onImportFileSelected(event);

    expect(component['importResultDialogVisible']()).toBe(true);
    expect(component['importResult']()?.totalRows).toBe(1);
  });
});
