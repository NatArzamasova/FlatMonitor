import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { filter, finalize } from 'rxjs/operators';
import { FlatsService } from '../../../../core/services/flats.service';
import { FilterPresetsService } from '../../../../core/services/filter-presets.service';
import { FilterPreset } from '../../../../core/models/filter-preset.model';
import { FlatHistory } from '../../../../core/models/flat-history.model';
import { Aggregatable, mergeWeightedAverage, roundPriceToEuro } from '../../../../core/utils/aggregation.utils';
import { normalizeRooms } from '../../../../core/utils/rooms.utils';
import { filterByDistricts } from '../../../../core/utils/filter.utils';
import { distinctSorted } from '../../../../core/utils/collection.utils';
import { describeLoadError } from '../../../../core/utils/api-response.utils';
import { HistoryChartComponent } from './history-chart/history-chart.component';
import { HistoryPoint } from './history-chart/history-chart.config';
import { LoadingSpinnerComponent } from '../../../../shared/loading-spinner/loading-spinner.component';
import { DismissibleAlertComponent } from '../../../../shared/dismissible-alert/dismissible-alert.component';
import { MultiSelectFilterComponent } from '../../../../shared/multi-select-filter/multi-select-filter.component';
import { FilterPresetBarComponent } from '../../../../shared/filter-preset-bar/filter-preset-bar.component';

@Component({
  selector: 'app-history-tab',
  standalone: true,
  imports: [
    CurrencyPipe,
    FormsModule,
    HistoryChartComponent,
    LoadingSpinnerComponent,
    DismissibleAlertComponent,
    MultiSelectFilterComponent,
    FilterPresetBarComponent
  ],
  templateUrl: './history-tab.component.html',
})
export class HistoryTabComponent implements OnInit {
  private readonly flatsService = inject(FlatsService);
  private readonly filterPresets = inject(FilterPresetsService);
  private readonly destroyRef = inject(DestroyRef);

  historyData: FlatHistory[] = [];
  loading = false;
  error: string | null = null;
  groupBy: 'district' | 'rooms' = 'district';
  /** Applied before grouping, so it also narrows the room-number trends. Empty = all districts. */
  selectedDistricts: string[] = [];
  availableDistricts: string[] = [];
  chartData: HistoryPoint[] = [];
  // Derived from chartData in updateChartData(), so the table is not recomputed on every change detection.
  latestAverageDate = '';
  latestAverageData: { label: string; price: number }[] = [];

  get chartTitle(): string {
    return `Price per m² Trend (${this.groupBy === 'district' ? 'by District' : 'by Room Number'})`;
  }

  ngOnInit(): void {
    this.followActivePreset();
    this.loadHistory();
  }

  /** Applies the active preset's districts now and whenever another preset is chosen; `null` keeps the selection. */
  private followActivePreset(): void {
    this.filterPresets.activePreset$
      .pipe(
        filter((preset): preset is FilterPreset => preset !== null),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(preset => {
        this.selectedDistricts = [...preset.districts];
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
      return `${rooms?.toString() || 'Unknown'} rooms`;
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
