import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageModule } from 'primeng/message';

import { Client, CreateCustomerRequest, CustomerSummary } from '../../api/finara-client';
import { describeApiError } from '../../core/api-error.util';
import { toUtcDateOnlyIso } from '../../core/date.util';

@Component({
  selector: 'app-customer-list',
  standalone: true,
  imports: [FormsModule, TableModule, ButtonModule, DialogModule, InputTextModule, DatePickerModule, CheckboxModule, MessageModule],
  templateUrl: './customer-list.component.html'
})
export class CustomerListComponent implements OnInit {
  private readonly client = inject(Client);
  private readonly router = inject(Router);

  protected customers = signal<CustomerSummary[]>([]);
  protected loading = signal(false);
  protected error = signal<string | null>(null);

  protected dialogVisible = signal(false);
  protected createBusy = signal(false);
  protected createError = signal<string | null>(null);
  protected newCustomer = {
    firstName: '',
    lastName: '',
    email: '',
    dateOfBirth: null as Date | null,
    hasInvestmentAccount: false,
    hasInternationalTransfers: false
  };

  async ngOnInit(): Promise<void> {
    await this.loadCustomers();
  }

  async loadCustomers(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.customers.set(await firstValueFrom(this.client.customersAll()));
    } catch (err) {
      this.error.set(describeApiError(err));
    } finally {
      this.loading.set(false);
    }
  }

  openCreateDialog(): void {
    this.newCustomer = {
      firstName: '',
      lastName: '',
      email: '',
      dateOfBirth: null,
      hasInvestmentAccount: false,
      hasInternationalTransfers: false
    };
    this.createError.set(null);
    this.dialogVisible.set(true);
  }

  async createCustomer(): Promise<void> {
    this.createError.set(null);
    if (!this.newCustomer.firstName || !this.newCustomer.lastName || !this.newCustomer.email || !this.newCustomer.dateOfBirth) {
      this.createError.set('Tüm zorunlu alanları doldurun.');
      return;
    }

    this.createBusy.set(true);
    try {
      await firstValueFrom(
        this.client.customers(
          new CreateCustomerRequest({
            firstName: this.newCustomer.firstName,
            lastName: this.newCustomer.lastName,
            email: this.newCustomer.email,
            dateOfBirth: new Date(toUtcDateOnlyIso(this.newCustomer.dateOfBirth)),
            hasInvestmentAccount: this.newCustomer.hasInvestmentAccount,
            hasInternationalTransfers: this.newCustomer.hasInternationalTransfers
          })
        )
      );
      this.dialogVisible.set(false);
      await this.loadCustomers();
    } catch (err) {
      this.createError.set(describeApiError(err));
    } finally {
      this.createBusy.set(false);
    }
  }

  openDetail(customer: CustomerSummary): void {
    this.router.navigate(['/customers', customer.id]);
  }
}
