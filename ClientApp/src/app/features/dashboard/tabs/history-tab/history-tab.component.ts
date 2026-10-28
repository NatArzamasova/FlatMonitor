import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { FlatsService } from '../../../../core/services/flats.service';
import { FlatHistory } from '../../../../core/models/flat-history.model';
import { Aggregatable, mergeWeightedAverage, roundPriceToEuro } from '../../../../core/utils/aggregation.utils';
import { normalizeRooms } from '../../../../core/utils/rooms.utils';
import { describeLoadError } from '../../../../core/utils/api-response.utils';
import { HistoryChartComponent } from './history-chart/history-chart.component';
import { HistoryPoint } from './history-chart/history-chart.config';
import { LoadingSpinnerComponent } from '../../../../shared/loading-spinner/loading-spinner.component';
import { DismissibleAlertComponent } from '../../../../shared/dismissible-alert/dismissible-alert.component';

@Component({
  selector: 'app-history-tab',
  standalone: true,
  imports: [CommonModule, FormsModule, HistoryChartComponent, LoadingSpinnerComponent, DismissibleAlertComponent],
  templateUrl: './history-tab.component.html',
})
export class HistoryTabComponent implements OnInit {
  historyData: FlatHistory[] = [];
  loadingHistory = false;
  error: string | null = null;
  historyGroupBy: 'district' | 'rooms' = 'district';
  chartData: HistoryPoint[] = [];
  // Derived from chartData in updateHistoryChart(), so the table is not recomputed on every change detection.
  latestAverageDate = '';
  latestAverageData: { label: string; price: number }[] = [];

  get chartTitle(): string {
    return `Price per m² Trend (${this.historyGroupBy === 'district' ? 'by District' : 'by Room Number'})`;
  }

  constructor(
    private flatsService: FlatsService
  ) {}

  ngOnInit(): void {
    this.loadHistory();
  }

  loadHistory(): void {
    this.loadingHistory = true;
    this.error = null;
    this.flatsService.getHistory()
      .pipe(finalize(() => this.loadingHistory = false))
      .subscribe({
        next: (history) => {
          this.historyData = history;
          this.updateHistoryChart();
        },
        error: (err) => {
          this.error = describeLoadError(err, 'Failed to load history', 'Error loading history');
        }
      });
  }

  onHistoryGroupByChange(): void {
    this.updateHistoryChart();
  }

  private updateHistoryChart(): void {
    if (this.historyGroupBy === 'district') {
      this.aggregateByDistrictHistory();
    } else {
      this.aggregateByRoomsHistory();
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

  private aggregateByDistrictHistory(): void {
    this.aggregateHistoryByLabel(item => item.district || 'Unknown');
  }

  private aggregateByRoomsHistory(): void {
    this.aggregateHistoryByLabel(item => {
      const rooms = item.roomsCount ? normalizeRooms(item.roomsCount) : null;
      return `${rooms?.toString() || 'Unknown'} rooms`;
    });
  }

  private aggregateHistoryByLabel(getLabel: (item: FlatHistory) => string): void {
    const grouped = new Map<string, Map<string, Aggregatable>>();

    this.historyData.forEach(item => {
      if (!item.scrapingDate) {
        return;
      }

      const label = getLabel(item);
      const date = item.scrapingDate;
      const count = item.count ?? 1;
      if (count <= 0) {
        return;
      }

      if (!grouped.has(label)) {
        grouped.set(label, new Map());
      }

      const dateMap = grouped.get(label)!;
      dateMap.set(date, mergeWeightedAverage(dateMap.get(date) ?? { count: 0, avgPricePerMeter: 0 }, {
        count,
        avgPricePerMeter: this.parsePrice(item.avgPricePerMeter)
      }));
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

  private parsePrice(price: number | undefined | null): number {
    if (!price) return 0;
    return Number(price) || 0;
  }

}
