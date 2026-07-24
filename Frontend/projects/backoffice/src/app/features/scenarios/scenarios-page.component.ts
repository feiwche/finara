import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CardModule } from 'primeng/card';

@Component({
    selector: 'app-scenarios-page',
    imports: [RouterLink, CardModule],
    templateUrl: './scenarios-page.component.html'
})
export class ScenariosPageComponent {}
