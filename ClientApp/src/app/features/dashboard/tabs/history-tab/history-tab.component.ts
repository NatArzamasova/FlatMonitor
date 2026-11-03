import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { FlatsService } from '../../../../core/services/flats.service';
import { DataReloadService } from '../../../../core/services/data-reload.service';
import { ThemeService } from '../../../../core/services/theme.service';
import { FilterPresetsService } from '../../../../core/services/filter-presets.service';
import { FlatHistory } from '../../../../core/models/flat-history.model';
import { Aggregatable, mergeWeightedAverage, roundPriceToEuro } from '../../../../core/utils/aggregation.utils';
import { normalizeRooms } from '../../../../core/utils/rooms.utils';
import { formatDecimal } from '../../../../core/utils/locale.utils';
import { filterByDistricts } from '../../../../core/utils/filter.utils';
import { distinctSorted } from '../../../../core/utils/collection.utils';
import { describeLoadError } from '../../../../core/utils/api-response.utils';
import { HistoryChartComponent } from './history-chart/history-chart.component';
import { HistoryPoint, seriesColorsByLabel } from './history-chart/history-chart.config';
import { LoadingSkeletonComponent } from '../../../../shared/loading-skeleton/loading-skeleton.component';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state.component';
import { StartScrapeButtonComponent } from '../../start-scrape-button/start-scrape-button.component';
import { DismissibleAlertComponent } from '../../../../shared/dismissible-alert/dismissible-alert.component';
import { MultiSelectFilterComponent } from '../../../../shared/multi-select-filter/multi-select-filter.component';
import { FilterToolbarComponent } from '../../../../shared/filter-toolbar/filter-toolbar.component';
import { FilterChip, buildFilterChips } from '../../../../core/utils/filter-chips.utils';
import { FilterPresetBarComponent } from '../../../../shared/filter-preset-bar/filter-preset-bar.component';

@Component({
  selector: 'app-history-tab',
  standalone: true,
  host: { class: 'fill-column' },
  imports: [
    CurrencyPipe,
    FormsModule,
    HistoryChartComponent,
    LoadingSkeletonComponent,
    EmptyStateComponent,
    StartScrapeButtonComponent,
    DismissibleAlertComponent,
    MultiSelectFilterComponent,
    FilterPresetBarComponent,
    FilterToolbarComponent
  ],
  templateUrl: './history-tab.component.html',
})
export class HistoryTabComponent implements OnInit {
  private readonly flatsService = inject(FlatsService);
  private readonly dataReload = inject(DataReloadService);
  private readonly filterPresets = inject(FilterPresetsService);
  private readonly themeService = inject(ThemeService);
  private readonly destroyRef = inject(DestroyRef);

  historyData: FlatHistory[] = [];
  loading = false;
  error: string | null = null;
  groupBy: 'district' | 'rooms' = 'district';
  /** Applied before grouping, so it also narrows the room-number trends. Empty = all districts. */
  selectedDistricts: string[] = [];
  availableDistricts: string[] = [];
  /** The active filters as chips, updated with the chart. */
  filterChips: FilterChip[] = [];
  chartData: HistoryPoint[] = [];
  // Derived from chartData in updateChartData(), so the table is not recomputed on every change detection.
  latestAverageDate = '';
  latestAverageData: { label: string; price: number }[] = [];
  /** The chart's line colour per label, for the dots in the "Latest averages" table. */
  seriesColors = new Map<string, string>();
  /** The dark theme has a lighter line palette, so the dots follow the theme like the chart. */
  private darkTheme = false;

  get chartTitle(): string {
    return `Price per m² Trend (${this.groupBy === 'district' ? 'by District' : 'by Rooms'})`;
  }

  ngOnInit(): void {
    this.followActivePreset();
    this.followTheme();
    this.loadHistory();

    // The header's refresh button and finished scrapes.
    this.dataReload.reload$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadHistory());
  }

  private followTheme(): void {
    this.themeService.theme$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(theme => {
        this.darkTheme = theme === 'dark';
        this.seriesColors = seriesColorsByLabel(this.chartData, this.darkTheme);
      });
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
        this.updateChartData();
      });
  }

  loadHistory(): void {
    this.loading = true;
    this.error = null;
    this.flatsService.getHistory()
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: (history) => {
          this.historyData = history;
          this.updateDistrictOptions();
          this.updateChartData();
        },
        error: (err) => {
          this.error = describeLoadError(err, 'Failed to load history', 'Error loading history');
        }
      });
  }

  removeFilterChip(chip: FilterChip): void {
    if (chip.kind === 'district') {
      this.selectedDistricts = this.selectedDistricts.filter(district => district !== chip.value);
    }
    this.updateChartData();
  }

  /** Clears every filter and the preset (keeping a preset whose districts were all removed would only show it as modified). */
  resetFilters(): void {
    this.selectedDistricts = [];
    this.filterPresets.selectPreset(null);
    this.updateChartData();
  }

  onGroupByChange(): void {
    this.updateChartData();
  }

  onDistrictFilterChange(): void {
    this.updateChartData();
  }

  /** Districts from the data plus selected ones without data, so a preset's districts can always be deselected. */
  private updateDistrictOptions(): void {
    this.availableDistricts = distinctSorted([...this.historyData.map(item => item.district), ...this.selectedDistricts]);
  }

  private updateChartData(): void {
    this.filterChips = buildFilterChips({ districts: this.selectedDistricts });
    if (this.groupBy === 'district') {
      this.aggregateByDistrict();
    } else {
      this.aggregateByRooms();
    }
    this.updateLatestAverages();
  }

  private updateLatestAverages(): void {
    const latestDate = this.chartData.reduce(
      (latest, item) => item.date > latest ? item.date : latest,
      ''
    );
    this.latestAverageDate = latestDate;
    this.seriesColors = seriesColorsByLabel(this.chartData, this.darkTheme);
    this.latestAverageData = this.chartData
      .filter(item => item.date === latestDate)
      .map(({ label, price }) => ({ label, price }))
      .sort((left, right) => right.price - left.price || left.label.localeCompare(right.label));
  }

  private aggregateByDistrict(): void {
    this.aggregateByLabel(item => item.district || 'Unknown');
  }

  private aggregateByRooms(): void {
    this.aggregateByLabel(item => {
      const rooms = item.roomsCount ? normalizeRooms(item.roomsCount) : null;
      return `${rooms ? formatDecimal(rooms) : 'Unknown'} rooms`;
    });
  }

  private aggregateByLabel(getLabel: (item: FlatHistory) => string): void {
    const grouped = new Map<string, Map<string, Aggregatable>>();

    filterByDistricts(this.historyData, this.selectedDistricts, item => item.district ?? undefined).forEach(item => {
      if (!item.scrapingDate) {
        return;
      }

      if (item.count <= 0) {
        return;
      }

      const label = getLabel(item);
      const date = item.scrapingDate;
      if (!grouped.has(label)) {
        grouped.set(label, new Map());
      }

      const dateMap = grouped.get(label)!;
      dateMap.set(date, mergeWeightedAverage(dateMap.get(date) ?? { count: 0, avgPricePerMeter: 0 }, item));
    });

    this.chartData = [];
    grouped.forEach((dateMap, label) => {
      dateMap.forEach((aggregation, date) => {
        this.chartData.push({
          date,
          price: roundPriceToEuro(aggregation.avgPricePerMeter),
          label
        });
      });
    });
  }

}
