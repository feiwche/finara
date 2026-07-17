import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageService } from 'primeng/api';

import { Client, GenerateMockDataRequest } from '../../api/finara-client';
import { describeApiError } from '../../core/api-error.util';

@Component({
  selector: 'app-tools',
  standalone: true,
  imports: [FormsModule, CardModule, ButtonModule, InputNumberModule],
  templateUrl: './tools.component.html'
})
export class ToolsComponent {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);

  protected mockCustomerCount = 20;
  protected mockTransactionsPerCustomer = 15;
  protected mockBusy = signal(false);

  protected segmentationBusy = signal(false);

  async generateMockData(): Promise<void> {
    this.mockBusy.set(true);
    try {
      const result = await firstValueFrom(
        this.client.generate(
          new GenerateMockDataRequest({
            customerCount: this.mockCustomerCount,
            transactionsPerCustomer: this.mockTransactionsPerCustomer
          })
        )
      );
      this.messageService.add({
        severity: 'success',
        summary: 'Mock veri üretildi',
        detail: `${result.customersCreated} müşteri, ${result.transactionsCreated} işlem oluşturuldu.`
      });
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Mock veri üretilemedi', detail: describeApiError(err) });
    } finally {
      this.mockBusy.set(false);
    }
  }

  async recalculateSegmentation(): Promise<void> {
    this.segmentationBusy.set(true);
    try {
      const result = await firstValueFrom(this.client.recalculate());
      this.messageService.add({
        severity: 'success',
        summary: 'Segmentasyon tamamlandı',
        detail: `${result.customersProcessed} müşteri işlendi, ${result.customersSkipped} atlandı.`
      });
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Segmentasyon başarısız', detail: describeApiError(err) });
    } finally {
      this.segmentationBusy.set(false);
    }
  }
}
