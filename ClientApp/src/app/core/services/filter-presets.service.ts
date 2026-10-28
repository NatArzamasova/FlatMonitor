import { Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, combineLatest, defer, EMPTY, Observable, of } from 'rxjs';
import { catchError, distinctUntilChanged, filter, map, tap } from 'rxjs/operators';
import { ApiResponse } from '../models/api-response.model';
import { FilterPreset, SaveFilterPresetRequest } from '../models/filter-preset.model';
import { unwrapApiResponse } from '../utils/api-response.utils';
import { sameElements } from '../utils/collection.utils';
import { PresetsChannel } from './presets-channel.service';

/**
 * Named district filter presets, shared by all tabs. The list is fetched once on first subscribe and
 * kept up to date by the write methods, so every subscriber sees changes made in any tab.
 * Also holds the preset chosen in the UI, so switching tabs keeps it.
 * Other browser tabs (e.g. the `/presets` page) are told about every write through `PresetsChannel`
 * and reload the list.
 */
@Injectable({
  providedIn: 'root'
})
export class FilterPresetsService {
  private readonly apiUrl = '/api/filter-presets';

  private readonly http = inject(HttpClient);
  private readonly channel = inject(PresetsChannel);

  /** `null` until the first load has finished. */
  private readonly presetsState = new BehaviorSubject<FilterPreset[] | null>(null);
  private loadRequested = false;

  /** `undefined` until the user picks something: then the default preset is the active one. `null` = no preset. */
  private readonly chosenPresetId = new BehaviorSubject<number | null | undefined>(undefined);

  /** All presets ordered by name. Emits `[]` if loading fails, so a tab never breaks because of presets. */
  readonly presets$: Observable<FilterPreset[]> = defer(() => {
    this.load();
    return this.presetsState;
  }).pipe(filter((presets): presets is FilterPreset[] => presets !== null));

  /** The preset applied when the dashboard opens, or `null` if there is none. */
  readonly defaultPreset$: Observable<FilterPreset | null> = this.presets$.pipe(
    map(presets => presets.find(preset => preset.isDefault) ?? null)
  );

  /**
   * The preset whose districts the tabs show: the one chosen in any tab, or the default until something is chosen.
   * Re-emits only when the active preset or its districts change, not on a rename or on changes to other presets,
   * so tabs can re-apply its districts on every emission without losing manual edits.
   */
  readonly activePreset$: Observable<FilterPreset | null> = combineLatest([this.presets$, this.chosenPresetId]).pipe(
    map(([presets, chosenId]) => chosenId === undefined
      ? presets.find(preset => preset.isDefault) ?? null
      : presets.find(preset => preset.id === chosenId) ?? null),
    distinctUntilChanged((previous, current) =>
      previous?.id === current?.id && sameElements(previous?.districts ?? [], current?.districts ?? []))
  );

  constructor() {
    this.channel.changed$
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.reloadAfterRemoteChange());
  }

  /** Makes a preset the active one in all tabs; `null` detaches from any preset and keeps the current selection. */
  selectPreset(id: number | null): void {
    this.chosenPresetId.next(id);
  }

  create(request: SaveFilterPresetRequest): Observable<FilterPreset> {
    return this.http.post<ApiResponse<FilterPreset>>(this.apiUrl, request).pipe(
      unwrapApiResponse(),
      tap(() => this.channel.notifyChanged()),
      tap(created => this.updateCache(presets => [...presets, created]))
    );
  }

  update(id: number, request: SaveFilterPresetRequest): Observable<FilterPreset> {
    return this.http.put<ApiResponse<FilterPreset>>(`${this.apiUrl}/${id}`, request).pipe(
      unwrapApiResponse(),
      tap(() => this.channel.notifyChanged()),
      tap(updated => this.updateCache(presets => presets.map(preset => preset.id === id ? updated : preset)))
    );
  }

  /** Emits the remaining presets. */
  delete(id: number): Observable<FilterPreset[]> {
    return this.http.delete<ApiResponse<FilterPreset[]>>(`${this.apiUrl}/${id}`).pipe(
      unwrapApiResponse(),
      tap(() => this.channel.notifyChanged()),
      tap(presets => {
        if (this.chosenPresetId.value === id) {
          this.chosenPresetId.next(null);
        }
        this.presetsState.next(presets);
      })
    );
  }

  setDefault(id: number): Observable<FilterPreset> {
    return this.http.put<ApiResponse<FilterPreset>>(`${this.apiUrl}/${id}/default`, {}).pipe(
      unwrapApiResponse(),
      tap(() => this.channel.notifyChanged()),
      tap(() => this.pinActivePreset()),
      tap(newDefault => this.updateCache(presets => presets.map(preset =>
        preset.id === id ? newDefault : { ...preset, isDefault: false }
      )))
    );
  }

  /** Leaves no preset as default; emits all presets. */
  clearDefault(): Observable<FilterPreset[]> {
    return this.http.delete<ApiResponse<FilterPreset[]>>(`${this.apiUrl}/default`).pipe(
      unwrapApiResponse(),
      tap(() => this.channel.notifyChanged()),
      tap(presets => {
        this.pinActivePreset();
        this.presetsState.next(presets);
      })
    );
  }

  /**
   * While nothing is chosen the active preset follows the default. Before the default changes, fix the
   * active preset to the current default so the tabs' selection and the shown preset don't jump.
   */
  private pinActivePreset(): void {
    if (this.chosenPresetId.value !== undefined) {
      return;
    }
    const currentDefault = this.presetsState.value?.find(preset => preset.isDefault);
    this.chosenPresetId.next(currentDefault?.id ?? null);
  }

  /**
   * Another browser tab changed the presets. Same rules as a local write: the active preset doesn't jump when
   * the default changes, and a deleted chosen preset becomes "no preset" (the tabs keep their selection).
   */
  private reloadAfterRemoteChange(): void {
    if (this.presetsState.value === null) {
      // Never loaded, or the first load is still running: the list is fetched fresh anyway.
      return;
    }

    this.http.get<ApiResponse<FilterPreset[]>>(this.apiUrl).pipe(
      unwrapApiResponse(),
      catchError((err) => {
        console.error('Failed to reload filter presets:', err);
        return EMPTY;
      })
    ).subscribe(presets => {
      this.pinActivePreset();
      const chosenId = this.chosenPresetId.value;
      if (typeof chosenId === 'number' && !presets.some(preset => preset.id === chosenId)) {
        this.chosenPresetId.next(null);
      }
      this.presetsState.next(presets);
    });
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
