import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
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
import { TextareaModule } from 'primeng/textarea';

import {
  AiInsightRequest,
  AiInsightResult,
  ApiException,
  Client,
  CustomerSummary,
  OfferSummary,
  RecordTransactionRequest,
  SegmentSummary,
  TransactionCategory,
  TransactionSummary
} from '../../api/finara-client';
import { describeApiError } from '../../core/api-error.util';
import { toUtcDateOnlyIso } from '../../core/date.util';

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
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
    TextareaModule
  ],
  templateUrl: './customer-detail.component.html'
})
export class CustomerDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly client = inject(Client);

  protected customerId = '';
  protected customer = signal<CustomerSummary | null>(null);
  protected loadError = signal<string | null>(null);

  protected segment = signal<SegmentSummary | null>(null);
  protected segmentMessage = signal<string | null>(null);

  protected transactions = signal<TransactionSummary[]>([]);
  protected categories = Object.values(TransactionCategory);
  protected newTransaction = { amount: 0, category: TransactionCategory.Groceries, merchantName: '', occurredAtUtc: null as Date | null };
  protected txBusy = signal(false);
  protected txError = signal<string | null>(null);

  protected offers = signal<OfferSummary[]>([]);
  protected offersBusy = signal(false);
  protected offersError = signal<string | null>(null);
  protected respondingOfferId = signal<string | null>(null);

  protected aiQuestion = '';
  protected aiAnswer = signal<AiInsightResult | null>(null);
  protected aiBusy = signal(false);
  protected aiError = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    this.customerId = this.route.snapshot.paramMap.get('id') ?? '';
    await Promise.all([this.loadCustomer(), this.loadSegment(), this.loadTransactions(), this.loadOffers()]);
  }

  private async loadCustomer(): Promise<void> {
    try {
      const all = await firstValueFrom(this.client.customersAll());
      this.customer.set(all.find(c => c.id === this.customerId) ?? null);
    } catch (err) {
      this.loadError.set(describeApiError(err));
    }
  }

  private async loadSegment(): Promise<void> {
    try {
      this.segment.set(await firstValueFrom(this.client.segment(this.customerId)));
      this.segmentMessage.set(null);
    } catch (err) {
      this.segment.set(null);
      this.segmentMessage.set(
        err instanceof ApiException && err.status === 404
          ? 'Bu müşteri için henüz segment hesaplanmamış — Araçlar sayfasından segmentasyonu çalıştırın.'
          : describeApiError(err)
      );
    }
  }

  private async loadTransactions(): Promise<void> {
    try {
      this.transactions.set(await firstValueFrom(this.client.transactionsAll(this.customerId)));
    } catch (err) {
      this.loadError.set(describeApiError(err));
    }
  }

  async recordTransaction(): Promise<void> {
    this.txError.set(null);
    if (!this.newTransaction.merchantName || !this.newTransaction.occurredAtUtc || this.newTransaction.amount <= 0) {
      this.txError.set('Tüm alanlar zorunlu, tutar sıfırdan büyük olmalı.');
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
      await this.loadTransactions();
    } catch (err) {
      this.txError.set(describeApiError(err));
    } finally {
      this.txBusy.set(false);
    }
  }

  private async loadOffers(): Promise<void> {
    try {
      this.offers.set(await firstValueFrom(this.client.offers(this.customerId)));
    } catch (err) {
      this.offersError.set(describeApiError(err));
    }
  }

  async generateOffers(): Promise<void> {
    this.offersError.set(null);
    this.offersBusy.set(true);
    try {
      await firstValueFrom(this.client.generate2(this.customerId, 5));
      await this.loadOffers();
    } catch (err) {
      this.offersError.set(describeApiError(err));
    } finally {
      this.offersBusy.set(false);
    }
  }

  async respondToOffer(offer: OfferSummary, accept: boolean): Promise<void> {
    this.offersError.set(null);
    this.respondingOfferId.set(offer.id ?? null);
    try {
      const updated = accept
        ? await firstValueFrom(this.client.accept(this.customerId, offer.id!))
        : await firstValueFrom(this.client.decline(this.customerId, offer.id!));
      this.offers.update(list => list.map(o => (o.id === updated.id ? updated : o)));
    } catch (err) {
      this.offersError.set(describeApiError(err));
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
