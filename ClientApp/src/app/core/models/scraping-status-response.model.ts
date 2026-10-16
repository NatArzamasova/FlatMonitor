export interface ScrapingStatusResponse {
  isScrapingInProgress: boolean;
  lastScrapingHistory?: ScrapingHistoryDto;
}

export interface ScrapingHistoryDto {
  id: number;
  startTimeUtc: Date;
  endTimeUtc?: Date;
  status: string; // "Running", "Successful", "Failed", "Canceled"
  errorMessage?: string;
  flatsScrapedCount: number;
}
