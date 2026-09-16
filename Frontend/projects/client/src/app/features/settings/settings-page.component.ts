import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageModule } from 'primeng/message';
import { MessageService } from 'primeng/api';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { Subscription } from 'rxjs';

import {
  AuthService,
  DateFormatPreference,
  IUserPreferencesDto,
  TableDensity,
  TwoFactorSetupResult,
  UiLanguage,
  UpdateUserPreferencesRequest,
  UserPreferencesService,
  describeApiError
} from '@finara/shared-ui';

const TIME_ZONE_OPTIONS = ['Europe/Istanbul', 'Europe/Berlin', 'Europe/London', 'Europe/Paris', 'UTC', 'America/New_York'];

@Component({
    selector: 'app-settings-page',
    imports: [FormsModule, CardModule, ButtonModule, InputTextModule, InputNumberModule, SelectModule, SelectButtonModule, CheckboxModule, MessageModule, TranslatePipe],
    templateUrl: './settings-page.component.html'
})
export class SettingsPageComponent implements OnInit, OnDestroy {
  private readonly messageService = inject(MessageService);
  private readonly preferencesService = inject(UserPreferencesService);
  protected readonly translate = inject(TranslateService);
  protected readonly authService = inject(AuthService);

  protected readonly isBankAdmin = computed(() => this.authService.role() === 'BankAdmin');

  // p-select/p-selectButton seçenek etiketleri TranslateService.instant ile üretiliyor —
  // dil değişince (onLangChange) yeniden hesaplanmaları için bir "tetikleyici" sinyale
  // bağımlı bir computed kullanılıyor (impure TranslatePipe şablonda kullanılamayacak
  // kadar çok yerde tekrarlanacağı için burada tek noktadan üretmek daha temiz).
  private readonly langTick = signal(0);
  private readonly langChangeSub: Subscription;

  protected readonly languageOptions = computed(() => {
    this.langTick();
    return [
      { label: 'Türkçe', value: UiLanguage.Turkish },
      { label: 'English', value: UiLanguage.English },
      { label: 'Deutsch', value: UiLanguage.German }
    ];
  });

  protected readonly dateFormatOptions = computed(() => {
    this.langTick();
    return [
      { label: this.translate.instant('settings.dateTime.formatDmy'), value: DateFormatPreference.DayMonthYear },
      { label: this.translate.instant('settings.dateTime.formatMdy'), value: DateFormatPreference.MonthDayYear },
      { label: this.translate.instant('settings.dateTime.formatYmd'), value: DateFormatPreference.YearMonthDay }
    ];
  });

  protected readonly densityOptions = computed(() => {
    this.langTick();
    return [
      { label: this.translate.instant('settings.tableView.densityComfortable'), value: TableDensity.Comfortable },
      { label: this.translate.instant('settings.tableView.densityCompact'), value: TableDensity.Compact }
    ];
  });

  protected readonly timeZoneOptions = TIME_ZONE_OPTIONS.map(id => ({ label: id, value: id }));

  protected loading = signal(true);
  protected saveBusy = signal(false);
  protected form: IUserPreferencesDto = {
    language: UiLanguage.Turkish,
    receiveWeeklyDigestEmail: true,
    dateFormat: DateFormatPreference.DayMonthYear,
    timeZoneId: 'Europe/Istanbul',
    tableDensity: TableDensity.Comfortable,
    defaultPageSize: 20
  };

  // İki faktörlü doğrulama — Hesabım sayfasından buraya taşındı (bkz. kullanıcı isteği).
  protected twoFactorEnabled = signal(false);
  protected setupState = signal<'idle' | 'awaitingCode'>('idle');
  protected setupData = signal<TwoFactorSetupResult | null>(null);
  protected setupCode = '';
  protected disableCode = '';
  protected twoFactorBusy = signal(false);
  protected twoFactorError = signal<string | null>(null);

  constructor() {
    this.langChangeSub = this.translate.onLangChange.subscribe(() => this.langTick.update(n => n + 1));
  }

  async ngOnInit(): Promise<void> {
    try {
      // Bilinçli olarak client.preferencesGET() DEĞİL, preferencesService.load() —
      // ikincisi ayrıca applyLanguage() çağırıp aktif arayüz dilini (TranslateService)
      // BURADA GERÇEKTEN KAYITLI olan değerle senkron tutuyor. Sadece ham GET yapılıp
      // form doldurulsaydı, aktif dil (ör. başka bir sekmede/oturumda değişmiş olabilir)
      // ile bu sayfadaki seçili değer birbirinden sessizce sapabiliyordu (canlı testte
      // bulundu — sekmeler arası "hangi dil gerçekten aktif" karmaşası).
      await this.preferencesService.load();
      const prefs = this.preferencesService.preferences();
      if (prefs) this.form = { ...prefs };
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('common.error'), detail: describeApiError(err) });
    } finally {
      this.loading.set(false);
    }

    // Bu çağrı try/catch DIŞINDAYDI (denetimde bulundu): başarısız olursa kullanıcı hiçbir
    // hata görmüyor, 2FA durumu sessizce güncellenmeden kalıyor ve konsola yakalanmamış bir
    // promise reddi düşüyordu.
    try {
      const me = await this.authService.fetchCurrentUser();
      this.twoFactorEnabled.set(me.twoFactorEnabled ?? false);
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('common.error'), detail: describeApiError(err) });
    }
  }

  ngOnDestroy(): void {
    this.langChangeSub.unsubscribe();
  }

  async save(): Promise<void> {
    this.saveBusy.set(true);
    try {
      await this.preferencesService.save(new UpdateUserPreferencesRequest(this.form));
      this.messageService.add({ severity: 'success', summary: this.translate.instant('settings.saved') });
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('common.error'), detail: describeApiError(err) });
    } finally {
      this.saveBusy.set(false);
    }
  }

  async beginTwoFactorSetup(): Promise<void> {
    this.twoFactorError.set(null);
    this.twoFactorBusy.set(true);
    try {
      const setup = await this.authService.beginTwoFactorSetup();
      this.setupData.set(setup);
      this.setupState.set('awaitingCode');
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('common.error'), detail: describeApiError(err) });
    } finally {
      this.twoFactorBusy.set(false);
    }
  }

  async confirmTwoFactorSetup(): Promise<void> {
    this.twoFactorError.set(null);
    this.twoFactorBusy.set(true);
    try {
      await this.authService.confirmTwoFactorSetup(this.setupCode);
      this.twoFactorEnabled.set(true);
      this.setupState.set('idle');
      this.setupData.set(null);
      this.setupCode = '';
    } catch (err) {
      this.twoFactorError.set(describeApiError(err));
    } finally {
      this.twoFactorBusy.set(false);
    }
  }

  cancelTwoFactorSetup(): void {
    this.twoFactorError.set(null);
    this.setupState.set('idle');
    this.setupData.set(null);
    this.setupCode = '';
  }

  async disableTwoFactor(): Promise<void> {
    this.twoFactorError.set(null);
    this.twoFactorBusy.set(true);
    try {
      await this.authService.disableTwoFactor(this.disableCode);
      this.twoFactorEnabled.set(false);
      this.disableCode = '';
    } catch (err) {
      this.twoFactorError.set(describeApiError(err));
    } finally {
      this.twoFactorBusy.set(false);
    }
  }
}
