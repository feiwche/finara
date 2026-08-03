import { Injectable, NgZone, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { API_BASE_URL, CampaignSummary, NotificationLogSummary } from '../api/finara-client';
import { AuthService } from '../auth/auth.service';

// Backend'deki NotificationsHub'a (bkz. Finara.Api/Realtime) tek yönlü bir dinleyici —
// istemci hub'da hiçbir metot çağırmıyor, sadece kampanya durumu değişince/teklif
// dağıtılınca gelen olayları dinliyor. AppShellComponent (client+backoffice) giriş
// yapılınca connect(), çıkış yapılınca disconnect() çağırır.
//
// KRİTİK — bulundu (canlı testte): SignalR'ın kendi otomatik yeniden bağlanma/heartbeat
// zamanlayıcıları Angular'ın zone'u İÇİNDE çalışırsa, NgZone hiçbir zaman "stabil" duruma
// geçemiyor (NG0506 hydration uyarısı) — bu da PrimeNG'in dinamik olarak (overlay/CDK ile)
// oluşturduğu menülerin (ör. BackOffice'teki "Yönetim" açılır menüsü) tıklama olaylarının
// hiç tetiklenmemesine yol açıyordu (menü açılıyor ama içindeki öğelere tıklanınca hiçbir şey
// olmuyordu). Çözüm: bağlantı kurulumu/olay dinleme runOutsideAngular() içinde yapılıyor,
// sadece gerçekten bir olay gelince (Subject.next çağrısı, component'lerin abone olduğu ve
// change detection tetiklemesi gereken tek an) zone'a geri dönülüyor (ngZone.run).
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly authService = inject(AuthService);
  private readonly apiBaseUrl = inject(API_BASE_URL, { optional: true }) ?? '';
  private readonly ngZone = inject(NgZone);

  private connection: HubConnection | null = null;

  private readonly campaignUpdatedSubject = new Subject<CampaignSummary>();
  private readonly offerDistributedSubject = new Subject<NotificationLogSummary>();

  readonly campaignUpdated$ = this.campaignUpdatedSubject.asObservable();
  readonly offerDistributed$ = this.offerDistributedSubject.asObservable();

  async connect(): Promise<void> {
    if (this.connection) return;

    await this.ngZone.runOutsideAngular(async () => {
      const connection = new HubConnectionBuilder()
        .withUrl(`${this.apiBaseUrl}/hubs/notifications`, {
          accessTokenFactory: () => this.authService.accessToken ?? ''
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on('campaignUpdated', (campaign: CampaignSummary) =>
        this.ngZone.run(() => this.campaignUpdatedSubject.next(campaign)));
      connection.on('offerDistributed', (log: NotificationLogSummary) =>
        this.ngZone.run(() => this.offerDistributedSubject.next(log)));

      this.connection = connection;
      try {
        await connection.start();
      } catch {
        // Sessizce başarısız olsun — canlı bildirim "nice-to-have", bağlantı kurulamazsa
        // uygulamanın geri kalanı (manuel yenileme/liste yükleme) hâlâ olduğu gibi çalışır.
        this.connection = null;
      }
    });
  }

  async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    await connection?.stop();
  }
}
