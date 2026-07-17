import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CurrencyPipe, DatePipe, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { firstValueFrom } from 'rxjs';

import { TabViewModule } from 'primeng/tabview';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { TagModule } from 'primeng/tag';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { ProgressBarModule } from 'primeng/progressbar';
import { TextareaModule } from 'primeng/textarea';
import { BreadcrumbModule } from 'primeng/breadcrumb';
import { ConfirmationService, MenuItem, MessageService } from 'primeng/api';

import {
  AiInsightRequest,
  AiInsightResult,
  Client,
  CustomerSummary,
  OfferSummary,
  RecordTransactionRequest,
  SegmentSummary,
  TransactionCategory,
  TransactionSummary
} from '../../api/finara-client';
import { apiErrorStatus, describeApiError } from '../../core/api-error.util';
import { toUtcDateOnlyIso } from '../../core/date.util';
import { offerResponseSeverity } from '../../core/severity.util';
import { TRANSACTION_CATEGORY_OPTIONS, offerResponseLabel, spendingPropensityLabel, transactionCategoryLabel } from '../../core/enum-labels.util';

const CATEGORY_ICONS: Record<string, string> = {
  Groceries: 'pi-shopping-cart',
  Fuel: 'pi-bolt',
  Travel: 'pi-send',
  Entertainment: 'pi-star',
  Healthcare: 'pi-heart',
  ECommerce: 'pi-shopping-bag',
  Motorcycle: 'pi-car',
  Investment: 'pi-chart-line',
  ForeignExchange: 'pi-dollar',
  Utilities: 'pi-home',
  Other: 'pi-tag'
};

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [
    CurrencyPipe,
    DatePipe,
    NgClass,
    FormsModule,
    TabViewModule,
    TableModule,
    ButtonModule,
    CardModule,
    MessageModule,
    TagModule,
    InputTextModule,
    InputNumberModule,
    SelectModule,
    DatePickerModule,
    ProgressSpinnerModule,
    ProgressBarModule,
    TextareaModule,
    BreadcrumbModule
  ],
  templateUrl: './customer-detail.component.html'
})
export class CustomerDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly client = inject(Client);
  private readonly destroyRef = inject(DestroyRef);
  private readonly titleService = inject(Title);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly offerResponseSeverity = offerResponseSeverity;
  protected readonly offerResponseLabel = offerResponseLabel;
  protected readonly transactionCategoryLabel = transactionCategoryLabel;
  protected readonly spendingPropensityLabel = spendingPropensityLabel;

  protected categoryIcon(category: string): string {
    return CATEGORY_ICONS[category] ?? 'pi-tag';
  }

  protected readonly breadcrumbHome: MenuItem = { icon: 'pi pi-home', routerLink: '/customers' };
  protected readonly breadcrumbItems = computed<MenuItem[]>(() => {
    const c = this.customer();
    return [{ label: 'Müşteriler', routerLink: '/customers' }, { label: c ? `${c.firstName} ${c.lastName}` : 'Müşteri' }];
  });

  protected customerId = '';
  protected customer = signal<CustomerSummary | null>(null);

  protected segment = signal<SegmentSummary | null>(null);
  protected segmentMessage = signal<string | null>(null);

  protected transactions = signal<TransactionSummary[]>([]);
  protected categories = TRANSACTION_CATEGORY_OPTIONS;
  protected newTransaction = { amount: 0, category: TransactionCategory.Groceries, merchantName: '', occurredAtUtc: null as Date | null };
  protected txBusy = signal(false);
  protected txSubmitted = false;

  protected offers = signal<OfferSummary[]>([]);
  protected offersBusy = signal(false);
  protected respondingOfferId = signal<string | null>(null);

  protected aiQuestion = '';
  protected aiAnswer = signal<AiInsightResult | null>(null);
  protected aiBusy = signal(false);
  protected aiError = signal<string | null>(null);

  ngOnInit(): void {
    // route.snapshot yerine paramMap'e abone oluyoruz: /customers/:id aynı route
    // tanımına eşleştiği için Angular Router, A'dan B'ye geçişte bu component'i
    // YENİDEN OLUŞTURMUYOR — sadece param değişiyor. snapshot kullansaydık ngOnInit
    // bir daha çalışmaz, sayfa eski müşterinin verisini göstermeye devam ederdi.
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.customerId = params.get('id') ?? '';
      void Promise.all([this.loadCustomer(), this.loadSegment(), this.loadTransactions(), this.loadOffers()]);
    });
  }

  private async loadCustomer(): Promise<void> {
    try {
      const all = await firstValueFrom(this.client.customersAll());
      const found = all.find(c => c.id === this.customerId) ?? null;
      this.customer.set(found);
      if (found) this.titleService.setTitle(`Finara — ${found.firstName} ${found.lastName}`);
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Müşteri yüklenemedi', detail: describeApiError(err) });
    }
  }

  private async loadSegment(): Promise<void> {
    try {
      this.segment.set(await firstValueFrom(this.client.segment(this.customerId)));
      this.segmentMessage.set(null);
    } catch (err) {
      this.segment.set(null);
      this.segmentMessage.set(
        apiErrorStatus(err) === 404
          ? 'Bu müşteri için henüz segment hesaplanmamış — Araçlar sayfasından segmentasyonu çalıştırın.'
          : describeApiError(err)
      );
    }
  }

  private async loadTransactions(): Promise<void> {
    try {
      this.transactions.set(await firstValueFrom(this.client.transactionsAll(this.customerId)));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'İşlemler yüklenemedi', detail: describeApiError(err) });
    }
  }

  async recordTransaction(): Promise<void> {
    this.txSubmitted = true;
    if (!this.newTransaction.merchantName || !this.newTransaction.occurredAtUtc || this.newTransaction.amount <= 0) {
      return;
    }

    this.txBusy.set(true);
    try {
      await firstValueFrom(
        this.client.transactions(
          this.customerId,
          new RecordTransactionRequest({
            amount: this.newTransaction.amount,
            category: this.newTransaction.category,
            merchantName: this.newTransaction.merchantName,
            occurredAtUtc: new Date(toUtcDateOnlyIso(this.newTransaction.occurredAtUtc))
          })
        )
      );
      this.newTransaction = { amount: 0, category: TransactionCategory.Groceries, merchantName: '', occurredAtUtc: null };
      this.txSubmitted = false;
      this.messageService.add({ severity: 'success', summary: 'İşlem kaydedildi' });
      await this.loadTransactions();
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'İşlem kaydedilemedi', detail: describeApiError(err) });
    } finally {
      this.txBusy.set(false);
    }
  }

  private async loadOffers(): Promise<void> {
    try {
      this.offers.set(await firstValueFrom(this.client.offers(this.customerId)));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Teklifler yüklenemedi', detail: describeApiError(err) });
    }
  }

  async generateOffers(): Promise<void> {
    this.offersBusy.set(true);
    try {
      const result = await firstValueFrom(this.client.generate2(this.customerId, 5));
      this.messageService.add({
        severity: 'success',
        summary: 'Teklif üretimi tamamlandı',
        detail: result.offersCreated ? `${result.offersCreated} yeni teklif oluşturuldu.` : 'Uygun yeni teklif bulunamadı.'
      });
      await this.loadOffers();
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Teklif üretilemedi', detail: describeApiError(err) });
    } finally {
      this.offersBusy.set(false);
    }
  }

  respondToOffer(offer: OfferSummary, accept: boolean): void {
    if (accept) {
      void this.executeOfferResponse(offer, true);
      return;
    }
    // Reddetme geri döndürülemez bir aksiyon — yanlışlıkla tıklamaya karşı onay isteniyor.
    this.confirmationService.confirm({
      header: 'Teklifi reddet',
      message: `"${offer.campaignName}" teklifini reddetmek istediğinize emin misiniz?`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Reddet',
      rejectLabel: 'Vazgeç',
      acceptButtonProps: { severity: 'danger' },
      accept: () => void this.executeOfferResponse(offer, false)
    });
  }

  private async executeOfferResponse(offer: OfferSummary, accept: boolean): Promise<void> {
    this.respondingOfferId.set(offer.id ?? null);
    try {
      const updated = accept
        ? await firstValueFrom(this.client.accept(this.customerId, offer.id!))
        : await firstValueFrom(this.client.decline(this.customerId, offer.id!));
      this.offers.update(list => list.map(o => (o.id === updated.id ? updated : o)));
      this.messageService.add({ severity: 'success', summary: accept ? 'Teklif kabul edildi' : 'Teklif reddedildi', detail: offer.campaignName });
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'İşlem başarısız', detail: describeApiError(err) });
    } finally {
      this.respondingOfferId.set(null);
    }
  }

  async runAiInsight(): Promise<void> {
    this.aiError.set(null);
    this.aiBusy.set(true);
    try {
      this.aiAnswer.set(
        await firstValueFrom(
          this.client.aiInsight(this.customerId, new AiInsightRequest({ question: this.aiQuestion || undefined }))
        )
      );
    } catch (err) {
      this.aiError.set(describeApiError(err));
    } finally {
      this.aiBusy.set(false);
    }
  }
}
