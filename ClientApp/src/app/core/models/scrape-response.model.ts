import { ApiResponse } from './api-response.model';

/** Data of `POST /api/scrape`. */
export interface ScrapeQueued {
  jobId: string;
  /** ISO date-time; parse with `parseUtcDate` if it is ever displayed. */
  queuedAtUtc: string;
}

/** `POST /api/scrape`: 202 with the queued job, or 409 (an HttpErrorResponse) while a scrape is queued or running. */
export type ScrapeResponse = ApiResponse<ScrapeQueued>;

/** `POST /api/scrape/cancel`: `data` says whether a queued or running scrape was canceled. */
export type CancelScrapeResponse = ApiResponse<boolean>;
