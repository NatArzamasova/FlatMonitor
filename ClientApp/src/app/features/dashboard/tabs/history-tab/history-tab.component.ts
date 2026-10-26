import { Component, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FlatsService } from '../../../../core/services/flats.service';
import { FlatHistory } from '../../../../core/models/flat-history.model';
import { Aggregatable, mergeWeightedAverage, roundPriceToEuro } from '../../../../core/utils/aggregation.utils';

@Component({
  selector: 'app-history-tab',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './history-tab.component.html',
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
    this.aggregateHistoryByLabel(item => item.district || 'Unknown');
  }

  private aggregateByRoomsHistory(): void {
    this.aggregateHistoryByLabel(item => {
      const rooms = item.roomsCount ? Math.round(item.roomsCount * 10) / 10 : null;
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
      if (!dateMap.has(date)) {
        dateMap.set(date, { count: 0, avgPricePerMeter: 0 });
      }
      mergeWeightedAverage(dateMap.get(date)!, {
        count,
        avgPricePerMeter: this.parsePrice(item.avgPricePerMeter)
      });
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
    const maxPrice = Math.max(...this.chartData.map(d => d.price));
    const pricePadding = Math.max((maxPrice - minPrice) * 0.1, minPrice * 0.05, 0.5);
    const chartMinPrice = Math.max(0, minPrice - pricePadding);

    const datasets = uniqueLabels.map((label, idx) => {
      const color = this.getColorForIndex(idx);
      const data = uniqueDates.map(date => {
        const item = this.chartData.find(d => d.date === date && d.label === label);
        return item ? item.price : null;
      });
      const latestDataIndex = data.reduce(
        (latestIndex, value, index) => value !== null ? index : latestIndex,
        -1
      );

      return {
        label,
        data,
        borderColor: color,
        backgroundColor: color + '20',
        borderWidth: 2,
        pointRadius: data.map((value, index) => index === latestDataIndex ? 6 : 3),
        pointHoverRadius: 7,
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
          },
          tooltip: {
            callbacks: {
              label: (context: any) => `${context.dataset.label}: €${roundPriceToEuro(Number(context.parsed.y))}/m²`
            }
          }
        },
        scales: {
          y: {
            beginAtZero: false,
            min: chartMinPrice,
            title: {
              display: true,
              text: 'Price per m² (€)'
            }
          },
          x: {
            ticks: {
              callback: (value: any) => this.formatHistoryDate(uniqueDates[Number(value)])
            },
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
      '#0072B2', '#E69F00', '#009E73', '#D55E00', '#CC79A7', '#56B4E9',
      '#F0E442', '#000000', '#332288', '#88CCEE', '#44AA99', '#117733',
      '#999933', '#DDCC77', '#CC6677', '#882255', '#AA4499', '#661100',
      '#6699CC', '#AA4466', '#4477AA', '#228833', '#EE6677', '#BBBBBB',
      '#1B9E77', '#D95F02', '#7570B3', '#E7298A'
    ];
    return colors[index % colors.length];
  }

  private formatHistoryDate(date: string): string {
    const parsedDate = new Date(`${date}T00:00:00Z`);
    if (Number.isNaN(parsedDate.getTime())) {
      return date;
    }

    return new Intl.DateTimeFormat('en-GB', {
      day: '2-digit',
      month: 'short',
      timeZone: 'UTC'
    }).format(parsedDate);
  }
}
