import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CardModule } from 'primeng/card';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
    selector: 'app-security-page',
    imports: [RouterLink, CardModule, TranslatePipe],
    templateUrl: './security-page.component.html'
})
export class SecurityPageComponent {}
