import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NgFor } from '@angular/common';
import { FormsModule } from '@angular/forms';

/**
 * Labelled compact multi-select used as a table filter:
 * `<app-multi-select-filter label="Rooms:" inputId="roomsSelect" [options]="rooms"
 *   [(selected)]="selectedRooms" (changed)="applyFilters()">`.
 * `changed` fires after `selectedChange`, so the host's bound value is already updated.
 */
@Component({
  selector: 'app-multi-select-filter',
  standalone: true,
  imports: [NgFor, FormsModule],
  host: { class: 'd-flex align-items-center gap-1' },
  template: `
    <label [for]="inputId" class="form-label mb-0 filter-label">{{ label }}</label>
    <select
      [id]="inputId"
      class="form-select form-select-sm"
      multiple
      size="1"
      [ngModel]="selected"
      (ngModelChange)="onSelectionChange($event)"
      [style.min-width.px]="minWidth"
    >
      <option disabled selected hidden>Select...</option>
      <option *ngFor="let option of options" [value]="option">
        {{ option }}
      </option>
    </select>
  `
})
export class MultiSelectFilterComponent<T> {
  @Input({ required: true }) label!: string;
  @Input({ required: true }) inputId!: string;
  @Input({ required: true }) options: readonly T[] = [];
  @Input() selected: T[] = [];
  @Input() minWidth = 140;

  @Output() selectedChange = new EventEmitter<T[]>();
  @Output() changed = new EventEmitter<void>();

  onSelectionChange(values: T[]): void {
    this.selected = values;
    this.selectedChange.emit(values);
    this.changed.emit();
  }
}
