import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';

import {
  ApiException,
  Client,
  CreateCustomerRequest,
  CustomerSummary,
  RecordTransactionRequest,
  TransactionCategory,
  TransactionSummary
} from '../../api/finara-client';
import { AuthService } from '../../core/auth.service';

// Faz 3.5 "walking skeleton": Angular client'ın gerçek API'ye (auth + müşteri +
// harcama uçları) uçtan uca bağlandığını kanıtlamak için bilerek minimal tutulan
// tek sayfa. Reactive Forms, validasyon, hata durumları için zengin UX vb. Faz 6'da.
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    InputTextModule,
    PasswordModule,
    ButtonModule,
    TableModule,
    CardModule,
    MessageModule,
    SelectModule,
    DatePickerModule,
    InputNumberModule
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  private readonly client = inject(Client);
  protected readonly auth = inject(AuthService);

  protected readonly categories = Object.values(TransactionCategory);

  protected loginEmail = 'admin@finara.dev';
  protected loginPassword = '';
  protected loginBusy = signal(false);
  protected loginError = signal<string | null>(null);

  protected customers = signal<CustomerSummary[]>([]);
  protected selectedCustomer = signal<CustomerSummary | null>(null);
  protected transactions = signal<TransactionSummary[]>([]);
  protected actionError = signal<string | null>(null);
  protected actionMessage = signal<string | null>(null);

  protected newCustomer = { firstName: '', lastName: '', email: '', dateOfBirth: null as Date | null };
  protected newTransaction = {
    amount: 0,
    category: TransactionCategory.Groceries,
    merchantName: '',
    occurredAtUtc: null as Date | null
  };

  async login(): Promise<void> {
    this.loginError.set(null);
    this.loginBusy.set(true);
    try {
      await this.auth.login(this.loginEmail, this.loginPassword);
      await this.loadCustomers();
    } catch (err) {
      this.loginError.set(this.describeError(err));
    } finally {
      this.loginBusy.set(false);
    }
  }

  async logout(): Promise<void> {
    await this.auth.logout();
    this.customers.set([]);
    this.selectedCustomer.set(null);
    this.transactions.set([]);
  }

  async loadCustomers(): Promise<void> {
    try {
      this.customers.set(await firstValueFrom(this.client.customersAll()));
    } catch (err) {
      this.actionError.set(this.describeError(err));
    }
  }

  async createCustomer(): Promise<void> {
    this.actionError.set(null);
    this.actionMessage.set(null);
    if (!this.newCustomer.firstName || !this.newCustomer.lastName || !this.newCustomer.email || !this.newCustomer.dateOfBirth) {
      this.actionError.set('Tüm müşteri alanları zorunlu.');
      return;
    }

    try {
      const created = await firstValueFrom(
        this.client.customers(
          new CreateCustomerRequest({
            firstName: this.newCustomer.firstName,
            lastName: this.newCustomer.lastName,
            email: this.newCustomer.email,
            dateOfBirth: this.newCustomer.dateOfBirth
          })
        )
      );
      this.actionMessage.set(`Müşteri oluşturuldu: ${created.firstName} ${created.lastName}`);
      this.newCustomer = { firstName: '', lastName: '', email: '', dateOfBirth: null };
      await this.loadCustomers();
    } catch (err) {
      this.actionError.set(this.describeError(err));
    }
  }

  async selectCustomer(customer: CustomerSummary): Promise<void> {
    this.selectedCustomer.set(customer);
    this.actionError.set(null);
    try {
      this.transactions.set(await firstValueFrom(this.client.transactionsAll(customer.id!)));
    } catch (err) {
      this.actionError.set(this.describeError(err));
    }
  }

  async recordTransaction(): Promise<void> {
    const customer = this.selectedCustomer();
    this.actionError.set(null);
    this.actionMessage.set(null);
    if (!customer) {
      this.actionError.set('Önce bir müşteri seçin.');
      return;
    }
    if (!this.newTransaction.merchantName || !this.newTransaction.occurredAtUtc || this.newTransaction.amount <= 0) {
      this.actionError.set('Tüm harcama alanları zorunlu, tutar sıfırdan büyük olmalı.');
      return;
    }

    try {
      await firstValueFrom(
        this.client.transactions(
          customer.id!,
          new RecordTransactionRequest({
            amount: this.newTransaction.amount,
            category: this.newTransaction.category,
            merchantName: this.newTransaction.merchantName,
            occurredAtUtc: this.newTransaction.occurredAtUtc
          })
        )
      );
      this.actionMessage.set('Harcama kaydedildi.');
      this.newTransaction = { amount: 0, category: TransactionCategory.Groceries, merchantName: '', occurredAtUtc: null };
      await this.selectCustomer(customer);
    } catch (err) {
      this.actionError.set(this.describeError(err));
    }
  }

  private describeError(err: unknown): string {
    if (err instanceof ApiException) {
      try {
        const parsed = JSON.parse(err.response);
        return parsed.detail ?? parsed.title ?? err.message;
      } catch {
        return err.message;
      }
    }
    return err instanceof Error ? err.message : String(err);
  }
}
