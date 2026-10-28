export interface ScrapeResponse {
  success: boolean;
  message: string;
  /** ISO date-time; parse with `parseUtcDate` if it is ever displayed. */
  scrapedAtUtc: string;
  flatsCount: number;
}

export interface CancelScrapeResponse {
  message: string;
}
