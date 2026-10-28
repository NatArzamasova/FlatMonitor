import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { FlatsService } from '../../../../core/services/flats.service';
import { FilterService } from '../../../../core/services/filter.service';
import { FlatOffer } from '../../../../core/models/flat-offer.model';
import { isHttpUrl } from '../../../../core/utils/url.utils';
import { normalizeRooms } from '../../../../core/utils/rooms.utils';
import { describeLoadError } from '../../../../core/utils/api-response.utils';
import { TableSort } from '../../../../core/utils/table-sort';
import { SortableHeaderComponent } from '../../../../shared/sortable-header/sortable-header.component';

type OfferSortColumn =
  'district' | 'roomsCount' | 'priceEur' | 'originalPriceEur' | 'livingAreaSqm' | 'pricePerMeter' | 'source';

@Component({
  selector: 'app-offers-tab',
  standalone: true,
  imports: [CommonModule, FormsModule, SortableHeaderComponent],
  templateUrl: './offers-tab.component.html',
})
export class OffersTabComponent implements OnInit {
  readonly sort = new TableSort<FlatOffer, OfferSortColumn>();

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
    private filterService: FilterService,
    private route: ActivatedRoute
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
    const params = this.route.snapshot.queryParamMap;
    this.selectedOfferDistricts = params.getAll('district');
    this.selectedOfferRooms = params.getAll('rooms')
      .map(value => normalizeRooms(Number(value)))
      .filter((value): value is number => value !== undefined);
  }

  loadCurrentOffers(): void {
    this.loadingOffers = true;
    this.error = null;
    this.flatsService.getCurrentOffers()
      .pipe(finalize(() => this.loadingOffers = false))
      .subscribe({
        next: (offers) => {
          this.currentOffers = offers;
          this.updateAvailableOfferFilters();
          this.applyOfferFilters();
        },
        error: (err) => {
          this.error = describeLoadError(err, 'Failed to load offers', 'Error loading offers');
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
      const roomsCount = normalizeRooms(offer.roomsCount);
      if (roomsCount !== undefined) {
        rooms.add(roomsCount);
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
      offer => normalizeRooms(offer.roomsCount)
    );
    filtered = this.filterService.filterByPriceRange(
      filtered,
      this.priceRangeMin,
      this.priceRangeMax,
      offer => offer.priceEur
    );

    this.filteredOffers = this.sort.apply(filtered);
  }

  onOfferFiltersChange(): void {
    this.applyOfferFilters();
  }

  onSort(column: OfferSortColumn): void {
    this.sort.toggle(column);
    this.applyOfferFilters();
  }
}
