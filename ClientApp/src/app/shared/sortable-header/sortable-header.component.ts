import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TableSort } from '../../core/utils/table-sort';

/**
 * Table header cell with a sort button: `<th appSortableHeader [sort]="sort" column="priceEur"
 * sortLabel="Price" (sortRequested)="onSort($event)">Price (€)</th>`.
 * Only displays the state of `sort`; the host decides what a click does.
 */
@Component({
  selector: 'th[appSortableHeader]',
  standalone: true,
  host: {
    '[attr.aria-sort]': 'sort.ariaSort(column)'
  },
  template: `
    <button
      type="button"
      class="btn btn-link link-light p-0 text-decoration-none"
      [attr.aria-label]="'Sort by ' + sortLabel"
      (click)="sortRequested.emit(column)"
    >
      <ng-content></ng-content> <span aria-hidden="true">{{ sort.indicator(column) }}</span>
    </button>
  `
})
export class SortableHeaderComponent<T, K extends keyof T> {
  @Input({ required: true }) sort!: TableSort<T, K>;
  @Input({ required: true }) column!: K;
  /** Accessible name after "Sort by", e.g. "Price per Square Meter". */
  @Input({ required: true }) sortLabel!: string;
  @Output() sortRequested = new EventEmitter<K>();
}
