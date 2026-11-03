import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe, Location } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Params, Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { FlatsService } from '../../../../core/services/flats.service';
import { FilterPresetsService } from '../../../../core/services/filter-presets.service';
import { filterByDistricts, filterByRooms } from '../../../../core/utils/filter.utils';
import { ScrapeStatusService } from '../../../../core/services/scrape-status.service';
import { FlatAggregation } from '../../../../core/models/flat-aggregation.model';
import { groupWeighted } from '../../../../core/utils/aggregation.utils';
import { normalizeRooms } from '../../../../core/utils/rooms.utils';
import { distinctSorted } from '../../../../core/utils/collection.utils';
import { describeLoadError } from '../../../../core/utils/api-response.utils';
import { ScrapingStatusComponent } from '../../scraping-status/scraping-status.component';
import { TableSort } from '../../../../core/utils/table-sort';
import { SortableHeaderComponent } from '../../../../shared/sortable-header/sortable-header.component';
import { MultiSelectFilterComponent } from '../../../../shared/multi-select-filter/multi-select-filter.component';
import { LoadingSpinnerComponent } from '../../../../shared/loading-spinner/loading-spinner.component';
import { DismissibleAlertComponent } from '../../../../shared/dismissible-alert/dismissible-alert.component';
import { FilterToolbarComponent } from '../../../../shared/filter-toolbar/filter-toolbar.component';
import { FilterChip, buildFilterChips } from '../../../../core/utils/filter-chips.utils';
import { FilterPresetBarComponent } from '../../../../shared/filter-preset-bar/filter-preset-bar.component';

type AggregationSortColumn = 'district' | 'roomsCount' | 'count' | 'avgPricePerMeter';

@Component({
  selector: 'app-overview-tab',
  standalone: true,
  host: { class: 'fill-column' },
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    FormsModule,
    ScrapingStatusComponent,
    SortableHeaderComponent,
    MultiSelectFilterComponent,
    LoadingSpinnerComponent,
    DismissibleAlertComponent,
    FilterPresetBarComponent,
    FilterToolbarComponent
  ],
  templateUrl: './overview-tab.component.html',
})
export class OverviewTabComponent implements OnInit {
  private readonly flatsService = inject(FlatsService);
  private readonly scrapeStatus = inject(ScrapeStatusService);
  private readonly filterPresets = inject(FilterPresetsService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly destroyRef = inject(DestroyRef);

  readonly sort = new TableSort<FlatAggregation, AggregationSortColumn>();

  aggregatedData: FlatAggregation[] = [];
  rawData: FlatAggregation[] = [];
  loading = false;
  error: string | null = null;
  lastUpdated: Date | null = null;

  selectedDistricts: string[] = [];
  selectedRooms: number[] = [];
  groupByDistrict = true;
  groupByRooms = true;

  availableDistricts: string[] = [];
  availableRooms: number[] = [];
  /** The active filters as chips, updated with the table. */
  filterChips: FilterChip[] = [];

  ngOnInit(): void {
    this.followActivePreset();
    this.loadAggregatedData();

    this.scrapeStatus.scrapeFinished$
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
          this.lastUpdated = new Date();
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

  refresh(): void {
    this.loadAggregatedData();
  }

  openOffersForRow(item: FlatAggregation): void {
    const queryParams: Params = {};

    if (this.groupByDistrict && item.district) {
      queryParams['district'] = item.district;
    }

    const roomsCount = normalizeRooms(item.roomsCount);
    if (this.groupByRooms && roomsCount !== undefined) {
      queryParams['rooms'] = String(roomsCount);
    }

    const offersUrl = this.router.serializeUrl(this.router.createUrlTree(['/offers'], { queryParams }));
    window.open(this.location.prepareExternalUrl(offersUrl), '_blank', 'noopener,noreferrer');
  }
}
