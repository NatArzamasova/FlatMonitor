import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FilterChip, filterChipKey } from '../../core/utils/filter-chips.utils';

let nextId = 0;

/**
 * A tab's filter controls in a card, with the active filters as removable chips and "Reset filters" below:
 * `<app-filter-toolbar [chips]="filterChips" (chipRemoved)="removeFilterChip($event)" (reset)="resetFilters()">
 *   …filter rows…</app-filter-toolbar>`.
 * Below the md breakpoint the card is collapsed behind a "Filters" button; the chips stay visible.
 */
@Component({
  selector: 'app-filter-toolbar',
  standalone: true,
  host: { class: 'd-block mb-3' },
  template: `
    <button
      type="button"
      class="btn btn-sm btn-outline-secondary d-md-none mb-2 filter-toolbar-toggle"
      [attr.aria-expanded]="expanded"
      [attr.aria-controls]="cardId"
      (click)="expanded = !expanded"
    >
      Filters
      @if (chips.length > 0) {
        <span class="badge rounded-pill text-bg-primary ms-1">{{ chips.length }}</span>
      }
    </button>

    <!-- Hidden below md unless expanded; always shown from md. -->
    <div [id]="cardId" class="card filter-toolbar d-md-block" [class.d-none]="!expanded">
      <div class="card-body py-2 px-3 d-flex flex-column gap-2">
        <ng-content></ng-content>
      </div>
    </div>

    @if (chips.length > 0) {
      <div class="d-flex flex-wrap align-items-center gap-2 mt-2">
        <ul class="list-unstyled d-flex flex-wrap gap-2 mb-0" aria-label="Active filters">
          @for (chip of chips; track key(chip)) {
            <li class="filter-chip">
              {{ chip.label }}
              <button type="button" class="filter-chip-remove" [attr.aria-label]="'Remove filter ' + chip.label"
                (click)="chipRemoved.emit(chip)">×</button>
            </li>
          }
        </ul>
        <button type="button" class="btn btn-link btn-sm p-0 filter-reset" (click)="reset.emit()">Reset filters</button>
      </div>
    }
  `,
  styles: [`
    .filter-chip {
      display: inline-flex;
      align-items: center;
      gap: 0.125rem;
      padding: 0.125rem 0.125rem 0.125rem 0.625rem;
      font-size: 0.8125rem;
      font-weight: 500;
      color: var(--bs-primary-text-emphasis);
      background-color: var(--bs-primary-bg-subtle);
      border: var(--bs-border-width) solid var(--bs-primary-border-subtle);
      border-radius: var(--bs-border-radius-pill);
    }
    .filter-chip-remove {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 1.25rem;
      height: 1.25rem;
      padding: 0;
      font-size: 1rem;
      line-height: 1;
      color: inherit;
      background: none;
      border: 0;
      border-radius: 50%;
    }
    .filter-chip-remove:hover,
    .filter-chip-remove:focus-visible {
      background-color: var(--bs-primary-border-subtle);
    }
  `]
})
export class FilterToolbarComponent {
  @Input() chips: readonly FilterChip[] = [];
  @Output() chipRemoved = new EventEmitter<FilterChip>();
  @Output() reset = new EventEmitter<void>();

  readonly cardId = `filter-toolbar-${nextId++}`;
  expanded = false;

  readonly key = filterChipKey;
}
