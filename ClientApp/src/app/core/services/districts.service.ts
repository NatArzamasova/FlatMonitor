import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map, shareReplay } from 'rxjs/operators';
import { ApiResponse } from '../models/api-response.model';
import { unwrapApiResponse } from '../utils/api-response.utils';

/** All districts the monitor tracks, independent of the flats data. */
@Injectable({
  providedIn: 'root'
})
export class DistrictsService {
  private readonly apiUrl = '/api/districts';

  private readonly http = inject(HttpClient);

  /** Fetched once on first subscribe, sorted by name. Emits `[]` if loading fails. */
  readonly districts$: Observable<string[]> = this.http.get<ApiResponse<string[]>>(this.apiUrl).pipe(
    unwrapApiResponse(),
    map(districts => [...districts].sort((a, b) => a.localeCompare(b))),
    catchError((err) => {
      console.error('Failed to load districts:', err);
      return of<string[]>([]);
    }),
    shareReplay(1)
  );
}
