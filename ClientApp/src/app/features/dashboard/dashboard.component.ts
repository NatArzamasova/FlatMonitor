import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AppHeaderComponent } from '../../shared/app-header/app-header.component';
import { DataReloadService } from '../../core/services/data-reload.service';
import { ScrapeControlComponent } from './scrape-control/scrape-control.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [AppHeaderComponent, RouterLink, RouterLinkActive, RouterOutlet, ScrapeControlComponent],
  host: { class: 'app-shell' },
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent {
  private readonly dataReload = inject(DataReloadService);

  /** The header's refresh button: the open tab loads its data again. */
  reloadData(): void {
    this.dataReload.requestReload();
  }
}
