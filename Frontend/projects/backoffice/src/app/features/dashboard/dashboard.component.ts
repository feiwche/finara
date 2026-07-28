import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';

import { CardModule } from 'primeng/card';
import { ChartModule } from 'primeng/chart';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import {
  AuditActionType,
  Client,
  OfferImpactSummary,
  RealtimeService,
  describeApiError,
  getSpendingPropensityOptions
} from '@finara/shared-ui';

@Component({
    selector: 'app-dashboard',
    imports: [CardModule, ChartModule, TooltipModule, DatePipe, TranslatePipe],
    templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);
  private readonly realtimeService = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly translate = inject(TranslateService);

  protected readonly today = new Date();

  protected loading = signal(false);
  protected totalCustomers = signal(0);
  protected activeCampaigns = signal(0);
  protected offerAccepted = signal(0);
  protected offerDeclined = signal(0);

  // Kural motorunun/AI'ın "gerçekten ne kadar iş yaptığını" gösteren, 30 günle sınırlı
  // olmayan tüm-zamanlar özeti — dashboard'daki anlık KPI'ların ROI hikayesini tamamlıyor.
  protected offerImpact = signal<OfferImpactSummary | null>(null);

  protected chartData = signal<unknown>(null);
  protected readonly chartOptions = {
    plugins: { legend: { position: 'bottom' } },
    responsive: true,
    maintainAspectRatio: false
  };

  // Anlık KPI'ları tamamlayan zaman-bazlı trendler (Adım 3 — raporlama) — yeni
  // /api/reports/* uçlarından, AuditLogs'tan hesaplanıyor (bkz. ReportService).
  protected offerTrendChartData = signal<unknown>(null);
  protected customerGrowthChartData = signal<unknown>(null);
  protected readonly trendChartOptions = {
    plugins: { legend: { position: 'bottom' } },
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: { grid: { display: false } },
      y: { beginAtZero: true, ticks: { precision: 0 } }
    }
  };

  protected get offerTotal(): number {
    return this.offerAccepted() + this.offerDeclined();
  }

  protected get offerAcceptRate(): number | null {
    return this.offerTotal === 0 ? null : Math.round((this.offerAccepted() / this.offerTotal) * 100);
  }

  protected get averageConfidenceScore(): number | null {
    const impact = this.offerImpact();
    return impact && impact.totalOffersGenerated! > 0 ? Math.round(impact.averageConfidenceScore!) : null;
  }

  protected get engagementRate(): number | null {
    const impact = this.offerImpact();
    const total = this.totalCustomers();
    return impact && total > 0 ? Math.round((impact.engagedCustomers! / total) * 100) : null;
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

      const [customers, campaigns, accepted, declined, offerTrend, customerGrowth, offerImpact] = await Promise.all([
        firstValueFrom(this.client.customersAll()),
        firstValueFrom(this.client.campaignsAll()),
        firstValueFrom(this.client.auditLogs(undefined, AuditActionType.CampaignOfferAccepted, thirtyDaysAgo, undefined)),
        firstValueFrom(this.client.auditLogs(undefined, AuditActionType.CampaignOfferDeclined, thirtyDaysAgo, undefined)),
        firstValueFrom(this.client.offerTrend(30)),
        firstValueFrom(this.client.customerGrowth(30)),
        firstValueFrom(this.client.offerImpact())
      ]);

      this.totalCustomers.set(customers.length);
      this.activeCampaigns.set(campaigns.filter(c => c.status === 'Active').length);
      this.offerAccepted.set(accepted.length);
      this.offerDeclined.set(declined.length);
      this.offerImpact.set(offerImpact);

      // getSpendingPropensityOptions() burada anlık dile göre (yükleme/olay tetiklendiği
      // andaki dil) hesaplanıyor — grafik SADECE loadDashboard() her çağrıldığında yeniden
      // çiziliyor (bkz. ngOnInit + campaignUpdated$), dil değişimine kendi başına tepki
      // vermiyor; bu, sayfa yeniden yüklenene/veri tazelenene kadar süregelen ayrı,
      // önceden var olan bir sınırlama (settings-page.component.ts'teki desen burada
      // kasıtlı olarak tekrarlanmadı — chart sadece veri yenilenince yeniden inşa ediliyor).
      const propensityOptions = getSpendingPropensityOptions();
      const counts: Record<string, number> = {};
      for (const option of propensityOptions) counts[option.value] = 0;
      for (const customer of customers) {
        if (customer.propensity) counts[customer.propensity] = (counts[customer.propensity] ?? 0) + 1;
      }

      this.chartData.set({
        labels: propensityOptions.map(o => o.label),
        datasets: [
          {
            data: propensityOptions.map(o => counts[o.value]),
            // Sırasıyla NeedsEngagement/Neutral/HighlyResponsive — severity.util.ts'teki
            // propensitySeverity (warn/info/success) ile aynı anlam, grafik renkleri de eşit.
            backgroundColor: ['#f59e0b', '#60a5fa', '#22c55e']
          }
        ]
      });

      const dayLabels = offerTrend.map(p => this.formatShortDate(p.date!));
      this.offerTrendChartData.set({
        labels: dayLabels,
        datasets: [
          {
            label: this.translate.instant('dashboard.charts.accepted'),
            data: offerTrend.map(p => p.accepted),
            borderColor: '#227E67',
            backgroundColor: 'rgba(34, 126, 103, 0.12)',
            tension: 0.35,
            fill: true
          },
          {
            label: this.translate.instant('dashboard.charts.declined'),
            data: offerTrend.map(p => p.declined),
            borderColor: '#B8752C',
            backgroundColor: 'rgba(184, 117, 44, 0.12)',
            tension: 0.35,
            fill: true
          }
        ]
      });

      this.customerGrowthChartData.set({
        labels: customerGrowth.map(p => this.formatShortDate(p.date!)),
        datasets: [
          {
            label: this.translate.instant('dashboard.charts.newCustomers'),
            data: customerGrowth.map(p => p.newCustomers),
            borderColor: '#175243',
            backgroundColor: 'rgba(23, 82, 67, 0.12)',
            tension: 0.35,
            fill: true
          }
        ]
      });
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('dashboard.loadError'), detail: describeApiError(err) });
    } finally {
      this.loading.set(false);
    }
  }

  private formatShortDate(value: Date | string): string {
    const date = new Date(value);
    return date.toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit' });
  }
}
