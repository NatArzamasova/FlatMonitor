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
      class="btn btn-link p-0 text-decoration-none sort-button"
      [attr.aria-label]="'Sort by ' + sortLabel"
      (click)="sortRequested.emit(column)"
    >
      <ng-content></ng-content>
      @if (sort.indicator(column); as arrow) {
        <span aria-hidden="true">{{ arrow }}</span>
      } @else {
        <!-- Shown on unsorted columns, so it is clear that they can be sorted. -->
        <span class="sort-hint" aria-hidden="true">↕</span>
      }
    </button>
  `,
  // The button looks like the header text around it (size, colour, case), not like a link.
  styles: [`
    .sort-button {
      font: inherit;
      letter-spacing: inherit;
      text-transform: inherit;
      color: inherit;
      vertical-align: baseline;
    }
    .sort-button:hover {
      color: var(--bs-emphasis-color);
    }
    .sort-hint {
      opacity: 0.4;
    }
  `]
})
export class SortableHeaderComponent<T, K extends keyof T> {
  @Input({ required: true }) sort!: TableSort<T, K>;
  @Input({ required: true }) column!: K;
  /** Accessible name after "Sort by", e.g. "Price per Square Meter". */
  @Input({ required: true }) sortLabel!: string;
  @Output() sortRequested = new EventEmitter<K>();
}
