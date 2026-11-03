import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AppHeaderComponent } from '../../shared/app-header/app-header.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [AppHeaderComponent, RouterLink, RouterLinkActive, RouterOutlet],
  host: { class: 'app-shell' },
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent {}
