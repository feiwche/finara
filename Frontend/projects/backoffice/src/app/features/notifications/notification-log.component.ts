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
  CONSENT_CHANNEL_OPTIONS,
  Client,
  ConsentChannel,
  NotificationLogSummary,
  consentChannelLabel,
  describeApiError,
  toUtcDateOnlyIso
} from '@finara/shared-ui';

@Component({
  selector: 'app-notification-log',
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
  templateUrl: './notification-log.component.html'
})
export class NotificationLogComponent implements OnInit {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);

  // Audit Log ekranındaki aynı desen (bkz. audit-log.component.ts) — #searchBox
  // p-table'ın "caption" ng-template'i içinde tanımlı olduğu için dışarıdan erişim
  // için ViewChild gerekiyor.
  @ViewChild('searchBox') private searchBoxRef?: ElementRef<HTMLInputElement>;

  protected logs = signal<NotificationLogSummary[]>([]);
  protected loading = signal(false);

  protected channels = CONSENT_CHANNEL_OPTIONS;
  protected channelFilter: ConsentChannel | null = null;
  protected fromUtc: Date | null = null;
  protected toUtc: Date | null = null;

  protected readonly consentChannelLabel = consentChannelLabel;

  async ngOnInit(): Promise<void> {
    await this.loadLogs();
  }

  async loadLogs(): Promise<void> {
    this.loading.set(true);
    try {
      const from = this.fromUtc ? new Date(toUtcDateOnlyIso(this.fromUtc)) : undefined;
      const to = this.toUtc
        ? new Date(Date.UTC(this.toUtc.getFullYear(), this.toUtc.getMonth(), this.toUtc.getDate(), 23, 59, 59, 999))
        : undefined;
      this.logs.set(await firstValueFrom(this.client.notifications(undefined, this.channelFilter ?? undefined, from, to)));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Bildirim kayıtları yüklenemedi', detail: describeApiError(err) });
    } finally {
      this.loading.set(false);
    }
  }

  clearFilters(table: Table): void {
    this.channelFilter = null;
    this.fromUtc = null;
    this.toUtc = null;
    if (this.searchBoxRef) this.searchBoxRef.nativeElement.value = '';
    table.filterGlobal('', 'contains');
    this.loadLogs();
  }
}
