import { Injectable, inject } from '@angular/core';
import { EMPTY, merge, Observable, Subject, timer } from 'rxjs';
import { catchError, filter, map, pairwise, shareReplay, switchMap, tap } from 'rxjs/operators';
import { FlatsService } from './flats.service';
import { CancelScrapeResponse, ScrapeResponse } from '../models/scrape-response.model';
import { ScrapingStatusResponse } from '../models/scraping-status-response.model';

export const SCRAPE_STATUS_POLL_INTERVAL_MS = 2000;

/** Polls the scrape status while anyone is subscribed and owns starting/cancelling scrapes. */
@Injectable({
  providedIn: 'root'
})
export class ScrapeStatusService {
  // Declared first: the stream fields below use it in their initializers.
  private readonly flatsService = inject(FlatsService);
  private readonly refresh$ = new Subject<void>();

  /** Latest scrape status: polled immediately and then every 2 s, shared by all subscribers. */
  readonly status$: Observable<ScrapingStatusResponse> = merge(
    timer(0, SCRAPE_STATUS_POLL_INTERVAL_MS),
    this.refresh$
  ).pipe(
    // Catch per request: an error reaching the outer stream would stop polling for good.
    switchMap(() => this.flatsService.getScrapeStatus().pipe(
      catchError((err) => {
        console.error('Failed to poll scraping status:', err);
        return EMPTY;
      })
    )),
    shareReplay({ bufferSize: 1, refCount: true })
  );

  /** Emits once each time a running scrape is reported as finished. */
  readonly scrapeFinished$: Observable<void> = this.status$.pipe(
    map(status => status.isScrapingInProgress),
    pairwise(),
    filter(([wasScraping, isScraping]) => wasScraping && !isScraping),
    map(() => undefined)
  );

  /** Polls once now instead of waiting for the next tick. */
  refresh(): void {
    this.refresh$.next();
  }

  triggerScrape(): Observable<ScrapeResponse> {
    return this.flatsService.triggerScrape().pipe(
      tap(response => {
        if (response.success) {
          this.refresh();
        }
      })
    );
  }

  cancelScrape(): Observable<CancelScrapeResponse> {
    return this.flatsService.cancelScrape();
  }
}
