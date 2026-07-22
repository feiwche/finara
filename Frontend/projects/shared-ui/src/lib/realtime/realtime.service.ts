import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { API_BASE_URL, CampaignSummary, NotificationLogSummary } from '../api/finara-client';
import { AuthService } from '../auth/auth.service';

// Backend'deki NotificationsHub'a (bkz. Finara.Api/Realtime) tek yönlü bir dinleyici —
// istemci hub'da hiçbir metot çağırmıyor, sadece kampanya durumu değişince/teklif
// dağıtılınca gelen olayları dinliyor. AppShellComponent (client+backoffice) giriş
// yapılınca connect(), çıkış yapılınca disconnect() çağırır.
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly authService = inject(AuthService);
  private readonly apiBaseUrl = inject(API_BASE_URL, { optional: true }) ?? '';

  private connection: HubConnection | null = null;

  private readonly campaignUpdatedSubject = new Subject<CampaignSummary>();
  private readonly offerDistributedSubject = new Subject<NotificationLogSummary>();

  readonly campaignUpdated$ = this.campaignUpdatedSubject.asObservable();
  readonly offerDistributed$ = this.offerDistributedSubject.asObservable();

  async connect(): Promise<void> {
    if (this.connection) return;

    const connection = new HubConnectionBuilder()
      .withUrl(`${this.apiBaseUrl}/hubs/notifications`, {
        accessTokenFactory: () => this.authService.accessToken ?? ''
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('campaignUpdated', (campaign: CampaignSummary) => this.campaignUpdatedSubject.next(campaign));
    connection.on('offerDistributed', (log: NotificationLogSummary) => this.offerDistributedSubject.next(log));

    this.connection = connection;
    try {
      await connection.start();
    } catch {
      // Sessizce başarısız olsun — canlı bildirim "nice-to-have", bağlantı kurulamazsa
      // uygulamanın geri kalanı (manuel yenileme/liste yükleme) hâlâ olduğu gibi çalışır.
      this.connection = null;
    }
  }

  async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    await connection?.stop();
  }
}
