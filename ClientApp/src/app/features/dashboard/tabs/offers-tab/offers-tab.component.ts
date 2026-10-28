import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FlatsService } from '../../../../core/services/flats.service';
import { FilterService } from '../../../../core/services/filter.service';
import { FlatOffer } from '../../../../core/models/flat-offer.model';
import { isHttpUrl } from '../../../../core/utils/url.utils';

@Component({
  selector: 'app-offers-tab',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './offers-tab.component.html',
})
export class OffersTabComponent implements OnInit {
  private readonly sortableColumns = [
    'district',
    'roomsCount',
    'priceEur',
    'originalPriceEur',
    'livingAreaSqm',
    'pricePerMeter',
    'source'
  ] as const;
  private sortColumn: typeof this.sortableColumns[number] | null = null;
  private sortDirection: 'asc' | 'desc' = 'asc';

  currentOffers: FlatOffer[] = [];
  filteredOffers: FlatOffer[] = [];
  loadingOffers = false;
  error: string | null = null;

  selectedOfferDistricts: string[] = [];
  selectedOfferRooms: number[] = [];
  priceRangeMin: number | null = null;
  priceRangeMax: number | null = null;
  availableOfferDistricts: string[] = [];
  availableOfferRooms: number[] = [];

  isHttpUrl = isHttpUrl;

  constructor(
    private flatsService: FlatsService,
    private filterService: FilterService
  ) {}

  ngOnInit(): void {
    this.loadInitialFiltersFromUrl();
    this.loadCurrentOffers();
  }

  hasChangedPrice(offer: FlatOffer): boolean {
    return offer.priceEur != null &&
      offer.originalPriceEur != null &&
      offer.priceEur !== offer.originalPriceEur;
  }

  getDiscountPercentage(offer: FlatOffer): number | null {
    if (!this.hasChangedPrice(offer) || offer.originalPriceEur! <= 0) {
      return null;
    }

    const discountPercentage = ((offer.originalPriceEur! - offer.priceEur!) / offer.originalPriceEur!) * 100;
    return discountPercentage > 0 ? discountPercentage : null;
  }

  private loadInitialFiltersFromUrl(): void {
    const params = new URLSearchParams(window.location.search);
    this.selectedOfferDistricts = params.getAll('district');
    this.selectedOfferRooms = params.getAll('rooms')
      .map(value => Number(value))
      .filter(value => Number.isFinite(value));
  }

  loadCurrentOffers(): void {
    this.loadingOffers = true;
    this.error = null;
    this.flatsService.getCurrentOffers().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.currentOffers = response.data;
          this.updateAvailableOfferFilters();
          this.applyOfferFilters();
        } else {
          this.error = response.message || 'Failed to load offers';
        }
        this.loadingOffers = false;
      },
      error: (err) => {
        this.error = `Error loading offers: ${err.message}`;
        this.loadingOffers = false;
      }
    });
  }

  private updateAvailableOfferFilters(): void {
    const districts = new Set<string>();
    const rooms = new Set<number>();

    this.currentOffers.forEach(offer => {
      if (offer.district) {
        districts.add(offer.district);
      }
      if (offer.roomsCount !== undefined && offer.roomsCount !== null) {
        rooms.add(Math.round(offer.roomsCount));
      }
    });

    this.availableOfferDistricts = Array.from(districts).sort();
    this.availableOfferRooms = Array.from(rooms).sort((a, b) => a - b);
  }

  applyOfferFilters(): void {
    let filtered = this.currentOffers;
    filtered = this.filterService.filterByDistricts(filtered, this.selectedOfferDistricts, offer => offer.district);
    filtered = this.filterService.filterByRooms(
      filtered,
      this.selectedOfferRooms,
      offer => offer.roomsCount !== undefined ? Math.round(offer.roomsCount) : undefined
    );
    filtered = this.filterService.filterByPriceRange(
      filtered,
      this.priceRangeMin,
      this.priceRangeMax,
      offer => offer.priceEur
    );

    this.filteredOffers = this.sortOffers(filtered);
  }

  onOfferFiltersChange(): void {
    this.applyOfferFilters();
  }

  onSort(column: typeof this.sortableColumns[number]): void {
    if (this.sortColumn === column) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortColumn = column;
      this.sortDirection = 'asc';
    }
    this.applyOfferFilters();
  }

  sortIndicator(column: typeof this.sortableColumns[number]): string {
    return this.sortColumn === column ? (this.sortDirection === 'asc' ? '↑' : '↓') : '';
  }

  sortAriaValue(column: typeof this.sortableColumns[number]): string | null {
    return this.sortColumn === column
      ? (this.sortDirection === 'asc' ? 'ascending' : 'descending')
      : null;
  }

  private sortOffers(offers: FlatOffer[]): FlatOffer[] {
    if (!this.sortColumn) {
      return offers;
    }

    const column = this.sortColumn;
    const direction = this.sortDirection === 'asc' ? 1 : -1;
    return [...offers].sort((left, right) => {
      const leftValue = left[column];
      const rightValue = right[column];
      if (leftValue == null) return rightValue == null ? 0 : 1;
      if (rightValue == null) return -1;
      const comparison = typeof leftValue === 'number' && typeof rightValue === 'number'
        ? leftValue - rightValue
        : String(leftValue).localeCompare(String(rightValue), undefined, { numeric: true, sensitivity: 'base' });
      return comparison * direction;
    });
  }
}
