import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Params, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { FlatsService } from '../../../../core/services/flats.service';
import { FilterService } from '../../../../core/services/filter.service';
import { ScrapeStatusService } from '../../../../core/services/scrape-status.service';
import { FlatAggregation } from '../../../../core/models/flat-aggregation.model';
import { mergeWeightedAverage, roundPriceToEuro } from '../../../../core/utils/aggregation.utils';
import { normalizeRooms } from '../../../../core/utils/rooms.utils';
import { describeLoadError } from '../../../../core/utils/api-response.utils';
import { ScrapingStatusComponent } from '../../scraping-status/scraping-status.component';
import { TableSort } from '../../../../core/utils/table-sort';
import { SortableHeaderComponent } from '../../../../shared/sortable-header/sortable-header.component';

type AggregationSortColumn = 'district' | 'roomsCount' | 'count' | 'avgPricePerMeter';

@Component({
  selector: 'app-overview-tab',
  standalone: true,
  imports: [CommonModule, FormsModule, ScrapingStatusComponent, SortableHeaderComponent],
  templateUrl: './overview-tab.component.html',
})
export class OverviewTabComponent implements OnInit, OnDestroy {
  readonly sort = new TableSort<FlatAggregation, AggregationSortColumn>();

  aggregatedData: FlatAggregation[] = [];
  rawData: FlatAggregation[] = [];
  loading = false;
  error: string | null = null;
  lastUpdated: Date | null = null;

  selectedDistricts: string[] = [];
  selectedRoomCounts: number[] = [];
  groupByDistrict = true;
  groupByRooms = true;

  availableDistricts: string[] = [];
  availableRooms: (number | null)[] = [];

  private destroy$ = new Subject<void>();

  roundPriceToEuro = roundPriceToEuro;

  constructor(
    private flatsService: FlatsService,
    private filterService: FilterService,
    private scrapeStatus: ScrapeStatusService,
    private router: Router,
    private location: Location
  ) {}

  ngOnInit(): void {
    this.loadAggregatedData();

    this.scrapeStatus.scrapeFinished$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.loadAggregatedData());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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
    const rooms = new Set<number | null>();

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
    this.availableRooms = Array.from(rooms).sort((a, b) => {
      if (a === null) return 1;
      if (b === null) return -1;
      return a - b;
    });
  }

  applyFiltersAndGrouping(): void {
    let filtered = this.rawData;
    filtered = this.filterService.filterByDistricts(filtered, this.selectedDistricts, item => item.district);
    filtered = this.filterService.filterByRooms(filtered, this.selectedRoomCounts, item => normalizeRooms(item.roomsCount));

    if (this.groupByDistrict && this.groupByRooms) {
      this.aggregatedData = filtered;
    } else if (this.groupByDistrict && !this.groupByRooms) {
      this.aggregatedData = this.aggregateByDistrict(filtered);
    } else if (!this.groupByDistrict && this.groupByRooms) {
      this.aggregatedData = this.aggregateByRooms(filtered);
    } else {
      this.aggregatedData = this.aggregateAll(filtered);
    }
    this.applySort();
  }

  onFiltersChange(): void {
    this.applyFiltersAndGrouping();
  }

  onSort(column: AggregationSortColumn): void {
    this.sort.toggle(column);
    this.applySort();
  }

  private applySort(): void {
    this.aggregatedData = this.sort.apply(this.aggregatedData);
  }

  private aggregateByDistrict(data: FlatAggregation[]): FlatAggregation[] {
    const grouped = new Map<string | undefined, FlatAggregation>();

    data.forEach(item => {
      const key = item.district;
      if (!grouped.has(key)) {
        grouped.set(key, {
          district: item.district,
          roomsCount: undefined,
          count: 0,
          avgPricePerMeter: 0,
          postalCode: item.postalCode,
          availability: item.availability
        });
      }

      const current = grouped.get(key)!;
      mergeWeightedAverage(current, item);
    });

    return Array.from(grouped.values()).sort((a, b) => {
      const districtA = a.district || '';
      const districtB = b.district || '';
      return districtA.localeCompare(districtB);
    });
  }

  private aggregateByRooms(data: FlatAggregation[]): FlatAggregation[] {
    const grouped = new Map<number | undefined, FlatAggregation>();

    data.forEach(item => {
      const key = item.roomsCount;
      if (!grouped.has(key)) {
        grouped.set(key, {
          district: undefined,
          roomsCount: item.roomsCount,
          count: 0,
          avgPricePerMeter: 0,
          postalCode: item.postalCode,
          availability: item.availability
        });
      }

      const current = grouped.get(key)!;
      mergeWeightedAverage(current, item);
    });

    return Array.from(grouped.values()).sort((a, b) => {
      const roomsA = a.roomsCount ?? -1;
      const roomsB = b.roomsCount ?? -1;
      return roomsA - roomsB;
    });
  }

  private aggregateAll(data: FlatAggregation[]): FlatAggregation[] {
    if (data.length === 0) {
      return [];
    }

    const aggregated: FlatAggregation = {
      district: undefined,
      roomsCount: undefined,
      count: 0,
      avgPricePerMeter: 0,
      postalCode: undefined,
      availability: undefined
    };

    data.forEach(item => {
      mergeWeightedAverage(aggregated, item);
    });

    return [aggregated];
  }

  onGroupingToggle(): void {
    this.applyFiltersAndGrouping();
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
