import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { FlatAggregation } from '../models/flat-aggregation.model';
import { FlatOffer } from '../models/flat-offer.model';
import { FlatHistory } from '../models/flat-history.model';
import { ScrapeResponse } from '../models/scrape-response.model';
import { ScrapingStatusResponse } from '../models/scraping-status-response.model';
import { unwrapApiResponse } from '../utils/api-response.utils';

@Injectable({
  providedIn: 'root'
})
export class FlatsService {
  private apiUrl = '/api';

  constructor(private http: HttpClient) {}

  getAggregated(): Observable<FlatAggregation[]> {
    return this.http.get<ApiResponse<FlatAggregation[]>>(`${this.apiUrl}/flats/aggregated`)
      .pipe(unwrapApiResponse());
  }

  getCurrentOffers(): Observable<FlatOffer[]> {
    return this.http.get<ApiResponse<FlatOffer[]>>(`${this.apiUrl}/flats/current-offers`)
      .pipe(unwrapApiResponse());
  }

  getHistory(): Observable<FlatHistory[]> {
    return this.http.get<ApiResponse<FlatHistory[]>>(`${this.apiUrl}/flats/history`)
      .pipe(unwrapApiResponse());
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
