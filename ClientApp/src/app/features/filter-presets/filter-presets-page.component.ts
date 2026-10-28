import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { FilterPresetsService } from '../../core/services/filter-presets.service';
import { DistrictsService } from '../../core/services/districts.service';
import { FilterPreset } from '../../core/models/filter-preset.model';
import { describeWriteError } from '../../core/utils/api-response.utils';
import { sameElements } from '../../core/utils/collection.utils';

/** The preset being edited; `id: null` is a new preset. */
interface PresetDraft {
  id: number | null;
  name: string;
  districts: string[];
}

/**
 * `/presets`: create, rename, edit districts, set the default and delete filter presets.
 * Opened in its own browser tab from the dashboard; open dashboard tabs pick up changes through `FilterPresetsService`.
 */
@Component({
  selector: 'app-filter-presets-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './filter-presets-page.component.html'
})
export class FilterPresetsPageComponent implements OnInit {
  private readonly presetsService = inject(FilterPresetsService);
  private readonly districtsService = inject(DistrictsService);
  private readonly destroyRef = inject(DestroyRef);

  presets: FilterPreset[] = [];
  districts: string[] = [];
  draft: PresetDraft | null = null;
  busy = false;
  error: string | null = null;

  /** The stored preset behind the draft; `null` for a new preset or nothing selected. */
  get editedPreset(): FilterPreset | null {
    const id = this.draft?.id;
    return this.presets.find(preset => preset.id === id) ?? null;
  }

  get isDirty(): boolean {
    const draft = this.draft;
    if (!draft) {
      return false;
    }
    const stored = this.editedPreset;
    if (!stored) {
      return true;
    }
    return draft.name.trim() !== stored.name || !sameElements(draft.districts, stored.districts);
  }

  /** Districts from the API plus any stored in the draft but unknown to the API, so they can be unticked. */
  get districtOptions(): string[] {
    const extra = (this.draft?.districts ?? []).filter(district => !this.districts.includes(district));
    return [...this.districts, ...extra];
  }

  ngOnInit(): void {
    this.presetsService.presets$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(presets => this.onPresetsChanged(presets));

    this.districtsService.districts$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(districts => this.districts = districts);
  }

  edit(preset: FilterPreset): void {
    this.error = null;
    this.draft = { id: preset.id, name: preset.name, districts: [...preset.districts] };
  }

  startNew(): void {
    this.error = null;
    this.draft = { id: null, name: '', districts: [] };
  }

  cancel(): void {
    this.error = null;
    const stored = this.editedPreset;
    this.draft = stored ? { id: stored.id, name: stored.name, districts: [...stored.districts] } : null;
  }

  isChecked(district: string): boolean {
    return this.draft?.districts.includes(district) ?? false;
  }

  toggleDistrict(district: string, checked: boolean): void {
    if (!this.draft) {
      return;
    }
    const others = this.draft.districts.filter(d => d !== district);
    this.draft.districts = checked ? [...others, district] : others;
  }

  clearDistricts(): void {
    if (this.draft) {
      this.draft.districts = [];
    }
  }

  save(): void {
    const draft = this.draft;
    const name = draft?.name.trim();
    if (!draft || !name || this.busy) {
      return;
    }
    const request = { name, districts: draft.districts };

    if (draft.id === null) {
      this.run(this.presetsService.create(request), created => this.edit(created));
    } else {
      this.run(this.presetsService.update(draft.id, request), updated => this.edit(updated));
    }
  }

  toggleDefault(): void {
    const stored = this.editedPreset;
    if (!stored) {
      return;
    }
    const request: Observable<unknown> = stored.isDefault
      ? this.presetsService.clearDefault()
      : this.presetsService.setDefault(stored.id);
    this.run(request);
  }

  deletePreset(): void {
    const stored = this.editedPreset;
    if (!stored || !confirm(`Delete preset "${stored.name}"?`)) {
      return;
    }
    this.run(this.presetsService.delete(stored.id), () => this.draft = null);
  }

  /**
   * Keeps unsaved edits when the list changes (e.g. a save in another browser tab), refreshes an unchanged draft,
   * and closes the editor when its preset was deleted.
   */
  private onPresetsChanged(presets: FilterPreset[]): void {
    const wasDirty = this.isDirty;
    const draft = this.draft;
    this.presets = presets;

    if (!draft || draft.id === null) {
      return;
    }
    const stored = this.editedPreset;
    if (!stored) {
      this.draft = null;
    } else if (!wasDirty) {
      this.edit(stored);
    }
  }

  private run<T>(request: Observable<T>, onSuccess?: (result: T) => void): void {
    this.busy = true;
    this.error = null;
    request
      .pipe(finalize(() => this.busy = false))
      .subscribe({
        next: result => onSuccess?.(result),
        error: err => this.error = describeWriteError(err)
      });
  }
}
