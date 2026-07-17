import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageModule } from 'primeng/message';

import { Client, GenerateMockDataRequest, MockGenerationResult, RecalculateSegmentsResult } from '../../api/finara-client';
import { describeApiError } from '../../core/api-error.util';

@Component({
  selector: 'app-tools',
  standalone: true,
  imports: [FormsModule, CardModule, ButtonModule, InputNumberModule, MessageModule],
  templateUrl: './tools.component.html'
})
export class ToolsComponent {
  private readonly client = inject(Client);

  protected mockCustomerCount = 20;
  protected mockTransactionsPerCustomer = 15;
  protected mockBusy = signal(false);
  protected mockError = signal<string | null>(null);
  protected mockResult = signal<MockGenerationResult | null>(null);

  protected segmentationBusy = signal(false);
  protected segmentationError = signal<string | null>(null);
  protected segmentationResult = signal<RecalculateSegmentsResult | null>(null);

  async generateMockData(): Promise<void> {
    this.mockError.set(null);
    this.mockResult.set(null);
    this.mockBusy.set(true);
    try {
      this.mockResult.set(
        await firstValueFrom(
          this.client.generate(
            new GenerateMockDataRequest({
              customerCount: this.mockCustomerCount,
              transactionsPerCustomer: this.mockTransactionsPerCustomer
            })
          )
        )
      );
    } catch (err) {
      this.mockError.set(describeApiError(err));
    } finally {
      this.mockBusy.set(false);
    }
  }

  async recalculateSegmentation(): Promise<void> {
    this.segmentationError.set(null);
    this.segmentationResult.set(null);
    this.segmentationBusy.set(true);
    try {
      this.segmentationResult.set(await firstValueFrom(this.client.recalculate()));
    } catch (err) {
      this.segmentationError.set(describeApiError(err));
    } finally {
      this.segmentationBusy.set(false);
    }
  }
}
