import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { TagModule } from 'primeng/tag';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import {
  AUDIT_ACTION_TYPE_OPTIONS,
  AuditActionType,
  AuditLogSummary,
  Client,
  auditActionTypeLabel,
  auditActionTypeSeverity,
  describeApiError,
  sanitizeCsvField,
  toUtcDateOnlyIso
} from '@finara/shared-ui';

@Component({
    selector: 'app-audit-log',
    imports: [
        DatePipe,
        FormsModule,
        TableModule,
        ButtonModule,
        SelectModule,
        DatePickerModule,
        TagModule,
        IconFieldModule,
        InputIconModule,
        InputTextModule,
        TooltipModule
    ],
    templateUrl: './audit-log.component.html'
})
export class AuditLogComponent implements OnInit, OnDestroy {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);

  protected readonly pageSize = 20;

  protected logs = signal<AuditLogSummary[]>([]);
  protected totalRecords = signal(0);
  protected loading = signal(false);

  protected actionTypes = AUDIT_ACTION_TYPE_OPTIONS;
  protected actionTypeFilter: AuditActionType | null = null;
  protected fromUtc: Date | null = null;
  protected toUtc: Date | null = null;
  protected searchTerm = '';

  // p-table'a [first]="tableFirst()" ile geri bağlanıyor — bkz. customer-list.component.ts'teki
  // aynı düzeltmenin yorumu (paginator'ın kendi iç sayfa state'i, veri sıfırlamasından
  // bağımsız kalıp eski sayfayı seçili göstermeye devam ediyordu).
  protected tableFirst = signal(0);

  private lastLazyEvent: TableLazyLoadEvent = { first: 0, rows: this.pageSize };
  private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;
  // bkz. customer-list.component.ts — debounce'lu arama ile paginator/filtre tetiklemeli
  // istekler arasındaki yarış durumunu önlemek için istek sıralama koruması.
  private requestSequence = 0;

  async ngOnInit(): Promise<void> {
    // Tablo satırları burada değil — [lazy]="true" p-table, ilk render'da (onLazyLoad)
    // ile kendiliğinden tetikliyor.
  }

  ngOnDestroy(): void {
    if (this.searchDebounceHandle) clearTimeout(this.searchDebounceHandle);
  }

  // Müşteri adı araması artık sunucuya gidiyor (bkz. GET /api/audit-logs/paged?search=) —
  // önceden p-table'ın client-side global filter'ıydı, sayfalamayla birlikte bu artık
  // sadece görünen sayfada arama yapardı (yanlış/eksik sonuç).
  async loadLogs(event?: TableLazyLoadEvent): Promise<void> {
    if (event) this.lastLazyEvent = event;
    this.tableFirst.set(this.lastLazyEvent.first ?? 0);

    const requestId = ++this.requestSequence;
    this.loading.set(true);
    try {
      const from = this.fromUtc ? new Date(toUtcDateOnlyIso(this.fromUtc)) : undefined;
      // Bitiş tarihi backend'de "<=" ile karşılaştırılıyor — seçilen günün gece yarısını
      // (00:00 UTC) değil, o günün son anını (23:59:59.999 UTC) göndermek gerekiyor,
      // yoksa seçilen günün neredeyse tamamı yanlışlıkla dışlanır.
      const to = this.toUtc
        ? new Date(Date.UTC(this.toUtc.getFullYear(), this.toUtc.getMonth(), this.toUtc.getDate(), 23, 59, 59, 999))
        : undefined;
      const first = this.lastLazyEvent.first ?? 0;
      const page = Math.floor(first / this.pageSize) + 1;
      const result = await firstValueFrom(
        this.client.paged(undefined, this.actionTypeFilter ?? undefined, from, to, this.searchTerm || undefined, page, this.pageSize)
      );
      if (requestId !== this.requestSequence) return;
      this.logs.set(result.items ?? []);
      this.totalRecords.set(result.totalCount ?? 0);
    } catch (err) {
      if (requestId !== this.requestSequence) return;
      this.messageService.add({ severity: 'error', summary: 'Denetim kayıtları yüklenemedi', detail: describeApiError(err) });
    } finally {
      if (requestId === this.requestSequence) this.loading.set(false);
    }
  }

  private resetToFirstPage(): void {
    this.lastLazyEvent = { ...this.lastLazyEvent, first: 0 };
  }

  onFilterChange(): void {
    this.resetToFirstPage();
    void this.loadLogs();
  }

  onSearchInput(): void {
    if (this.searchDebounceHandle) clearTimeout(this.searchDebounceHandle);
    this.searchDebounceHandle = setTimeout(() => {
      this.searchDebounceHandle = null;
      this.resetToFirstPage();
      void this.loadLogs();
    }, 300);
  }

  clearFilters(): void {
    this.actionTypeFilter = null;
    this.fromUtc = null;
    this.toUtc = null;
    this.searchTerm = '';
    this.onFilterChange();
  }

  protected readonly actionTypeLabel = auditActionTypeLabel;
  protected readonly actionTypeSeverity = auditActionTypeSeverity;

  // p-table'ın yerleşik exportCSV()'i için — aksiyon tipi ham İngilizce yerine
  // Türkçe etikete, tarih okunur bir formata çevriliyor.
  protected readonly exportColumns = [
    { field: 'occurredAtUtc', header: 'Tarih' },
    { field: 'customerName', header: 'Müşteri' },
    { field: 'actionType', header: 'Aksiyon' },
    { field: 'details', header: 'Detay' }
  ];

  protected exportFn = (event: { data: unknown; field: string }): string => {
    if (event.data == null) return '';
    switch (event.field) {
      case 'actionType':
        return auditActionTypeLabel(event.data as string);
      case 'occurredAtUtc':
        return new Date(event.data as string).toLocaleString('tr-TR');
      default:
        return sanitizeCsvField(String(event.data));
    }
  };
}
