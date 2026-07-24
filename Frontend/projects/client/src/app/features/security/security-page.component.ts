import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CardModule } from 'primeng/card';

@Component({
    selector: 'app-security-page',
    imports: [RouterLink, CardModule],
    templateUrl: './security-page.component.html'
})
export class SecurityPageComponent {}
