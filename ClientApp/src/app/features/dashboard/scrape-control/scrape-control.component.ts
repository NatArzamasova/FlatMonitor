import { Component, DestroyRef, ElementRef, HostListener, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs/operators';
import { ScrapeStatusService } from '../../../core/services/scrape-status.service';
import { ScrapingHistoryDto, ScrapingStatus, ScrapingStatusResponse } from '../../../core/models/scraping-status-response.model';
import { parseUtcDate } from '../../../core/utils/date.utils';
import { formatTimeAgo } from '../../../core/utils/relative-time.utils';
import { describeScrapeStartError } from '../../../core/utils/scrape-error.utils';

/** A scrape run as shown in the header and its details panel. */
export interface ScrapeRun {
  status: ScrapingStatus;
  statusLabel: string;
  tone: string;
  start: Date;
  end: Date | null;
  timeAgo: string;
  flats: number;
  errorMessage: string | null;
}

const STATUS_LABEL: Record<ScrapingStatus, string> = {
  Running: 'Running',
  Successful: 'Successful',
  PartialFailed: 'Partly failed',
  Failed: 'Failed',
  Canceled: 'Canceled'
};

/** Bootstrap colour of each status, for the status dot and the badges in the panel. */
const STATUS_TONE: Record<ScrapingStatus, string> = {
  Running: 'info',
  Successful: 'success',
  PartialFailed: 'warning',
  Failed: 'danger',
  Canceled: 'secondary'
};

/**
 * Scrape status and controls for the app header: a status dot with "Last scrape 2 h ago · 246 flats" that opens a
 * details panel, "Scrape now" / "Cancel", a progress bar along the header's bottom edge while a scrape runs, and
 * errors of starting or cancelling. The progress bar is placed against the nearest positioned ancestor (the
 * sticky header), so the host itself stays unpositioned.
 */
@Component({
  selector: 'app-scrape-control',
  standalone: true,
  imports: [DatePipe],
  host: { class: 'd-flex align-items-center gap-2' },
  template: `
    @if (error) {
      <span class="scrape-error small text-danger d-flex align-items-center gap-1" role="alert">
        <span class="text-truncate">{{ error }}</span>
        <button type="button" class="btn-close" aria-label="Dismiss the scrape error" (click)="error = null"></button>
      </span>
    }

    <div class="dropdown" (keydown.escape)="detailsOpen = false">
      <button
        type="button"
        class="btn btn-sm btn-link text-decoration-none d-flex align-items-center gap-2 scrape-status-button"
        aria-haspopup="true"
        [attr.aria-expanded]="detailsOpen"
        aria-controls="scrape-details"
        title="Show the details of the last scrape"
        (click)="detailsOpen = !detailsOpen"
      >
        <span class="scrape-dot" [class]="'bg-' + (scraping ? 'info' : (lastRun?.tone ?? 'secondary'))"
          [class.scrape-dot-pulse]="scraping" aria-hidden="true"></span>
        <span class="d-none d-md-inline scrape-summary">{{ summary }}</span>
        <span class="visually-hidden d-md-none">{{ summary }}</span>
      </button>

      @if (detailsOpen) {
        <div id="scrape-details" class="dropdown-menu dropdown-menu-end show p-3 small scrape-details" data-bs-popper="static">
          @if (lastRun; as run) {
            <h2 class="h6 mb-2">Last scrape</h2>
            <dl class="mb-0">
              <dt>Status</dt>
              <dd><span class="badge" [class]="'text-bg-' + run.tone">{{ run.statusLabel }}</span></dd>
              <dt>Started</dt>
              <dd>{{ run.start | date: 'medium' }} ({{ run.timeAgo }})</dd>
              @if (run.end) {
                <dt>Finished</dt>
                <dd>{{ run.end | date: 'medium' }}</dd>
              }
              <dt>Flats</dt>
              <dd>{{ run.flats }}</dd>
              @if (run.errorMessage) {
                <dt>Error</dt>
                <dd class="text-danger scrape-error-message">{{ run.errorMessage }}</dd>
              }
            </dl>
            <!-- Only worth showing when the last run did not succeed. -->
            @if (lastSuccessfulRun && run.status !== 'Successful') {
              <h2 class="h6 mt-3 mb-2">Last successful scrape</h2>
              <dl class="mb-0">
                <dt>Started</dt>
                <dd>{{ lastSuccessfulRun.start | date: 'medium' }} ({{ lastSuccessfulRun.timeAgo }})</dd>
                <dt>Flats</dt>
                <dd>{{ lastSuccessfulRun.flats }}</dd>
              </dl>
            }
          } @else {
            <p class="mb-0 text-body-secondary">No scrape has run yet.</p>
          }
        </div>
      }
    </div>

    @if (scraping) {
      <button type="button" class="btn btn-sm btn-outline-danger text-nowrap" (click)="cancelScrape()" [disabled]="busy">
        <span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>
        Cancel
      </button>
    } @else {
      <button type="button" class="btn btn-sm btn-success text-nowrap" (click)="triggerScrape()" [disabled]="busy"
        title="Fetch the current flats from all sources">Scrape now</button>
    }

    @if (scraping) {
      <div class="progress scrape-progress" role="progressbar" aria-label="Scrape in progress">
        <div class="progress-bar progress-bar-striped progress-bar-animated w-100"></div>
      </div>
    }
  `,
  styles: [`
    .scrape-status-button {
      color: var(--bs-secondary-color);
      font-size: 0.8125rem;
      white-space: nowrap;
    }
    .scrape-status-button:hover,
    .scrape-status-button:focus-visible {
      color: var(--bs-emphasis-color);
    }
    .scrape-dot {
      display: inline-block;
      flex-shrink: 0;
      width: 0.625rem;
      height: 0.625rem;
      border-radius: 50%;
    }
    .scrape-dot-pulse {
      animation: scrape-pulse 1.2s ease-in-out infinite;
    }
    @keyframes scrape-pulse {
      50% { opacity: 0.3; }
    }
    @media (prefers-reduced-motion: reduce) {
      .scrape-dot-pulse { animation: none; }
    }
    .scrape-details {
      width: 20rem;
      max-width: calc(100vw - 2rem);
      box-shadow: var(--bs-box-shadow);
    }
    .scrape-details dl {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 0.25rem 0.75rem;
    }
    .scrape-details dt {
      font-weight: 500;
      color: var(--bs-secondary-color);
    }
    .scrape-details dd {
      margin: 0;
    }
    .scrape-error-message {
      overflow-wrap: anywhere;
    }
    .scrape-error {
      max-width: 22rem;
    }
    .scrape-error .btn-close {
      flex-shrink: 0;
      font-size: 0.625rem;
    }
    .scrape-progress {
      position: absolute;
      left: 0;
      right: 0;
      bottom: 0;
      height: 3px;
      border-radius: 0;
    }
  `]
})
export class ScrapeControlComponent implements OnInit {
  private readonly scrapeStatus = inject(ScrapeStatusService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  scraping = false;
  /** A start or cancel request is under way. */
  busy = false;
  error: string | null = null;
  detailsOpen = false;
  lastRun: ScrapeRun | null = null;
  lastSuccessfulRun: ScrapeRun | null = null;

  /** One line for the header: what runs now, or when the last scrape ran and what it found. */
  get summary(): string {
    const run = this.lastRun;
    if (this.scraping) {
      return run?.status === 'Running' ? `Scraping… started ${run.timeAgo} · ${run.flats} flats` : 'Scraping…';
    }
    if (!run) {
      return 'No scrape yet';
    }
    const outcome = run.status === 'Successful' ? '' : ` (${run.statusLabel.toLowerCase()})`;
    return `Last scrape ${run.timeAgo}${outcome} · ${run.flats} flats`;
  }

  ngOnInit(): void {
    this.scrapeStatus.status$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(status => this.updateStatus(status));
  }

  triggerScrape(): void {
    this.error = null;
    this.busy = true;
    this.scrapeStatus.triggerScrape()
      .pipe(finalize(() => this.busy = false))
      .subscribe({
        next: (response) => {
          if (response.success) {
            this.scraping = true;
          } else {
            this.error = response.message || 'Scrape failed';
          }
        },
        error: (err) => {
          const failure = describeScrapeStartError(err);
          this.error = failure.message;
          this.scraping = failure.alreadyRunning;
        }
      });
  }

  cancelScrape(): void {
    this.error = null;
    this.busy = true;
    this.scrapeStatus.cancelScrape()
      .pipe(finalize(() => this.busy = false))
      .subscribe({
        error: (err) => this.error = `Cancel error: ${err.message}`
      });
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.detailsOpen && !this.host.nativeElement.contains(event.target as Node)) {
      this.detailsOpen = false;
    }
  }

  private updateStatus(status: ScrapingStatusResponse): void {
    const now = new Date();
    this.scraping = status.isScrapingInProgress;
    this.lastRun = status.lastScrapingHistory ? toRun(status.lastScrapingHistory, now) : null;
    this.lastSuccessfulRun = status.lastSuccessfulScrapingHistory ? toRun(status.lastSuccessfulScrapingHistory, now) : null;
  }
}

function toRun(history: ScrapingHistoryDto, now: Date): ScrapeRun {
  const start = parseUtcDate(history.startTimeUtc);
  return {
    status: history.status,
    // An unknown status from a newer backend keeps its name with a neutral colour.
    statusLabel: STATUS_LABEL[history.status] ?? history.status,
    tone: STATUS_TONE[history.status] ?? 'secondary',
    start,
    end: history.endTimeUtc ? parseUtcDate(history.endTimeUtc) : null,
    timeAgo: formatTimeAgo(start, now),
    flats: history.flatsScrapedCount,
    errorMessage: history.errorMessage ?? null
  };
}
