import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { TableModule } from 'primeng/table';
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
  standalone: true,
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
      const to = this.toUtc ? new Date(toUtcDateOnlyIso(this.toUtc)) : undefined;
      this.logs.set(await firstValueFrom(this.client.auditLogs(undefined, this.actionTypeFilter ?? undefined, from, to)));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Denetim kayıtları yüklenemedi', detail: describeApiError(err) });
    } finally {
      this.loading.set(false);
    }
  }

  clearFilters(): void {
    this.actionTypeFilter = null;
    this.fromUtc = null;
    this.toUtc = null;
    this.loadLogs();
  }

  protected readonly actionTypeLabel = auditActionTypeLabel;
  protected readonly actionTypeSeverity = auditActionTypeSeverity;
}
