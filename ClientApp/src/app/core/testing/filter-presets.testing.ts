import { BehaviorSubject, Observable } from 'rxjs';
import { FilterPresetsService } from '../services/filter-presets.service';
import { FilterPreset } from '../models/filter-preset.model';

export interface FilterPresetsServiceStub {
  service: jasmine.SpyObj<FilterPresetsService>;
  /** Push a new list to `service.presets$`. */
  presets$: BehaviorSubject<FilterPreset[]>;
  /** Push a new active preset to `service.activePreset$`; starts with the given initial value. */
  activePreset$: BehaviorSubject<FilterPreset | null>;
}

/** FilterPresetsService replacement for component tests: streams are driven by the test, writes are spies. */
export function createFilterPresetsServiceStub(
  presets: FilterPreset[] = [],
  activePreset: FilterPreset | null = null
): FilterPresetsServiceStub {
  const presets$ = new BehaviorSubject<FilterPreset[]>(presets);
  const activePreset$ = new BehaviorSubject<FilterPreset | null>(activePreset);
  const service = jasmine.createSpyObj<FilterPresetsService>(
    'FilterPresetsService',
    ['selectPreset', 'create', 'update', 'delete', 'setDefault', 'clearDefault'],
    {
      presets$: presets$ as Observable<FilterPreset[]>,
      activePreset$: activePreset$ as Observable<FilterPreset | null>
    }
  );
  return { service, presets$, activePreset$ };
}

export function filterPreset(id: number, name: string, districts: string[] = [], isDefault = false): FilterPreset {
  return { id, name, districts, isDefault, updatedAtUtc: '2026-10-28T09:30:00Z' };
}
