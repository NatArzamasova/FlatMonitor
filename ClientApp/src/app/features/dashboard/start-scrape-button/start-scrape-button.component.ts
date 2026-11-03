import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs/operators';
import { ScrapeStatusService } from '../../../core/services/scrape-status.service';
import { describeScrapeStartError } from '../../../core/utils/scrape-error.utils';

/**
 * "Start a scrape" for the tabs' empty states. While a scrape runs it is disabled and says so; the header's scrape
 * control shows the progress. A failure to start is shown below the button.
 */
@Component({
  selector: 'app-start-scrape-button',
  standalone: true,
  host: { class: 'd-flex flex-column align-items-center gap-2' },
  template: `
    <button type="button" class="btn btn-primary" (click)="start()" [disabled]="scraping || busy">
      @if (scraping) {
        <span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>
        Scraping…
      } @else {
        Start a scrape
      }
    </button>
    @if (error) {
      <small class="text-danger" role="alert">{{ error }}</small>
    }
  `
})
export class StartScrapeButtonComponent implements OnInit {
  private readonly scrapeStatus = inject(ScrapeStatusService);
  private readonly destroyRef = inject(DestroyRef);

  scraping = false;
  busy = false;
  error: string | null = null;

  ngOnInit(): void {
    this.scrapeStatus.status$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(status => this.scraping = status.isScrapingInProgress);
  }

  start(): void {
    this.error = null;
    this.busy = true;
    this.scrapeStatus.triggerScrape()
      .pipe(finalize(() => this.busy = false))
      .subscribe({
        next: response => {
          if (response.success) {
            this.scraping = true;
          } else {
            this.error = response.message || 'Scrape failed';
          }
        },
        error: err => {
          const failure = describeScrapeStartError(err);
          this.error = failure.message;
          this.scraping = failure.alreadyRunning;
        }
      });
  }
}
