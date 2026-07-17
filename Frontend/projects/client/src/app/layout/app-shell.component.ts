import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { MenuItem } from 'primeng/api';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, MenubarModule, ButtonModule],
  templateUrl: './app-shell.component.html'
})
export class AppShellComponent {
  private readonly router = inject(Router);
  protected readonly authService = inject(AuthService);

  protected readonly navItems: MenuItem[] = [
    { label: 'Müşteriler', icon: 'pi pi-users', routerLink: '/customers' },
    { label: 'Kampanyalar', icon: 'pi pi-megaphone', routerLink: '/campaigns' },
    { label: 'Araçlar', icon: 'pi pi-wrench', routerLink: '/tools' }
  ];

  async logout(): Promise<void> {
    await this.authService.logout();
    await this.router.navigate(['/login']);
  }
}
