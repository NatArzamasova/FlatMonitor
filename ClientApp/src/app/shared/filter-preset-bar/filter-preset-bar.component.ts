import { Component, DestroyRef, Input, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { FilterPresetsService } from '../../core/services/filter-presets.service';
import { FilterPreset } from '../../core/models/filter-preset.model';
import { ApiResponseError } from '../../core/utils/api-response.utils';
import { sameElements } from '../../core/utils/collection.utils';

/**
 * Preset dropdown and actions for the district filter:
 * `<app-filter-preset-bar [selected]="selectedDistricts"></app-filter-preset-bar>`.
 * Choosing a preset goes through `FilterPresetsService.selectPreset`; the host tab applies the districts by
 * subscribing to `activePreset$`. `selected` is the host's current district selection, used by Save and Save as new.
 */
@Component({
  selector: 'app-filter-preset-bar',
  standalone: true,
  imports: [FormsModule],
  host: { class: 'd-flex align-items-center gap-1 flex-wrap' },
  template: `
    <label [for]="inputId" class="form-label mb-0 filter-label">Preset:</label>
    <select
      [id]="inputId"
      class="form-select form-select-sm w-auto"
      [ngModel]="activePresetId"
      (ngModelChange)="onPresetChosen($event)"
      [disabled]="busy"
    >
      <option [ngValue]="null">— none —</option>
      @for (preset of presets; track preset.id) {
        <option [ngValue]="preset.id">{{ preset.isDefault ? '★ ' : '' }}{{ preset.name }}</option>
      }
    </select>

    @if (nameMode) {
      <input
        type="text"
        class="form-control form-control-sm w-auto"
        [attr.aria-label]="nameMode === 'create' ? 'New preset name' : 'Preset name'"
        placeholder="Preset name"
        maxlength="100"
        [(ngModel)]="nameDraft"
        (keydown.enter)="submitName()"
        (keydown.escape)="cancelNaming()"
      />
      <button type="button" class="btn btn-sm btn-primary" (click)="submitName()" [disabled]="busy || !nameDraft.trim()">
        {{ nameMode === 'create' ? 'Create' : 'Rename' }}
      </button>
      <button type="button" class="btn btn-sm btn-outline-secondary" (click)="cancelNaming()" [disabled]="busy">Cancel</button>
    } @else {
      @if (isModified) {
        <small class="text-muted fst-italic">modified</small>
      }
      <button type="button" class="btn btn-sm btn-outline-secondary" (click)="save()"
        [disabled]="busy || !activePreset || !isModified" title="Overwrite the preset with the current districts">Save</button>
      <button type="button" class="btn btn-sm btn-outline-secondary" (click)="startNaming('create')"
        [disabled]="busy" title="Create a new preset from the current districts">Save as new…</button>
      <button type="button" class="btn btn-sm btn-outline-secondary" (click)="startNaming('rename')"
        [disabled]="busy || !activePreset">Rename…</button>
      <button type="button" class="btn btn-sm btn-outline-secondary" (click)="toggleDefault()"
        [disabled]="busy || !activePreset" title="The default preset is applied when the dashboard opens">
        {{ activePreset?.isDefault ? 'Unset default' : 'Set as default' }}
      </button>
      <button type="button" class="btn btn-sm btn-outline-danger" (click)="deletePreset()"
        [disabled]="busy || !activePreset">Delete</button>
    }

    @if (error) {
      <small class="text-danger" role="alert">{{ error }}</small>
    }
  `
})
export class FilterPresetBarComponent implements OnInit {
  private readonly presetsService = inject(FilterPresetsService);
  private readonly destroyRef = inject(DestroyRef);

  /** The host tab's current district selection. */
  @Input() selected: string[] = [];
  @Input() inputId = 'presetSelect';

  presets: FilterPreset[] = [];
  activePresetId: number | null = null;
  nameMode: 'create' | 'rename' | null = null;
  nameDraft = '';
  busy = false;
  error: string | null = null;

  /** Looked up in `presets`, so a rename or a new default flag shows without `activePreset$` re-emitting. */
  get activePreset(): FilterPreset | null {
    return this.presets.find(preset => preset.id === this.activePresetId) ?? null;
  }

  /** The current selection differs from the active preset's districts. */
  get isModified(): boolean {
    const active = this.activePreset;
    return active !== null && !sameElements(active.districts, this.selected);
  }

  ngOnInit(): void {
    this.presetsService.presets$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(presets => this.presets = presets);

    this.presetsService.activePreset$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(preset => this.activePresetId = preset?.id ?? null);
  }

  onPresetChosen(id: number | null): void {
    this.error = null;
    this.presetsService.selectPreset(id);
  }

  save(): void {
    const active = this.activePreset;
    if (!active) {
      return;
    }
    this.run(this.presetsService.update(active.id, { name: active.name, districts: this.selected }));
  }

  startNaming(mode: 'create' | 'rename'): void {
    this.error = null;
    this.nameMode = mode;
    this.nameDraft = mode === 'rename' ? this.activePreset?.name ?? '' : '';
  }

  cancelNaming(): void {
    this.nameMode = null;
    this.nameDraft = '';
    this.error = null;
  }

  submitName(): void {
    const name = this.nameDraft.trim();
    if (!name || this.busy) {
      return;
    }

    if (this.nameMode === 'create') {
      this.run(this.presetsService.create({ name, districts: this.selected }), created => {
        this.cancelNaming();
        this.presetsService.selectPreset(created.id);
      });
    } else if (this.nameMode === 'rename' && this.activePreset) {
      // A rename keeps the preset's stored districts, even if the current selection differs.
      const active = this.activePreset;
      this.run(this.presetsService.update(active.id, { name, districts: active.districts }), () => this.cancelNaming());
    }
  }

  toggleDefault(): void {
    const active = this.activePreset;
    if (!active) {
      return;
    }
    const request: Observable<unknown> = active.isDefault
      ? this.presetsService.clearDefault()
      : this.presetsService.setDefault(active.id);
    this.run(request);
  }

  deletePreset(): void {
    const active = this.activePreset;
    if (!active || !confirm(`Delete preset "${active.name}"?`)) {
      return;
    }
    this.run(this.presetsService.delete(active.id));
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

/** The API's own message (e.g. "A filter preset named 'East' already exists") when there is one. */
function describeWriteError(err: unknown): string {
  if (err instanceof ApiResponseError && err.message) {
    return err.message;
  }
  // HttpErrorResponse: the body is the backend's ApiResponse for 400/404/409/500.
  const apiMessage = (err as { error?: { message?: unknown } })?.error?.message;
  if (typeof apiMessage === 'string' && apiMessage) {
    return apiMessage;
  }
  return 'Could not save the preset. Is the backend running?';
}
