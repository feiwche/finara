import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CardModule } from 'primeng/card';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
    selector: 'app-scenarios-page',
    imports: [RouterLink, CardModule, TranslatePipe],
    templateUrl: './scenarios-page.component.html'
})
export class ScenariosPageComponent {}
