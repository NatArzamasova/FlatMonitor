import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe, Location } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Params, Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { FlatsService } from '../../../../core/services/flats.service';
import { FilterService } from '../../../../core/services/filter.service';
import { ScrapeStatusService } from '../../../../core/services/scrape-status.service';
import { FlatAggregation } from '../../../../core/models/flat-aggregation.model';
import { groupWeighted, roundPriceToEuro } from '../../../../core/utils/aggregation.utils';
import { normalizeRooms } from '../../../../core/utils/rooms.utils';
import { describeLoadError } from '../../../../core/utils/api-response.utils';
import { ScrapingStatusComponent } from '../../scraping-status/scraping-status.component';
import { TableSort } from '../../../../core/utils/table-sort';
import { SortableHeaderComponent } from '../../../../shared/sortable-header/sortable-header.component';
import { MultiSelectFilterComponent } from '../../../../shared/multi-select-filter/multi-select-filter.component';
import { LoadingSpinnerComponent } from '../../../../shared/loading-spinner/loading-spinner.component';
import { DismissibleAlertComponent } from '../../../../shared/dismissible-alert/dismissible-alert.component';

type AggregationSortColumn = 'district' | 'roomsCount' | 'count' | 'avgPricePerMeter';

@Component({
  selector: 'app-overview-tab',
  standalone: true,
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    FormsModule,
    ScrapingStatusComponent,
    SortableHeaderComponent,
    MultiSelectFilterComponent,
    LoadingSpinnerComponent,
    DismissibleAlertComponent
  ],
  templateUrl: './overview-tab.component.html',
})
export class OverviewTabComponent implements OnInit {
  private readonly flatsService = inject(FlatsService);
  private readonly filterService = inject(FilterService);
  private readonly scrapeStatus = inject(ScrapeStatusService);
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

  roundPriceToEuro = roundPriceToEuro;

  ngOnInit(): void {
    this.loadAggregatedData();

    this.scrapeStatus.scrapeFinished$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadAggregatedData());
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
    const districts = new Set<string>();
    const rooms = new Set<number>();

    this.rawData.forEach(item => {
      if (item.district) {
        districts.add(item.district);
      }
      const roomsCount = normalizeRooms(item.roomsCount);
      if (roomsCount !== undefined) {
        rooms.add(roomsCount);
      }
    });

    this.availableDistricts = Array.from(districts).sort();
    this.availableRooms = Array.from(rooms).sort((a, b) => a - b);
  }

  applyFiltersAndGrouping(): void {
    let filtered = this.rawData;
    filtered = this.filterService.filterByDistricts(filtered, this.selectedDistricts, item => item.district);
    filtered = this.filterService.filterByRooms(filtered, this.selectedRooms, item => normalizeRooms(item.roomsCount));

    // Backend rows are already one per district + rooms combination.
    this.aggregatedData = this.groupByDistrict && this.groupByRooms ? filtered : this.aggregate(filtered);
    this.applySort();
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
    // A group keeps its first row's postal code / availability; the all-in-one row has none.
    const keepDetails = byDistrict || byRooms;

    const groups = groupWeighted(
      data,
      item => byDistrict ? item.district : byRooms ? item.roomsCount : 'all',
      item => ({
        district: byDistrict ? item.district : undefined,
        roomsCount: byRooms ? item.roomsCount : undefined,
        count: 0,
        avgPricePerMeter: 0,
        postalCode: keepDetails ? item.postalCode : undefined,
        availability: keepDetails ? item.availability : undefined
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
