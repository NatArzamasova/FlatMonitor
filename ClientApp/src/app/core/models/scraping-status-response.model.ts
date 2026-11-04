export interface ScrapingStatusResponse {
  isScrapingInProgress: boolean;
  lastScrapingHistory?: ScrapingHistoryDto | null;
  lastSuccessfulScrapingHistory?: ScrapingHistoryDto | null;
}

/** Values written by the backend's ScrapeBackgroundService. */
export type ScrapingStatus = 'Running' | 'Successful' | 'PartialFailed' | 'Failed' | 'Canceled';

export interface ScrapingHistoryDto {
  id: number;
  /** ISO date-time; may lack an offset although it is UTC — parse with `parseUtcDate`. */
  startTimeUtc: string;
  endTimeUtc?: string | null;
  status: ScrapingStatus;
  errorMessage?: string | null;
  flatsScrapedCount: number;
  /** The sources the run scraped, e.g. `['OhneMakler', 'ImmobilienScout24']`; missing for runs from before it was recorded. */
  sources?: string[] | null;
}
