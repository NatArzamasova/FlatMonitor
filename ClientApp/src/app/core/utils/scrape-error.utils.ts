import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';

/** Why `POST /api/scrape` failed, worded for the user. */
export interface ScrapeStartFailure {
  message: string;
  /** 409: a scrape is already queued or running, so the caller should show the running state. */
  alreadyRunning: boolean;
}

/** Shared by every "Scrape now" button, so they report failures the same way. */
export function describeScrapeStartError(err: unknown): ScrapeStartFailure {
  // 409: a scrape is already queued or running; the API explains that in the body
  if (err instanceof HttpErrorResponse && err.status === HttpStatusCode.Conflict) {
    return { message: err.error?.message || 'A scrape is already queued or running', alreadyRunning: true };
  }
  return { message: `Scrape error: ${(err as { message?: string })?.message ?? String(err)}`, alreadyRunning: false };
}
