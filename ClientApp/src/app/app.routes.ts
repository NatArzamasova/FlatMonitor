import { inject } from '@angular/core';
import { RedirectFunction, Router, Routes } from '@angular/router';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { OverviewTabComponent } from './features/dashboard/tabs/overview-tab/overview-tab.component';
import { OffersTabComponent } from './features/dashboard/tabs/offers-tab/offers-tab.component';

// Overview rows used to open `/?tab=offers&district=…&rooms=…`; keep those links working.
const redirectLegacyTabLinks: RedirectFunction = ({ queryParams }) => {
  const { tab, ...filters } = queryParams;
  const target = tab === 'offers' ? '/offers' : '/overview';
  return inject(Router).createUrlTree([target], { queryParams: filters });
};

export const routes: Routes = [
  {
    path: '',
    component: DashboardComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: redirectLegacyTabLinks },
      { path: 'overview', component: OverviewTabComponent },
      { path: 'offers', component: OffersTabComponent },
      // Lazy so Chart.js is only downloaded when the History tab is opened.
      {
        path: 'history',
        loadComponent: () => import('./features/dashboard/tabs/history-tab/history-tab.component')
          .then(m => m.HistoryTabComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
