import { Component, OnInit, inject } from '@angular/core';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
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
import { MultiSelectFilterComponent } from '../../../../shared/multi-select-filter/multi-select-filter.component';
import { LoadingSpinnerComponent } from '../../../../shared/loading-spinner/loading-spinner.component';
import { DismissibleAlertComponent } from '../../../../shared/dismissible-alert/dismissible-alert.component';

type OfferSortColumn =
  'district' | 'roomsCount' | 'priceEur' | 'originalPriceEur' | 'livingAreaSqm' | 'pricePerMeter' | 'source';

interface OfferRow {
  offer: FlatOffer;
  discountPercentage: number | null;
  priceChanged: boolean;
  hasLink: boolean;
}

@Component({
  selector: 'app-offers-tab',
  standalone: true,
  imports: [
    CurrencyPipe,
    DecimalPipe,
    FormsModule,
    SortableHeaderComponent,
    MultiSelectFilterComponent,
    LoadingSpinnerComponent,
    DismissibleAlertComponent
  ],
  templateUrl: './offers-tab.component.html',
})
export class OffersTabComponent implements OnInit {
  private readonly flatsService = inject(FlatsService);
  private readonly filterService = inject(FilterService);
  private readonly route = inject(ActivatedRoute);

  readonly sort = new TableSort<FlatOffer, OfferSortColumn>();

  currentOffers: FlatOffer[] = [];
  filteredOffers: FlatOffer[] = [];
  /** `filteredOffers` with the per-row display state, computed once per filter/sort change. */
  offerRows: OfferRow[] = [];
  loadingOffers = false;
  error: string | null = null;

  selectedOfferDistricts: string[] = [];
  selectedOfferRooms: number[] = [];
  priceRangeMin: number | null = null;
  priceRangeMax: number | null = null;
  availableOfferDistricts: string[] = [];
  availableOfferRooms: number[] = [];

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
    this.offerRows = this.filteredOffers.map(offer => ({
      offer,
      discountPercentage: this.getDiscountPercentage(offer),
      priceChanged: this.hasChangedPrice(offer),
      hasLink: isHttpUrl(offer.url)
    }));
  }

  onOfferFiltersChange(): void {
    this.applyOfferFilters();
  }

  onSort(column: OfferSortColumn): void {
    this.sort.toggle(column);
    this.applyOfferFilters();
  }
}
