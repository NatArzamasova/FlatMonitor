export interface ScrapeResponse {
  success: boolean;
  message: string;
  scrapedAtUtc: Date;
  flatsCount: number;
}
