import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { FlatAggregation } from '../models/flat-aggregation.model';
import { FlatOffer } from '../models/flat-offer.model';
import { FlatHistory } from '../models/flat-history.model';
import { ScrapeResponse } from '../models/scrape-response.model';
import { ScrapingStatusResponse } from '../models/scraping-status-response.model';

@Injectable({
  providedIn: 'root'
})
export class FlatsService {
  private apiUrl = '/api';

  constructor(private http: HttpClient) {}

  getAggregated(): Observable<ApiResponse<FlatAggregation[]>> {
    return this.http.get<ApiResponse<FlatAggregation[]>>(`${this.apiUrl}/flats/aggregated`);
  }

  getCurrentOffers(): Observable<ApiResponse<FlatOffer[]>> {
    return this.http.get<ApiResponse<FlatOffer[]>>(`${this.apiUrl}/flats/current-offers`);
  }

  getHistory(): Observable<ApiResponse<FlatHistory[]>> {
    return this.http.get<ApiResponse<FlatHistory[]>>(`${this.apiUrl}/flats/history`);
  }

  triggerScrape(): Observable<ScrapeResponse> {
    return this.http.post<ScrapeResponse>(`${this.apiUrl}/scrape`, {});
  }

  getScrapeStatus(): Observable<ScrapingStatusResponse> {
    return this.http.get<ScrapingStatusResponse>(`${this.apiUrl}/scrape/status`);
  }

  cancelScrape(): Observable<any> {
    return this.http.post(`${this.apiUrl}/scrape/cancel`, {});
  }
}
