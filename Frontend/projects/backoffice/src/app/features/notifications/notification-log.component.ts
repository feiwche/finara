import { Component, DestroyRef, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
  RealtimeService,
  consentChannelLabel,
  describeApiError,
  sanitizeCsvField,
  toUtcDateOnlyIso
} from '@finara/shared-ui';

@Component({
    selector: 'app-notification-log',
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
  private readonly realtimeService = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

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

    // Başka bir sekmede/kullanıcı tarafından bir teklif dağıtıldığında, listeyi elle
    // yenilemeye gerek kalmadan burada da görünsün diye (mevcut filtreler korunarak).
    this.realtimeService.offerDistributed$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(log => {
      this.messageService.add({
        severity: 'info',
        summary: 'Yeni bildirim',
        detail: `${consentChannelLabel(log.channel!)} · ${log.customerName}`
      });
      void this.loadLogs();
    });
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

  // p-table'ın yerleşik exportCSV()'i için — kanal ham İngilizce yerine Türkçe
  // etikete, "sent" boolean'ı okunur bir duruma çevriliyor. exportFunction sadece
  // {data, field} alıyor (satırın tamamına erişim yok), bu yüzden "Durum" ve
  // "Atlanma Sebebi" tek bir birleşik metin yerine ayrı sütunlar olarak tutuldu.
  protected readonly exportColumns = [
    { field: 'sentAtUtc', header: 'Tarih' },
    { field: 'customerName', header: 'Müşteri' },
    { field: 'channel', header: 'Kanal' },
    { field: 'sent', header: 'Durum' },
    { field: 'skippedReason', header: 'Atlanma Sebebi' },
    { field: 'message', header: 'Mesaj' }
  ];

  protected exportFn = (event: { data: unknown; field: string }): string => {
    switch (event.field) {
      case 'channel':
        return event.data == null ? '' : consentChannelLabel(event.data as string);
      case 'sentAtUtc':
        return event.data == null ? '' : new Date(event.data as string).toLocaleString('tr-TR');
      case 'sent':
        return event.data ? 'Gönderildi' : 'Atlandı';
      default:
        return event.data == null ? '' : sanitizeCsvField(String(event.data));
    }
  };
}
