import { Component, ElementRef, EventEmitter, HostListener, Input, Output, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { formatDecimal } from '../../core/utils/locale.utils';

/** Lists with more options than this get a search box. */
const SEARCH_THRESHOLD = 8;

/**
 * Labelled compact multi-select used as a table filter: a button that summarises the selection
 * ("All" / the one option / "3 selected") and opens a menu with checkboxes, "Select all", "Clear" and, for long
 * lists, a search box:
 * `<app-multi-select-filter label="Rooms:" inputId="roomsSelect" [options]="rooms"
 *   [(selected)]="selectedRooms" (changed)="applyFilters()">`.
 * `selected` keeps the order of `options`. `changed` fires after `selectedChange`, so the host's bound value is
 * already updated. An empty selection means "no filter", which is why it is summarised as "All".
 */
@Component({
  selector: 'app-multi-select-filter',
  standalone: true,
  imports: [FormsModule],
  host: { class: 'd-flex align-items-center gap-1' },
  template: `
    <label [id]="inputId + '-label'" [for]="inputId" class="form-label mb-0 filter-label">{{ label }}</label>
    <div class="dropdown" (keydown.escape)="close(true)">
      <button
        #toggleButton
        type="button"
        [id]="inputId"
        class="form-select form-select-sm text-start text-truncate multi-select-toggle"
        [class.is-filtering]="selected.length > 0"
        [style.min-width.px]="minWidth"
        aria-haspopup="true"
        [attr.aria-expanded]="open"
        [attr.aria-controls]="inputId + '-menu'"
        [attr.aria-labelledby]="inputId + '-label ' + inputId"
        (click)="toggleMenu()"
      >{{ summary }}</button>

      @if (open) {
        <div [id]="inputId + '-menu'" class="dropdown-menu show multi-select-menu" data-bs-popper="static">
          <div class="multi-select-head">
            @if (searchable) {
              <input
                #searchInput
                type="search"
                class="form-control form-control-sm mb-2"
                placeholder="Search…"
                [attr.aria-label]="'Search ' + labelText"
                [(ngModel)]="query"
              />
            }
            <div class="d-flex align-items-center gap-3 small">
              <button type="button" class="btn btn-link btn-sm p-0" (click)="selectAll()"
                [disabled]="visibleOptions.length === 0">Select all</button>
              <button type="button" class="btn btn-link btn-sm p-0" (click)="clear()"
                [disabled]="selected.length === 0">Clear</button>
              @if (selected.length > 0) {
                <span class="ms-auto text-body-secondary multi-select-count">{{ selected.length }} selected</span>
              }
            </div>
          </div>
          <div class="multi-select-options" role="group" [attr.aria-label]="labelText">
            <!-- The whole row is the label, so a click anywhere on it ticks the box. -->
            @for (item of visibleOptions; track item.index) {
              <label
                class="multi-select-option"
                [class.is-selected]="isSelected(item.option)"
                [for]="inputId + '-option-' + item.index"
              >
                <input
                  class="form-check-input"
                  type="checkbox"
                  [id]="inputId + '-option-' + item.index"
                  [checked]="isSelected(item.option)"
                  (change)="setOption(item.option, $any($event.target).checked)"
                />
                <span class="multi-select-option-label">{{ optionLabel(item.option) }}</span>
              </label>
            } @empty {
              <div class="text-body-secondary small px-2 py-1">No matches</div>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .multi-select-toggle {
      max-width: 16rem;
    }
    /* An active filter stands out from the ones showing "All". */
    .multi-select-toggle.is-filtering {
      border-color: var(--bs-primary-border-subtle);
      background-color: var(--bs-primary-bg-subtle);
      color: var(--bs-primary-text-emphasis);
      font-weight: 600;
    }
    .multi-select-menu {
      min-width: 100%;
      width: max-content;
      max-width: 20rem;
      padding: 0;
      overflow: hidden;
      border-color: var(--bs-border-color-translucent);
      border-radius: var(--bs-border-radius-lg);
      box-shadow: var(--bs-box-shadow);
    }
    .multi-select-head {
      padding: 0.5rem 0.75rem;
      border-bottom: var(--bs-border-width) solid var(--bs-border-color-translucent);
    }
    .multi-select-options {
      max-height: 16rem;
      overflow-y: auto;
      padding: 0.25rem;
    }
    /* Each option is a full-width row: a light hover, ticked rows tinted in the primary colour. */
    .multi-select-option {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0;
      padding: 0.375rem 0.5rem;
      border-radius: var(--bs-border-radius);
      cursor: pointer;
      user-select: none;
    }
    .multi-select-option:hover {
      background-color: var(--bs-tertiary-bg);
    }
    .multi-select-option.is-selected {
      background-color: var(--bs-primary-bg-subtle);
      color: var(--bs-primary-text-emphasis);
      font-weight: 600;
    }
    .multi-select-option .form-check-input {
      flex-shrink: 0;
      width: 1rem;
      height: 1rem;
      margin: 0;
      border-radius: 0.3rem;
      cursor: pointer;
    }
  `]
})
export class MultiSelectFilterComponent<T> {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  @Input({ required: true }) label!: string;
  @Input({ required: true }) inputId!: string;
  @Input({ required: true }) options: readonly T[] = [];
  @Input() selected: T[] = [];
  @Input() minWidth = 140;

  @Output() selectedChange = new EventEmitter<T[]>();
  @Output() changed = new EventEmitter<void>();

  @ViewChild('toggleButton') private toggleButton?: ElementRef<HTMLButtonElement>;

  /** Runs when the menu has opened with a search box: typing then filters straight away. */
  @ViewChild('searchInput') private set searchInput(input: ElementRef<HTMLInputElement> | undefined) {
    input?.nativeElement.focus();
  }

  open = false;
  query = '';

  /** The label without its trailing colon, for the menu's accessible names. */
  get labelText(): string {
    return this.label.replace(/:\s*$/, '');
  }

  get searchable(): boolean {
    return this.options.length > SEARCH_THRESHOLD;
  }

  get summary(): string {
    const chosen = this.options.filter(option => this.isSelected(option));
    if (chosen.length === 0) {
      return 'All';
    }
    return chosen.length === 1 ? this.optionLabel(chosen[0]) : `${chosen.length} selected`;
  }

  /** The options matching the search, with their index in `options` (stable ids while searching). */
  get visibleOptions(): { option: T; index: number }[] {
    const query = this.query.trim().toLocaleLowerCase();
    return this.options
      .map((option, index) => ({ option, index }))
      .filter(({ option }) => !query || this.optionLabel(option).toLocaleLowerCase().includes(query));
  }

  /** How an option is shown: numbers in the app locale (`2,5`), anything else as it is. */
  optionLabel(option: T): string {
    return typeof option === 'number' ? formatDecimal(option) : String(option);
  }

  isSelected(option: T): boolean {
    return this.selected.includes(option);
  }

  toggleMenu(): void {
    if (this.open) {
      this.close(false);
    } else {
      this.open = true;
      this.query = '';
    }
  }

  close(returnFocus: boolean): void {
    if (!this.open) {
      return;
    }
    this.open = false;
    if (returnFocus) {
      this.toggleButton?.nativeElement.focus();
    }
  }

  setOption(option: T, checked: boolean): void {
    this.emit(this.options.filter(candidate => candidate === option ? checked : this.isSelected(candidate)));
  }

  /** Adds every option matching the search to the selection. */
  selectAll(): void {
    const visible = new Set(this.visibleOptions.map(item => item.option));
    this.emit(this.options.filter(option => visible.has(option) || this.isSelected(option)));
  }

  clear(): void {
    this.emit([]);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open && !this.host.nativeElement.contains(event.target as Node)) {
      this.close(false);
    }
  }

  private emit(values: T[]): void {
    this.selected = values;
    this.selectedChange.emit(values);
    this.changed.emit();
  }
}
