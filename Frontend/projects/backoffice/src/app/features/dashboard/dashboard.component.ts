import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';

import { CardModule } from 'primeng/card';
import { ChartModule } from 'primeng/chart';
import { MessageService } from 'primeng/api';

import {
  AuditActionType,
  Client,
  RealtimeService,
  SPENDING_PROPENSITY_OPTIONS,
  describeApiError
} from '@finara/shared-ui';

@Component({
    selector: 'app-dashboard',
    imports: [CardModule, ChartModule, DatePipe],
    templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);
  private readonly realtimeService = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly today = new Date();

  protected loading = signal(false);
  protected totalCustomers = signal(0);
  protected activeCampaigns = signal(0);
  protected offerAccepted = signal(0);
  protected offerDeclined = signal(0);

  protected chartData = signal<unknown>(null);
  protected readonly chartOptions = {
    plugins: { legend: { position: 'bottom' } },
    responsive: true,
    maintainAspectRatio: false
  };

  protected get offerTotal(): number {
    return this.offerAccepted() + this.offerDeclined();
  }

  protected get offerAcceptRate(): number | null {
    return this.offerTotal === 0 ? null : Math.round((this.offerAccepted() / this.offerTotal) * 100);
  }

  async ngOnInit(): Promise<void> {
    await this.loadDashboard();

    // Başka bir sekmede/kullanıcı tarafından bir kampanya aktifleştirilince/duraklatılınca
    // "Aktif Kampanya" KPI'ı elle yenilemeye gerek kalmadan güncellensin diye.
    this.realtimeService.campaignUpdated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      void this.loadDashboard();
    });
  }

  // Henüz toplu bir istatistik ucu yok (bkz. plan notu — v1'de erken optimizasyon
  // yapılmıyor). Müşteri/kampanya listeleri zaten toplu geliyor; eğilim dağılımı
  // CustomerSummary.propensity üzerinden (segment endpoint'i per-customer olduğu için
  // N+1'den kaçınıldı), teklif kabul/red oranı ise Faz 7.2'de eklenen Denetim Kaydı
  // uçlarından (actionType filtresiyle) — ikisi de tek seferde, sunucu tarafında süzülüyor.
  async loadDashboard(): Promise<void> {
    this.loading.set(true);
    try {
      const thirtyDaysAgo = new Date();
      thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);

      const [customers, campaigns, accepted, declined] = await Promise.all([
        firstValueFrom(this.client.customersAll()),
        firstValueFrom(this.client.campaignsAll()),
        firstValueFrom(this.client.auditLogs(undefined, AuditActionType.CampaignOfferAccepted, thirtyDaysAgo, undefined)),
        firstValueFrom(this.client.auditLogs(undefined, AuditActionType.CampaignOfferDeclined, thirtyDaysAgo, undefined))
      ]);

      this.totalCustomers.set(customers.length);
      this.activeCampaigns.set(campaigns.filter(c => c.status === 'Active').length);
      this.offerAccepted.set(accepted.length);
      this.offerDeclined.set(declined.length);

      const counts: Record<string, number> = {};
      for (const option of SPENDING_PROPENSITY_OPTIONS) counts[option.value] = 0;
      for (const customer of customers) {
        if (customer.propensity) counts[customer.propensity] = (counts[customer.propensity] ?? 0) + 1;
      }

      this.chartData.set({
        labels: SPENDING_PROPENSITY_OPTIONS.map(o => o.label),
        datasets: [
          {
            data: SPENDING_PROPENSITY_OPTIONS.map(o => counts[o.value]),
            // Sırasıyla NeedsEngagement/Neutral/HighlyResponsive — severity.util.ts'teki
            // propensitySeverity (warn/info/success) ile aynı anlam, grafik renkleri de eşit.
            backgroundColor: ['#f59e0b', '#60a5fa', '#22c55e']
          }
        ]
      });
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Panel yüklenemedi', detail: describeApiError(err) });
    } finally {
      this.loading.set(false);
    }
  }
}
