import { Component, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FlatsService } from '../../../../core/services/flats.service';
import { FlatHistory } from '../../../../core/models/flat-history.model';

@Component({
  selector: 'app-history-tab',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div>
      <div *ngIf="loadingHistory" class="d-flex justify-content-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>

      <div *ngIf="!loadingHistory && historyData.length > 0" class="mb-3">
        <div class="d-flex gap-3 align-items-center flex-wrap">
          <div>
            <label class="form-label mb-0 me-2">Group By:</label>
            <div class="form-check form-check-inline">
              <input
                class="form-check-input"
                type="radio"
                id="groupByDistrictRadio"
                name="historyGroupBy"
                value="district"
                [(ngModel)]="historyGroupBy"
                (change)="onHistoryGroupByChange()"
              />
              <label class="form-check-label" for="groupByDistrictRadio">
                District
              </label>
            </div>
            <div class="form-check form-check-inline">
              <input
                class="form-check-input"
                type="radio"
                id="groupByRoomsRadio"
                name="historyGroupBy"
                value="rooms"
                [(ngModel)]="historyGroupBy"
                (change)="onHistoryGroupByChange()"
              />
              <label class="form-check-label" for="groupByRoomsRadio">
                Room Number
              </label>
            </div>
          </div>

        </div>
      </div>

      <div *ngIf="!loadingHistory && chartData.length > 0" style="position: relative; height: 400px; width: 100%; margin-bottom: 20px;">
        <canvas #historyChart></canvas>
      </div>

      <div *ngIf="!loadingHistory && historyData.length === 0" class="alert alert-info">
        No history data available yet. Historical data will be collected after multiple scrapes.
      </div>

      <div *ngIf="error" class="alert alert-danger alert-dismissible fade show" role="alert">
        {{ error }}
        <button type="button" class="btn-close" (click)="error = null"></button>
      </div>
    </div>
  `
})
export class HistoryTabComponent implements OnInit, OnDestroy, AfterViewInit {
  historyData: FlatHistory[] = [];
  loadingHistory = false;
  error: string | null = null;
  historyGroupBy: 'district' | 'rooms' = 'district';
  chartData: { date: string; price: number; label: string }[] = [];
  chart: any;

  @ViewChild('historyChart') historyChart: ElementRef<HTMLCanvasElement> | undefined;

  constructor(
    private flatsService: FlatsService
  ) {}

  ngOnInit(): void {
    this.loadHistory();
  }

  ngAfterViewInit(): void {
    if (this.historyData.length > 0) {
      this.renderChart();
    }
  }

  ngOnDestroy(): void {
    if (this.chart) {
      this.chart.destroy();
    }
  }

  loadHistory(): void {
    this.loadingHistory = true;
    this.error = null;
    this.flatsService.getHistory().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.historyData = response.data;
          this.updateHistoryChart();
          setTimeout(() => this.renderChart(), 100);
        } else {
          this.error = response.message || 'Failed to load history';
        }
        this.loadingHistory = false;
      },
      error: (err) => {
        this.error = `Error loading history: ${err.message}`;
        this.loadingHistory = false;
      }
    });
  }

  onHistoryGroupByChange(): void {
    this.updateHistoryChart();
    this.renderChart();
  }

  private updateHistoryChart(): void {
    if (this.historyGroupBy === 'district') {
      this.aggregateByDistrictHistory();
    } else {
      this.aggregateByRoomsHistory();
    }
  }

  private aggregateByDistrictHistory(): void {
    const grouped = new Map<string, Map<string, number>>();

    this.historyData.forEach(item => {
      const district = item.district || 'Unknown';
      const date = item.scrapingDate || '';
      const price = this.parsePrice(item.avgPricePerMeter);

      if (!grouped.has(district)) {
        grouped.set(district, new Map());
      }
      grouped.get(district)!.set(date, price);
    });

    this.chartData = [];
    const dates = new Set<string>();
    this.historyData.forEach(item => {
      if (item.scrapingDate) dates.add(item.scrapingDate);
    });

    const sortedDates = Array.from(dates).sort();
    grouped.forEach((dateMap, district) => {
      sortedDates.forEach(date => {
        if (dateMap.has(date)) {
          this.chartData.push({
            date,
            price: dateMap.get(date)!,
            label: district
          });
        }
      });
    });
  }

  private aggregateByRoomsHistory(): void {
    const grouped = new Map<string, Map<string, number>>();

    this.historyData.forEach(item => {
      const rooms = item.roomsCount ? Math.round(item.roomsCount * 10) / 10 : null;
      const date = item.scrapingDate || '';
      const price = this.parsePrice(item.avgPricePerMeter);

      const roomsKey = rooms?.toString() || 'Unknown';
      if (!grouped.has(roomsKey)) {
        grouped.set(roomsKey, new Map());
      }
      grouped.get(roomsKey)!.set(date, price);
    });

    this.chartData = [];
    const dates = new Set<string>();
    this.historyData.forEach(item => {
      if (item.scrapingDate) dates.add(item.scrapingDate);
    });

    const sortedDates = Array.from(dates).sort();
    grouped.forEach((dateMap, rooms) => {
      sortedDates.forEach(date => {
        if (dateMap.has(date)) {
          this.chartData.push({
            date,
            price: dateMap.get(date)!,
            label: `${rooms} rooms`
          });
        }
      });
    });
  }

  private parsePrice(price: number | undefined | null): number {
    if (!price) return 0;
    return Number(price) || 0;
  }

  private renderChart(): void {
    if (!this.historyChart || this.chartData.length === 0) return;

    if (this.chart) {
      this.chart.destroy();
    }

    const canvas = this.historyChart.nativeElement;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const uniqueDates = Array.from(new Set(this.chartData.map(d => d.date))).sort();
    const uniqueLabels = Array.from(new Set(this.chartData.map(d => d.label)));
    const minPrice = Math.min(...this.chartData.map(d => d.price));

    const datasets = uniqueLabels.map((label, idx) => {
      const color = this.getColorForIndex(idx);
      const data = uniqueDates.map(date => {
        const item = this.chartData.find(d => d.date === date && d.label === label);
        return item ? item.price : null;
      });

      return {
        label,
        data,
        borderColor: color,
        backgroundColor: color + '20',
        borderWidth: 2,
        pointRadius: 4,
        pointBackgroundColor: color,
        tension: 0.4,
        fill: false
      };
    });

    const chartConfig = {
      type: 'line' as const,
      data: {
        labels: uniqueDates,
        datasets
      },
      options: {
        responsive: true,
        maintainAspectRatio: true,
        plugins: {
          legend: {
            display: true,
            position: 'top' as const
          },
          title: {
            display: true,
            text: `Price per m² Trend (${this.historyGroupBy === 'district' ? 'by District' : 'by Room Number'})`
          }
        },
        scales: {
          y: {
            beginAtZero: false,
            min: minPrice,
            title: {
              display: true,
              text: 'Price per m² (€)'
            }
          },
          x: {
            title: {
              display: true,
              text: 'Date'
            }
          }
        }
      }
    };

    const Chart = (window as any).Chart;
    this.chart = new Chart(ctx, chartConfig);
  }

  private getColorForIndex(index: number): string {
    const colors = [
      '#201923', '#fcff5d', '#7dfc00', '#0ec434', '#228c68',
      '#8ad8e8', '#235b54', '#29bdab', '#3998f5', '#37294f',
      '#277da7', '#3750db', '#f22020', '#991919', '#ffcba5',
      '#e68f66', '#c56133', '#96341c', '#632819', '#ffc413',
      '#f47a22', '#2f2aa0', '#b732cc', '#772b9d', '#f07cab',
      '#d30b94', '#c3a5b4', '#946aa2', '#5d4c86'
    ];
    return colors[index % colors.length];
  }
}
