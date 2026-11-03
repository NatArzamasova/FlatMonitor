import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CurrencyPipe, DecimalPipe, Location } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Params, Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { FlatsService } from '../../../../core/services/flats.service';
import { FilterPresetsService } from '../../../../core/services/filter-presets.service';
import { filterByDistricts, filterByRooms } from '../../../../core/utils/filter.utils';
import { DataReloadService } from '../../../../core/services/data-reload.service';
import { FlatAggregation } from '../../../../core/models/flat-aggregation.model';
import { groupWeighted } from '../../../../core/utils/aggregation.utils';
import { normalizeRooms } from '../../../../core/utils/rooms.utils';
import { roomsLabel } from '../../../../core/utils/filter-chips.utils';
import { formatDecimal } from '../../../../core/utils/locale.utils';
import { distinctSorted } from '../../../../core/utils/collection.utils';
import { describeLoadError } from '../../../../core/utils/api-response.utils';
import { TableSort } from '../../../../core/utils/table-sort';
import { SortableHeaderComponent } from '../../../../shared/sortable-header/sortable-header.component';
import { MultiSelectFilterComponent } from '../../../../shared/multi-select-filter/multi-select-filter.component';
import { LoadingSkeletonComponent } from '../../../../shared/loading-skeleton/loading-skeleton.component';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state.component';
import { StartScrapeButtonComponent } from '../../start-scrape-button/start-scrape-button.component';
import { DismissibleAlertComponent } from '../../../../shared/dismissible-alert/dismissible-alert.component';
import { StatTileComponent } from '../../../../shared/stat-tile/stat-tile.component';
import { OverviewKpis, computeOverviewKpis } from '../../../../core/utils/overview-kpis.utils';
import { FilterToolbarComponent } from '../../../../shared/filter-toolbar/filter-toolbar.component';
import { FilterChip, buildFilterChips } from '../../../../core/utils/filter-chips.utils';
import { FilterPresetBarComponent } from '../../../../shared/filter-preset-bar/filter-preset-bar.component';

type AggregationSortColumn = 'district' | 'roomsCount' | 'count' | 'avgPricePerMeter';

/** Where a Dashboard row's link leads and how it is announced. */
interface OffersLink {
  url: string;
  label: string;
}

@Component({
  selector: 'app-overview-tab',
  standalone: true,
  host: { class: 'fill-column' },
  imports: [
    CurrencyPipe,
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
    StatTileComponent
  ],
  templateUrl: './overview-tab.component.html',
})
export class OverviewTabComponent implements OnInit {
  private readonly flatsService = inject(FlatsService);
  private readonly dataReload = inject(DataReloadService);
  private readonly filterPresets = inject(FilterPresetsService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly destroyRef = inject(DestroyRef);

  readonly sort = new TableSort<FlatAggregation, AggregationSortColumn>();
  private readonly offersLinks = new Map<FlatAggregation, OffersLink>();

  aggregatedData: FlatAggregation[] = [];
  rawData: FlatAggregation[] = [];
  loading = false;
  error: string | null = null;

  selectedDistricts: string[] = [];
  selectedRooms: number[] = [];
  /** The active preset (`null` = none), passed on to Current Offers by the row links. */
  activePresetId: number | null = null;
  groupByDistrict = true;
  groupByRooms = true;

  availableDistricts: string[] = [];
  availableRooms: number[] = [];
  /** The active filters as chips, updated with the table. */
  filterChips: FilterChip[] = [];
  /** Key figures of the filtered rows (independent of the grouping), shown as tiles above the table. */
  kpis: OverviewKpis | null = null;

  ngOnInit(): void {
    this.followActivePreset();
    this.loadAggregatedData();

    this.dataReload.reload$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadAggregatedData());
  }

  /** Applies the active preset's districts now and whenever another preset is chosen; no preset (`null`) shows all districts. */
  private followActivePreset(): void {
    this.filterPresets.activePreset$
      .pipe(
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(preset => {
        this.activePresetId = preset?.id ?? null;
        this.selectedDistricts = [...(preset?.districts ?? [])];
        this.updateDistrictOptions();
        this.applyFiltersAndGrouping();
      });
  }

  loadAggregatedData(): void {
    this.loading = true;
    this.error = null;
    this.flatsService.getAggregated()
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: (data) => {
          this.rawData = data;
          this.updateAvailableFilters();
          this.applyFiltersAndGrouping();
        },
        error: (err) => {
          this.error = describeLoadError(err, 'Failed to load data', 'Error loading data');
        }
      });
  }

  private updateAvailableFilters(): void {
    this.updateDistrictOptions();
    this.availableRooms = distinctSorted(this.rawData.map(item => normalizeRooms(item.roomsCount)), (a, b) => a - b);
  }

  /** Districts from the data plus selected ones without data, so a preset's districts can always be deselected. */
  private updateDistrictOptions(): void {
    this.availableDistricts = distinctSorted([...this.rawData.map(item => item.district), ...this.selectedDistricts]);
  }

  applyFiltersAndGrouping(): void {
    let filtered = this.rawData;
    filtered = filterByDistricts(filtered, this.selectedDistricts, item => item.district);
    filtered = filterByRooms(filtered, this.selectedRooms, item => normalizeRooms(item.roomsCount));
    this.filterChips = buildFilterChips({ districts: this.selectedDistricts, rooms: this.selectedRooms });
    this.kpis = computeOverviewKpis(filtered);

    // Backend rows are already one per district + rooms combination.
    this.aggregatedData = this.groupByDistrict && this.groupByRooms ? filtered : this.aggregate(filtered);
    this.applySort();
  }

  removeFilterChip(chip: FilterChip): void {
    if (chip.kind === 'district') {
      this.selectedDistricts = this.selectedDistricts.filter(district => district !== chip.value);
    } else if (chip.kind === 'rooms') {
      this.selectedRooms = this.selectedRooms.filter(rooms => rooms !== chip.value);
    }
    this.applyFiltersAndGrouping();
  }

  /** Clears every filter and the preset (keeping a preset whose districts were all removed would only show it as modified). */
  resetFilters(): void {
    this.selectedDistricts = [];
    this.selectedRooms = [];
    this.filterPresets.selectPreset(null);
    this.applyFiltersAndGrouping();
  }

  onSort(column: AggregationSortColumn): void {
    this.sort.toggle(column);
    this.applySort();
  }

  private applySort(): void {
    this.aggregatedData = this.sort.apply(this.aggregatedData);
    this.offersLinks.clear();
  }

  /** Merges rows by the selected dimension (or into one row when none is selected). */
  private aggregate(data: FlatAggregation[]): FlatAggregation[] {
    const byDistrict = this.groupByDistrict;
    const byRooms = this.groupByRooms;

    const groups = groupWeighted(
      data,
      item => byDistrict ? item.district : byRooms ? item.roomsCount : 'all',
      item => ({
        district: byDistrict ? item.district : undefined,
        roomsCount: byRooms ? item.roomsCount : undefined,
        count: 0,
        avgPricePerMeter: 0
      })
    );
    const rows = Array.from(groups.values());

    if (byDistrict) {
      return rows.sort((a, b) => (a.district || '').localeCompare(b.district || ''));
    }
    if (byRooms) {
      return rows.sort((a, b) => (a.roomsCount ?? -1) - (b.roomsCount ?? -1));
    }
    return rows;
  }

  /**
   * The link of a row to its matching current offers: URL and accessible name. Computed once per row after each
   * filter, grouping or sort change (`applySort` clears the cache), not on every change detection.
   */
  offersLink(item: FlatAggregation): OffersLink {
    let link = this.offersLinks.get(item);
    if (!link) {
      link = { url: this.offersUrl(item), label: this.offersLinkLabel(item) };
      this.offersLinks.set(item, link);
    }
    return link;
  }

  /** A click anywhere on a row opens its offers; a click on the row's own link is left to the link. */
  onRowClick(item: FlatAggregation, event: MouseEvent): void {
    if ((event.target as Element | null)?.closest('a')) {
      return;
    }
    this.openOffersForRow(item);
  }

  openOffersForRow(item: FlatAggregation): void {
    window.open(this.offersUrl(item), '_blank', 'noopener,noreferrer');
  }

  /** `Open offers in Südstadt with 3 rooms`, `Open offers in 4 districts`, `Open all current offers`, … */
  private offersLinkLabel(item: FlatAggregation): string {
    const { districts, rooms } = this.offersFilters(item);
    if (districts.length === 0 && rooms.length === 0) {
      return 'Open all current offers';
    }
    const where = districts.length === 0 ? '' : ` in ${districts.length === 1 ? districts[0] : `${districts.length} districts`}`;
    const withRooms = rooms.length === 0 ? ''
      : ` with ${rooms.length === 1 ? roomsLabel(rooms[0]) : `${rooms.map(formatDecimal).join(', ')} rooms`}`;
    return `Open offers${where}${withRooms}`;
  }

  /**
   * `/offers?district=…&rooms=…&preset=<id>|none`. The preset is passed because the offers open in a new browser
   * tab, whose app would otherwise apply the default preset instead of the one chosen here.
   */
  private offersUrl(item: FlatAggregation): string {
    const { districts, rooms, presetId } = this.offersFilters(item);
    const queryParams: Params = {};
    if (districts.length > 0) {
      queryParams['district'] = districts;
    }
    if (rooms.length > 0) {
      queryParams['rooms'] = rooms.map(String);
    }
    queryParams['preset'] = presetId ?? 'none';
    const offersUrl = this.router.serializeUrl(this.router.createUrlTree(['/offers'], { queryParams }));
    return this.location.prepareExternalUrl(offersUrl);
  }

  /**
   * The filters of the offers behind a row: the row's own district and rooms where the grouping shows them,
   * otherwise this tab's filters (the row's figures were computed over them). A row's single district is not the
   * preset, so then no preset is passed on.
   */
  private offersFilters(item: FlatAggregation): { districts: string[]; rooms: number[]; presetId: number | null } {
    const rowDistrict = this.groupByDistrict && item.district ? item.district : undefined;
    const rowRooms = this.groupByRooms ? normalizeRooms(item.roomsCount) : undefined;
    return {
      districts: rowDistrict !== undefined ? [rowDistrict] : [...this.selectedDistricts],
      rooms: rowRooms !== undefined ? [rowRooms] : [...this.selectedRooms],
      presetId: rowDistrict !== undefined ? null : this.activePresetId
    };
  }
}
