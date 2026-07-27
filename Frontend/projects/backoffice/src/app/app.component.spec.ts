import { TestBed } from '@angular/core/testing';
import { MessageService, ConfirmationService } from 'primeng/api';
import { AppComponent } from './app.component';

// client'taki app.component.spec.ts ile aynı gerekçe — bkz. o dosyadaki yorum.
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

  it(`should have the 'backoffice' title`, () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app.title).toEqual('backoffice');
  });

  it('should render without errors', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(() => fixture.detectChanges()).not.toThrow();
  });
});
