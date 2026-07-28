import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageModule } from 'primeng/message';
import { MessageService } from 'primeng/api';

import { AuthService, Client, GenerateMockDataRequest, describeApiError } from '@finara/shared-ui';

@Component({
    selector: 'app-tools',
    imports: [FormsModule, CardModule, ButtonModule, InputNumberModule, MessageModule, TranslatePipe],
    templateUrl: './tools.component.html'
})
export class ToolsComponent {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);
  protected readonly authService = inject(AuthService);
  protected readonly translate = inject(TranslateService);

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
        summary: this.translate.instant('tools.mockData.successSummary'),
        detail: this.translate.instant('tools.mockData.successDetail', {
          customers: result.customersCreated,
          transactions: result.transactionsCreated
        })
      });
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('tools.mockData.errorSummary'), detail: describeApiError(err) });
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
        summary: this.translate.instant('tools.segmentation.successSummary'),
        detail: this.translate.instant('tools.segmentation.successDetail', {
          processed: result.customersProcessed,
          skipped: result.customersSkipped
        })
      });
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('tools.segmentation.errorSummary'), detail: describeApiError(err) });
    } finally {
      this.segmentationBusy.set(false);
    }
  }
}
