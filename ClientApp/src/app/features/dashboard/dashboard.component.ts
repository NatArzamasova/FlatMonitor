import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { interval, Subject } from 'rxjs';
import { takeUntil, switchMap } from 'rxjs/operators';
import { FlatsService } from '../../core/services/flats.service';
import { FlatAggregation } from '../../core/models/flat-aggregation.model';
import { ScrapingStatusResponse, ScrapingHistoryDto } from '../../core/models/scraping-status-response.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit, OnDestroy {
  aggregatedData: FlatAggregation[] = [];
  loading = false;
  scraping = false;
  error: string | null = null;
  lastUpdated: Date | null = null;
  scrapedCount = 0;
  lastScrapingTime: Date | null = null;
  lastScrapingStatus: string | null = null;
  lastScrapingCount = 0;
  private destroy$ = new Subject<void>();

  constructor(private flatsService: FlatsService) {}

  ngOnInit(): void {
    this.loadAggregatedData();
    this.loadScrapingStatus();

    // Poll scraping status: fast while scraping, slow otherwise
    interval(2000)  // Poll every 2 seconds when scraping
      .pipe(
        switchMap(() => this.flatsService.getScrapeStatus()),
        takeUntil(this.destroy$)
      )
      .subscribe((status) => {
        this.updateScrapingStatus(status);
        if (status.isScrapingInProgress) {
          this.scraping = true;
        } else if (this.scraping) {
          // Scraping just finished, reload data
          this.scraping = false;
          this.loadAggregatedData();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAggregatedData(): void {
    this.loading = true;
    this.error = null;
    this.flatsService.getAggregated().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.aggregatedData = response.data;
          this.lastUpdated = new Date();
        } else {
          this.error = response.message || 'Failed to load data';
        }
        this.loading = false;
      },
      error: (err) => {
        this.error = `Error loading data: ${err.message}`;
        this.loading = false;
      }
    });
  }

  loadScrapingStatus(): void {
    this.flatsService.getScrapeStatus().subscribe({
      next: (status) => {
        this.updateScrapingStatus(status);
      },
      error: (err) => {
        console.error('Failed to load scraping status:', err);
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
  }

  triggerScrape(): void {
    this.error = null;
    this.flatsService.triggerScrape().subscribe({
      next: (response) => {
        if (response.success) {
          this.scrapedCount = response.flatsCount;
          // Immediately check status to update UI
          this.loadScrapingStatus();
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
    this.flatsService.cancelScrape().subscribe({
      next: () => {
        this.error = null;
      },
      error: (err) => {
        this.error = `Cancel error: ${err.message}`;
      }
    });
  }

  refresh(): void {
    this.loadAggregatedData();
  }
}
