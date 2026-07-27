import { TestBed } from '@angular/core/testing';
import { MessageService, ConfirmationService } from 'primeng/api';
import { AppComponent } from './app.component';

// AppComponent artık ToastModule/ConfirmDialogModule kullanıyor (bkz. app.component.html:
// <p-toast/><p-confirmdialog/><router-outlet/>, "Hello, client" başlığı yok) — bu component'ler
// MessageService/ConfirmationService'e ihtiyaç duyuyor, TestBed'e sağlanmazsa
// NullInjectorError fırlatıyor.
describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [MessageService, ConfirmationService]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it(`should have the 'client' title`, () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app.title).toEqual('client');
  });

  it('should render without errors', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(() => fixture.detectChanges()).not.toThrow();
  });
});
