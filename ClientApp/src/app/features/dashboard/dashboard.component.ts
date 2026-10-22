import { AfterViewInit, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OverviewTabComponent } from './tabs/overview-tab/overview-tab.component';
import { OffersTabComponent } from './tabs/offers-tab/offers-tab.component';
import { HistoryTabComponent } from './tabs/history-tab/history-tab.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, OverviewTabComponent, OffersTabComponent, HistoryTabComponent],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements AfterViewInit {
  ngAfterViewInit(): void {
    const params = new URLSearchParams(window.location.search);
    if (params.get('tab') !== 'offers') {
      return;
    }

    document.getElementById('offers-tab')?.click();
  }
}
