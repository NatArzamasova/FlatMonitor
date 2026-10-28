import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ScrapeStatusService } from '../../../core/services/scrape-status.service';
import { ScrapingStatusResponse } from '../../../core/models/scraping-status-response.model';
import { DismissibleAlertComponent } from '../../../shared/dismissible-alert/dismissible-alert.component';

/**
 * Last/last-successful scrape info with the Manual Scrape / Cancel buttons.
 * Hosts can project extra info (`[scrapingStatusInfo]`) and buttons (`[scrapingStatusActions]`) into the same row.
 */
@Component({
  selector: 'app-scraping-status',
  standalone: true,
  imports: [CommonModule, DismissibleAlertComponent],
  templateUrl: './scraping-status.component.html',
})
export class ScrapingStatusComponent implements OnInit, OnDestroy {
  scraping = false;
  error: string | null = null;
  success: string | null = null;

  lastScrapingTime: Date | null = null;
  lastScrapingStatus: string | null = null;
  lastScrapingCount = 0;

  lastSuccessfulScrapingTime: Date | null = null;
  lastSuccessfulScrapingCount = 0;

  private destroy$ = new Subject<void>();

  constructor(private scrapeStatus: ScrapeStatusService) {}

  ngOnInit(): void {
    this.scrapeStatus.status$
      .pipe(takeUntil(this.destroy$))
      .subscribe(status => this.updateScrapingStatus(status));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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
      this.lastScrapingTime = new Date(status.lastScrapingHistory.startTimeUtc);
      this.lastScrapingStatus = status.lastScrapingHistory.status;
      this.lastScrapingCount = status.lastScrapingHistory.flatsScrapedCount;
    }

    if (status.lastSuccessfulScrapingHistory) {
      this.lastSuccessfulScrapingTime = new Date(status.lastSuccessfulScrapingHistory.startTimeUtc);
      this.lastSuccessfulScrapingCount = status.lastSuccessfulScrapingHistory.flatsScrapedCount;
    }
  }
}
