import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { DatePipe, NgClass } from '@angular/common';
import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ScrapeStatusService } from '../../../core/services/scrape-status.service';
import { ScrapingStatus, ScrapingStatusResponse } from '../../../core/models/scraping-status-response.model';
import { parseUtcDate } from '../../../core/utils/date.utils';
import { DismissibleAlertComponent } from '../../../shared/dismissible-alert/dismissible-alert.component';

const STATUS_BADGE_CLASS: Record<ScrapingStatus, string> = {
  Running: 'bg-info',
  Successful: 'bg-success',
  PartialFailed: 'bg-warning',
  Failed: 'bg-danger',
  Canceled: 'bg-warning'
};

/**
 * Last/last-successful scrape info with the Manual Scrape / Cancel buttons.
 * Hosts can project extra info (`[scrapingStatusInfo]`) and buttons (`[scrapingStatusActions]`) into the same row.
 */
@Component({
  selector: 'app-scraping-status',
  standalone: true,
  imports: [DatePipe, NgClass, DismissibleAlertComponent],
  templateUrl: './scraping-status.component.html',
})
export class ScrapingStatusComponent implements OnInit {
  private readonly scrapeStatus = inject(ScrapeStatusService);
  private readonly destroyRef = inject(DestroyRef);

  scraping = false;
  error: string | null = null;
  success: string | null = null;

  lastScrapingTime: Date | null = null;
  lastScrapingStatus: ScrapingStatus | null = null;
  lastScrapingBadgeClass = '';
  lastScrapingCount = 0;

  lastSuccessfulScrapingTime: Date | null = null;
  lastSuccessfulScrapingCount = 0;

  ngOnInit(): void {
    this.scrapeStatus.status$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(status => this.updateScrapingStatus(status));
  }

  triggerScrape(): void {
    this.error = null;
    this.success = null;
    this.scrapeStatus.triggerScrape().subscribe({
      next: (response) => {
        if (response.success) {
          this.success = response.message || 'Scrape job queued';
          this.scraping = true;
        } else {
          this.error = response.message || 'Scrape failed';
        }
      },
      error: (err) => {
        // 409: a scrape is already queued or running; the API explains that in the body
        if (err instanceof HttpErrorResponse && err.status === HttpStatusCode.Conflict) {
          this.error = err.error?.message || 'A scrape is already queued or running';
          this.scraping = true;
          return;
        }
        this.error = `Scrape error: ${err.message}`;
        this.scraping = false;
      }
    });
  }

  cancelScrape(): void {
    this.scrapeStatus.cancelScrape().subscribe({
      next: () => {
        this.error = null;
      },
      error: (err) => {
        this.error = `Cancel error: ${err.message}`;
      }
    });
  }

  private updateScrapingStatus(status: ScrapingStatusResponse): void {
    this.scraping = status.isScrapingInProgress;

    if (status.lastScrapingHistory) {
      this.lastScrapingTime = parseUtcDate(status.lastScrapingHistory.startTimeUtc);
      this.lastScrapingStatus = status.lastScrapingHistory.status;
      // An unknown status from a newer backend just gets an uncoloured badge.
      this.lastScrapingBadgeClass = STATUS_BADGE_CLASS[this.lastScrapingStatus] ?? '';
      this.lastScrapingCount = status.lastScrapingHistory.flatsScrapedCount;
    }

    if (status.lastSuccessfulScrapingHistory) {
      this.lastSuccessfulScrapingTime = parseUtcDate(status.lastSuccessfulScrapingHistory.startTimeUtc);
      this.lastSuccessfulScrapingCount = status.lastSuccessfulScrapingHistory.flatsScrapedCount;
    }
  }
}
