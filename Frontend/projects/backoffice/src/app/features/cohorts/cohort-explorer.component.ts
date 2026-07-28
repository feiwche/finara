import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';

import {
  ageBracketLabel,
  Client,
  CohortSummary,
  describeApiError,
  educationLevelLabel,
  propensitySeverity,
  spendingPropensityLabel,
  transactionCategoryLabel
} from '@finara/shared-ui';

@Component({
    selector: 'app-cohort-explorer',
    imports: [TableModule, TagModule, TranslatePipe],
    templateUrl: './cohort-explorer.component.html'
})
export class CohortExplorerComponent implements OnInit {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);
  protected readonly translate = inject(TranslateService);

  protected cohorts = signal<CohortSummary[]>([]);
  protected loading = signal(false);

  protected readonly totalCustomerCount = computed(() => this.cohorts().reduce((sum, c) => sum + (c.customerCount ?? 0), 0));
  protected readonly otherGroupCount = computed(() => this.cohorts().filter(c => c.isOther).length);

  protected readonly ageBracketLabel = ageBracketLabel;
  protected readonly educationLevelLabel = educationLevelLabel;
  protected readonly spendingPropensityLabel = spendingPropensityLabel;
  protected readonly propensitySeverity = propensitySeverity;
  protected readonly transactionCategoryLabel = transactionCategoryLabel;

  async ngOnInit(): Promise<void> {
    await this.loadCohorts();
  }

  async loadCohorts(): Promise<void> {
    this.loading.set(true);
    try {
      this.cohorts.set(await firstValueFrom(this.client.cohorts()));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('cohortExplorer.toast.loadFailed'), detail: describeApiError(err) });
    } finally {
      this.loading.set(false);
    }
  }
}
