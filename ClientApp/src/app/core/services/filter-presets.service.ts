import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, defer, Observable, of } from 'rxjs';
import { catchError, filter, map, tap } from 'rxjs/operators';
import { ApiResponse } from '../models/api-response.model';
import { FilterPreset, SaveFilterPresetRequest } from '../models/filter-preset.model';
import { unwrapApiResponse } from '../utils/api-response.utils';

/**
 * Named district filter presets, shared by all tabs. The list is fetched once on first subscribe and
 * kept up to date by the write methods, so every subscriber sees changes made in any tab.
 */
@Injectable({
  providedIn: 'root'
})
export class FilterPresetsService {
  private readonly apiUrl = '/api/filter-presets';

  private readonly http = inject(HttpClient);

  /** `null` until the first load has finished. */
  private readonly presetsState = new BehaviorSubject<FilterPreset[] | null>(null);
  private loadRequested = false;

  /** All presets ordered by name. Emits `[]` if loading fails, so a tab never breaks because of presets. */
  readonly presets$: Observable<FilterPreset[]> = defer(() => {
    this.load();
    return this.presetsState;
  }).pipe(filter((presets): presets is FilterPreset[] => presets !== null));

  /** The preset applied when the dashboard opens, or `null` if there is none. */
  readonly defaultPreset$: Observable<FilterPreset | null> = this.presets$.pipe(
    map(presets => presets.find(preset => preset.isDefault) ?? null)
  );

  create(request: SaveFilterPresetRequest): Observable<FilterPreset> {
    return this.http.post<ApiResponse<FilterPreset>>(this.apiUrl, request).pipe(
      unwrapApiResponse(),
      tap(created => this.updateCache(presets => [...presets, created]))
    );
  }

  update(id: number, request: SaveFilterPresetRequest): Observable<FilterPreset> {
    return this.http.put<ApiResponse<FilterPreset>>(`${this.apiUrl}/${id}`, request).pipe(
      unwrapApiResponse(),
      tap(updated => this.updateCache(presets => presets.map(preset => preset.id === id ? updated : preset)))
    );
  }

  /** Emits the remaining presets. */
  delete(id: number): Observable<FilterPreset[]> {
    return this.http.delete<ApiResponse<FilterPreset[]>>(`${this.apiUrl}/${id}`).pipe(
      unwrapApiResponse(),
      tap(presets => this.presetsState.next(presets))
    );
  }

  setDefault(id: number): Observable<FilterPreset> {
    return this.http.put<ApiResponse<FilterPreset>>(`${this.apiUrl}/${id}/default`, {}).pipe(
      unwrapApiResponse(),
      tap(newDefault => this.updateCache(presets => presets.map(preset =>
        preset.id === id ? newDefault : { ...preset, isDefault: false }
      )))
    );
  }

  /** Leaves no preset as default; emits all presets. */
  clearDefault(): Observable<FilterPreset[]> {
    return this.http.delete<ApiResponse<FilterPreset[]>>(`${this.apiUrl}/default`).pipe(
      unwrapApiResponse(),
      tap(presets => this.presetsState.next(presets))
    );
  }

  private load(): void {
    if (this.loadRequested) {
      return;
    }
    this.loadRequested = true;

    this.http.get<ApiResponse<FilterPreset[]>>(this.apiUrl).pipe(
      unwrapApiResponse(),
      catchError((err) => {
        console.error('Failed to load filter presets:', err);
        // Let the next subscriber try again instead of keeping the empty fallback forever.
        this.loadRequested = false;
        return of<FilterPreset[]>([]);
      })
    ).subscribe(presets => this.presetsState.next(presets));
  }

  /** Applies a change to the loaded list and keeps it ordered by name like the backend does. */
  private updateCache(change: (presets: FilterPreset[]) => FilterPreset[]): void {
    const current = this.presetsState.value;
    if (current === null) {
      // Not loaded yet: the pending/next load brings the change from the backend.
      return;
    }
    this.presetsState.next(sortByName(change(current)));
  }
}

function sortByName(presets: FilterPreset[]): FilterPreset[] {
  return [...presets].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
}
