import { DestroyRef, Directive, OnInit, inject } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { Observable } from 'rxjs';
import { finalize, skip } from 'rxjs/operators';
import { DataReloadService } from '../../../../core/services/data-reload.service';
import { FilterPresetsService } from '../../../../core/services/filter-presets.service';
import { filterByDistricts, filterByMaximum, filterByPriceRange, filterByRooms, filterBySelection } from '../../../../core/utils/filter.utils';
import { FlatOffer } from '../../../../core/models/flat-offer.model';
import { isHttpUrl } from '../../../../core/utils/url.utils';
import { offerPreviewUrl } from '../../../../core/utils/preview.utils';
import { sourceBadge } from '../../../../core/utils/source.utils';
import { ENERGY_CLASSES, energyClassBadge, normalizeEnergyClass } from '../../../../core/utils/energy-class.utils';
import { normalizeRooms } from '../../../../core/utils/rooms.utils';
import { distinctSorted } from '../../../../core/utils/collection.utils';
import { describeLoadError } from '../../../../core/utils/api-response.utils';
import { TableSort } from '../../../../core/utils/table-sort';
import { FilterChip, buildFilterChips } from '../../../../core/utils/filter-chips.utils';
import { LAST_SEEN_PERIODS } from '../../../../core/utils/archive.utils';
import { SortableHeaderComponent } from '../../../../shared/sortable-header/sortable-header.component';
import { MultiSelectFilterComponent } from '../../../../shared/multi-select-filter/multi-select-filter.component';
import { LoadingSkeletonComponent } from '../../../../shared/loading-skeleton/loading-skeleton.component';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state.component';
import { DismissibleAlertComponent } from '../../../../shared/dismissible-alert/dismissible-alert.component';
import { FilterToolbarComponent } from '../../../../shared/filter-toolbar/filter-toolbar.component';
import { FilterPresetBarComponent } from '../../../../shared/filter-preset-bar/filter-preset-bar.component';
import { StartScrapeButtonComponent } from '../../start-scrape-button/start-scrape-button.component';
import { OfferCardListComponent } from './offer-card-list/offer-card-list.component';
import { OfferPreviewComponent } from './offer-preview/offer-preview.component';
import { OfferRow, SortColumnOption, openOfferLabel, openOfferOnClick } from './offer-row';

/** The imports of the shared offers template (`offers-tab.component.html`), for every tab that uses it. */
export const OFFER_LIST_IMPORTS = [
  CurrencyPipe,
  DatePipe,
  DecimalPipe,
  FormsModule,
  SortableHeaderComponent,
  MultiSelectFilterComponent,
  LoadingSkeletonComponent,
  EmptyStateComponent,
  StartScrapeButtonComponent,
  DismissibleAlertComponent,
  FilterPresetBarComponent,
  FilterToolbarComponent,
  OfferCardListComponent,
  OfferPreviewComponent
];

/** The styles of the offers table, shared by the tabs that show it. */
export const OFFER_LIST_STYLES = `
  /* The 30 px thumbnail with little padding keeps the rows about as high as rows of text. */
  .data-table tbody td {
    vertical-align: middle;
  }
  .offer-photo-col,
  .offer-photo {
    width: 1%;
  }
  .offer-photo {
    padding-top: 0.125rem;
    padding-bottom: 0.125rem;
  }
`;

/** The Archive tab's "Last seen" filter, bound in the shared template. */
export interface LastSeenFilter {
  /** Last seen at most this many days ago; `null` = all. */
  days: number | null;
}

/**
 * A tab listing offers as a table (cards on small screens) with the district, preset, rooms, energy class, price and
 * Provision filters, sorting, and the title photo: Current Offers and Archive. Both share `offers-tab.component.html`,
 * whose `archived` flag shows the archive's extras. A subclass says where the offers come from and can add filters
 * through the `*Extra*` hooks.
 */
@Directive()
export abstract class OfferListTab<T extends FlatOffer, K extends keyof T & string> implements OnInit {
  private readonly dataReload = inject(DataReloadService);
  private readonly route = inject(ActivatedRoute);
  private readonly filterPresets = inject(FilterPresetsService);
  private readonly destroyRef = inject(DestroyRef);

  abstract readonly sort: TableSort<T, K>;
  /** The sortable columns in table order, for the cards' sort control. */
  abstract readonly sortColumns: readonly SortColumnOption<K>[];
  /** Shows the archive's columns, filter and texts in the shared template. */
  abstract readonly archived: boolean;
  /** The archive's "Last seen" filter; `null` in a tab without it. */
  abstract readonly lastSeen: LastSeenFilter | null;

  /** The offers the tab loaded, before filtering. */
  currentOffers: T[] = [];
  filteredOffers: T[] = [];
  /** `filteredOffers` with the per-row display state, computed once per filter/sort change. */
  offerRows: OfferRow<T>[] = [];
  loading = false;
  error: string | null = null;

  selectedDistricts: string[] = [];
  selectedRooms: number[] = [];
  /** Normalized classes (`A+` … `H`). */
  selectedEnergyClasses: string[] = [];
  priceRangeMax: number | null = null;
  /** Highest Provision in percent; `0` = commission-free only, `null` = no limit. */
  commissionMax: number | null = null;
  availableDistricts: string[] = [];
  availableRooms: number[] = [];
  /** The classes of the scale that some offer has, best first. */
  availableEnergyClasses: string[] = [];
  /** The active filters as chips, updated with the table. */
  filterChips: FilterChip[] = [];

  readonly openOffer = openOfferOnClick;
  readonly openOfferLabel = openOfferLabel;
  /** The choices of the `lastSeen` filter. */
  readonly lastSeenPeriods = LAST_SEEN_PERIODS;

  /** Loads the offers of this tab. */
  protected abstract fetchOffers(): Observable<T[]>;

  /** The texts of a failed load: the fallback message and the prefix of an HTTP error (see `describeLoadError`). */
  protected abstract readonly loadErrorTexts: { fallback: string; httpPrefix: string };

  /** A subclass's own filters, applied after the common ones. */
  protected applyExtraFilters(offers: T[]): T[] {
    return offers;
  }

  /** Chips for a subclass's own filters, after the common ones. */
  protected extraFilterChips(): FilterChip[] {
    return [];
  }

  /** Clears the subclass filter a chip of `extraFilterChips()` stands for. */
  protected removeExtraFilterChip(_chip: FilterChip): void {
  }

  /** Clears a subclass's own filters, for "Reset filters". */
  protected resetExtraFilters(): void {
  }

  /** The row of an offer; a subclass can add to it or show another photo. */
  protected toRow(offer: T): OfferRow<T> {
    return {
      offer,
      discountPercentage: this.getDiscountPercentage(offer),
      hasLink: isHttpUrl(offer.url),
      source: sourceBadge(offer.source),
      energyClass: energyClassBadge(offer.energyEfficiencyClass),
      previewUrl: offerPreviewUrl(offer)
    };
  }

  ngOnInit(): void {
    const districtsFromUrl = this.loadInitialFiltersFromUrl();
    this.followActivePreset(districtsFromUrl);
    this.loadCurrentOffers();

    // The header's refresh button and finished scrapes.
    this.dataReload.reload$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadCurrentOffers());
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

  /**
   * Reads the filters a link brought along (`?district=…&rooms=…&preset=<id>|none`, see the Dashboard's row links).
   * Such a tab is usually a new browser tab, whose app does not know the preset chosen where the link was clicked,
   * so it would fall back to the default preset. With districts or a `preset` in the URL, the URL's preset
   * (`none` when it names none) becomes the active one instead, and the URL's districts are the selection.
   * Returns whether the URL set the districts.
   */
  private loadInitialFiltersFromUrl(): boolean {
    const params = this.route.snapshot.queryParamMap;
    this.selectedDistricts = params.getAll('district');
    this.selectedRooms = params.getAll('rooms')
      .map(value => normalizeRooms(Number(value)))
      .filter((value): value is number => value !== undefined);

    const presetParam = params.get('preset');
    const districtsFromUrl = this.selectedDistricts.length > 0 || presetParam !== null;
    if (districtsFromUrl) {
      this.filterPresets.selectPreset(parsePresetParam(presetParam));
    }
    return districtsFromUrl;
  }

  /**
   * Applies the active preset's districts now and whenever another preset is chosen. Districts from the URL win
   * over the preset that is active when the tab opens (its first emission is skipped); no preset (`null`) shows all
   * districts.
   */
  private followActivePreset(districtsFromUrl: boolean): void {
    this.filterPresets.activePreset$
      .pipe(
        skip(districtsFromUrl ? 1 : 0),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(preset => {
        this.selectedDistricts = [...(preset?.districts ?? [])];
        this.updateDistrictOptions();
        this.applyFilters();
      });
  }

  loadCurrentOffers(): void {
    this.loading = true;
    this.error = null;
    this.fetchOffers()
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: (offers) => {
          this.currentOffers = offers;
          this.updateAvailableFilters();
          this.applyFilters();
        },
        error: (err) => {
          this.error = describeLoadError(err, this.loadErrorTexts.fallback, this.loadErrorTexts.httpPrefix);
        }
      });
  }

  private updateAvailableFilters(): void {
    this.updateDistrictOptions();
    this.availableRooms = distinctSorted(this.currentOffers.map(offer => normalizeRooms(offer.roomsCount)), (a, b) => a - b);
    const classes = new Set(this.currentOffers.map(offer => normalizeEnergyClass(offer.energyEfficiencyClass)));
    // Selected classes stay choosable after a reload without them, so they can still be deselected
    this.availableEnergyClasses = ENERGY_CLASSES.filter(energyClass =>
      classes.has(energyClass) || this.selectedEnergyClasses.includes(energyClass));
  }

  /** Districts from the data plus selected ones without offers, so a preset's districts can always be deselected. */
  private updateDistrictOptions(): void {
    this.availableDistricts = distinctSorted([...this.currentOffers.map(offer => offer.district), ...this.selectedDistricts]);
  }

  applyFilters(): void {
    let filtered = this.currentOffers;
    filtered = filterByDistricts(filtered, this.selectedDistricts, offer => offer.district);
    filtered = filterByRooms(
      filtered,
      this.selectedRooms,
      offer => normalizeRooms(offer.roomsCount)
    );
    filtered = filterBySelection(filtered, this.selectedEnergyClasses, offer => normalizeEnergyClass(offer.energyEfficiencyClass));
    filtered = filterByPriceRange(filtered, null, this.priceRangeMax, offer => offer.priceEur);
    filtered = filterByMaximum(filtered, this.commissionMax, offer => offer.commissionPercent);
    filtered = this.applyExtraFilters(filtered);

    this.filteredOffers = this.sort.apply(filtered);
    this.filterChips = [
      ...buildFilterChips({
        districts: this.selectedDistricts,
        rooms: this.selectedRooms,
        energyClasses: this.selectedEnergyClasses,
        priceMax: this.priceRangeMax,
        commissionMax: this.commissionMax
      }),
      ...this.extraFilterChips()
    ];
    this.offerRows = this.filteredOffers.map(offer => this.toRow(offer));
  }

  removeFilterChip(chip: FilterChip): void {
    switch (chip.kind) {
      case 'district':
        this.selectedDistricts = this.selectedDistricts.filter(district => district !== chip.value);
        break;
      case 'rooms':
        this.selectedRooms = this.selectedRooms.filter(rooms => rooms !== chip.value);
        break;
      case 'energyClass':
        this.selectedEnergyClasses = this.selectedEnergyClasses.filter(energyClass => energyClass !== chip.value);
        break;
      case 'priceMax':
        this.priceRangeMax = null;
        break;
      case 'commissionMax':
        this.commissionMax = null;
        break;
      default:
        this.removeExtraFilterChip(chip);
    }
    this.applyFilters();
  }

  /** Clears every filter and the preset (keeping a preset whose districts were all removed would only show it as modified). */
  resetFilters(): void {
    this.selectedDistricts = [];
    this.selectedRooms = [];
    this.selectedEnergyClasses = [];
    this.priceRangeMax = null;
    this.commissionMax = null;
    this.resetExtraFilters();
    this.filterPresets.selectPreset(null);
    this.applyFilters();
  }

  onSort(column: K): void {
    this.sort.toggle(column);
    this.applyFilters();
  }
}

/** `preset=12` → 12; `none`, a missing or an invalid value → `null` (no preset). */
function parsePresetParam(value: string | null): number | null {
  const id = Number(value);
  return value !== null && Number.isInteger(id) && id > 0 ? id : null;
}
