import { Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { Table, TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { TagModule } from 'primeng/tag';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';

import {
  AUDIT_ACTION_TYPE_OPTIONS,
  AuditActionType,
  AuditLogSummary,
  Client,
  auditActionTypeLabel,
  auditActionTypeSeverity,
  describeApiError,
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
        InputTextModule
    ],
    templateUrl: './audit-log.component.html'
})
export class AuditLogComponent implements OnInit {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);

  // #searchBox p-table'ın "caption" ng-template'i içinde tanımlı — Angular'ın embedded
  // view kapsam kuralı yüzünden template reference variable olarak dışarıdan (ör. filtre
  // barındaki butondan) erişilemiyor, bu yüzden ViewChild ile alınıyor.
  @ViewChild('searchBox') private searchBoxRef?: ElementRef<HTMLInputElement>;

  protected logs = signal<AuditLogSummary[]>([]);
  protected loading = signal(false);

  protected actionTypes = AUDIT_ACTION_TYPE_OPTIONS;
  protected actionTypeFilter: AuditActionType | null = null;
  protected fromUtc: Date | null = null;
  protected toUtc: Date | null = null;

  async ngOnInit(): Promise<void> {
    await this.loadLogs();
  }

  // Müşteri filtresi sunucuya gitmiyor — p-table'ın kendi global filter'ı (arama kutusu)
  // zaten yüklenmiş sonuçlar üzerinde CustomerName'e göre süzüyor, ayrı bir müşteri
  // seçici bileşeni gerekmiyor (customer-list'teki arama deseniyle aynı).
  async loadLogs(): Promise<void> {
    this.loading.set(true);
    try {
      const from = this.fromUtc ? new Date(toUtcDateOnlyIso(this.fromUtc)) : undefined;
      // Bitiş tarihi backend'de "<=" ile karşılaştırılıyor — seçilen günün gece yarısını
      // (00:00 UTC) değil, o günün son anını (23:59:59.999 UTC) göndermek gerekiyor,
      // yoksa seçilen günün neredeyse tamamı yanlışlıkla dışlanır.
      const to = this.toUtc
        ? new Date(Date.UTC(this.toUtc.getFullYear(), this.toUtc.getMonth(), this.toUtc.getDate(), 23, 59, 59, 999))
        : undefined;
      this.logs.set(await firstValueFrom(this.client.auditLogs(undefined, this.actionTypeFilter ?? undefined, from, to)));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Denetim kayıtları yüklenemedi', detail: describeApiError(err) });
    } finally {
      this.loading.set(false);
    }
  }

  // Müşteri arama kutusu ngModel'e bağlı değil (p-table'ın global filter deseni bu
  // şekilde kuruldu) — bu yüzden "Filtreleri Temizle" tablonun/input'un kendisine
  // referansla erişip görsel değeri ve filtre durumunu birlikte sıfırlıyor.
  clearFilters(table: Table): void {
    this.actionTypeFilter = null;
    this.fromUtc = null;
    this.toUtc = null;
    if (this.searchBoxRef) this.searchBoxRef.nativeElement.value = '';
    table.filterGlobal('', 'contains');
    this.loadLogs();
  }

  protected readonly actionTypeLabel = auditActionTypeLabel;
  protected readonly actionTypeSeverity = auditActionTypeSeverity;
}
