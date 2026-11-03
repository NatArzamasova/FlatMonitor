import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { FlatOffer } from '../../../../../core/models/flat-offer.model';
import { TableSort } from '../../../../../core/utils/table-sort';
import { roomsLabel } from '../../../../../core/utils/filter-chips.utils';
import { OFFER_SORT_COLUMNS, OfferRow, OfferSortColumn } from '../offer-row';

/**
 * The current offers as cards, for small screens where the table would scroll sideways:
 * `<app-offer-card-list [rows]="offerRows" [sort]="sort" (sortRequested)="onSort($event)">`.
 * Its sort control emits the same `sortRequested` as the table's header buttons: choosing a column sorts it
 * ascending, the direction button asks for the current column again, which toggles the direction. Below the sort
 * control the list scrolls inside the height its host gets (`.card-list-fill`).
 */
@Component({
  selector: 'app-offer-card-list',
  standalone: true,
  imports: [CurrencyPipe, DecimalPipe],
  template: `
    <div class="d-flex align-items-center gap-2 mb-2 offer-sort">
      <label for="offerCardSort" class="form-label mb-0 filter-label">Sort by:</label>
      <select id="offerCardSort" class="form-select form-select-sm w-auto"
        [value]="sort.column ?? ''" (change)="chooseColumn($any($event.target).value)">
        @if (sort.column === null) {
          <option value="">Default order</option>
        }
        @for (item of columns; track item.column) {
          <option [value]="item.column" [selected]="item.column === sort.column">{{ item.label }}</option>
        }
      </select>
      <button type="button" class="btn btn-sm btn-outline-secondary offer-sort-direction"
        [disabled]="sort.column === null" (click)="toggleDirection()"
        [attr.aria-label]="directionLabel" [title]="directionLabel">
        <span aria-hidden="true">{{ sort.direction === 'asc' ? '↑' : '↓' }}</span>
      </button>
    </div>

    <ul class="list-unstyled mb-0 offer-cards">
      <!-- The offer objects survive filtering and sorting, so their cards are moved instead of re-created. -->
      @for (row of rows; track row.offer) {
        <li>
          <article class="card offer-card">
            <div class="card-body py-2 px-3">
              <h3 class="h6 mb-1 offer-card-title">{{ title(row.offer) }}</h3>
              <div class="d-flex align-items-baseline flex-wrap gap-1">
                <span class="fs-5 fw-semibold offer-card-price">
                  {{ row.offer.priceEur ? (row.offer.priceEur | currency: 'EUR': 'symbol': '1.0-0') : '-' }}
                </span>
                @if (row.discountPercentage !== null) {
                  <span class="badge rounded-pill bg-success-subtle text-success-emphasis price-drop"
                    [title]="'Was ' + (row.offer.originalPriceEur | currency: 'EUR': 'symbol': '1.0-0') + ' when first listed'">−{{ row.discountPercentage | number: '1.0-1' }} %</span>
                }
              </div>
              @if (row.offer.availability) {
                <div class="small mt-1 text-truncate offer-card-availability" [title]="row.offer.availability">
                  <span class="text-body-secondary">Available:</span> {{ row.offer.availability }}
                </div>
              }
              <div class="d-flex align-items-center justify-content-between gap-2 mt-1">
                <span class="d-flex align-items-center gap-2">
                @if (row.energyClass; as energyClass) {
                  <span class="badge ee-badge" [class]="energyClass.className"
                    [title]="'Energy efficiency class ' + energyClass.label">{{ energyClass.label }}</span>
                }
                <span class="text-body-secondary small offer-card-details">
                  {{ row.offer.livingAreaSqm ? (row.offer.livingAreaSqm | number: '1.0-1') + ' m²' : '- m²' }}
                  ·
                  {{ row.offer.pricePerMeter ? (row.offer.pricePerMeter | currency: 'EUR': 'symbol': '1.0-0') + ' / m²' : '- / m²' }}
                </span>
                </span>
                <!-- Same markup as the table's source cell, so the link has the same accessible name. -->
                @if (row.hasLink) {
                  <a [href]="row.offer.url" target="_blank" rel="noopener noreferrer" class="source-link"
                    [title]="'Open the offer on ' + (row.offer.source || 'its portal')">
                    <span class="badge rounded-pill source-badge" [class]="row.source.className">{{ row.source.label }}</span>
                    <span aria-hidden="true">↗</span>
                  </a>
                } @else {
                  <span class="badge rounded-pill source-badge" [class]="row.source.className">{{ row.source.label }}</span>
                }
              </div>
            </div>
          </article>
        </li>
      }
    </ul>
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      min-height: 0;
    }
    .offer-cards {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      flex: 1 1 0;
      min-height: 0;
      overflow-y: auto;
    }
    .offer-card-price {
      font-variant-numeric: tabular-nums;
    }
  `]
})
export class OfferCardListComponent {
  @Input({ required: true }) rows: readonly OfferRow[] = [];
  @Input({ required: true }) sort!: TableSort<FlatOffer, OfferSortColumn>;
  @Output() sortRequested = new EventEmitter<OfferSortColumn>();

  readonly columns = OFFER_SORT_COLUMNS;

  get directionLabel(): string {
    return this.sort.direction === 'asc' ? 'Ascending (switch to descending)' : 'Descending (switch to ascending)';
  }

  /** `Südstadt · 3 rooms`; without a room count only the district. */
  title(offer: FlatOffer): string {
    const district = offer.district || '-';
    return offer.roomsCount ? `${district} · ${roomsLabel(offer.roomsCount)}` : district;
  }

  chooseColumn(value: string): void {
    const column = this.columns.find(item => item.column === value)?.column;
    if (column && column !== this.sort.column) {
      this.sortRequested.emit(column);
    }
  }

  toggleDirection(): void {
    if (this.sort.column !== null) {
      this.sortRequested.emit(this.sort.column);
    }
  }
}
