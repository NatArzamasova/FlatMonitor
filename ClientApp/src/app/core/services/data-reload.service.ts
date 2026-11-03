import { Injectable, inject } from '@angular/core';
import { merge, Observable, Subject } from 'rxjs';
import { ScrapeStatusService } from './scrape-status.service';

/**
 * Tells the dashboard tabs to load their data again: when the header's refresh button is clicked
 * (`requestReload()`) and each time a scrape finishes.
 */
@Injectable({
  providedIn: 'root'
})
export class DataReloadService {
  private readonly scrapeStatus = inject(ScrapeStatusService);
  private readonly requested$ = new Subject<void>();

  readonly reload$: Observable<void> = merge(this.requested$, this.scrapeStatus.scrapeFinished$);

  requestReload(): void {
    this.requested$.next();
  }
}
