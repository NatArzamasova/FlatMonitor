import { Component, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { interval, Subject } from 'rxjs';
import { takeUntil, switchMap } from 'rxjs/operators';
import { FlatsService } from '../../core/services/flats.service';
import { FlatAggregation } from '../../core/models/flat-aggregation.model';
import { FlatOffer } from '../../core/models/flat-offer.model';
import { FlatHistory } from '../../core/models/flat-history.model';
import { ScrapingStatusResponse, ScrapingHistoryDto } from '../../core/models/scraping-status-response.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit, OnDestroy, AfterViewInit {
  aggregatedData: FlatAggregation[] = [];
  rawData: FlatAggregation[] = [];
  currentOffers: FlatOffer[] = [];
  filteredOffers: FlatOffer[] = [];
  loadingOffers = false;
  loading = false;
  scraping = false;
  error: string | null = null;
  success: string | null = null;
  lastUpdated: Date | null = null;
  scrapedCount = 0;

  // Current Offers filters
  selectedOfferDistricts: string[] = [];
  selectedOfferRooms: number[] = [];
  priceRangeMin: number | null = null;
  priceRangeMax: number | null = null;
  availableOfferDistricts: string[] = [];
  availableOfferRooms: number[] = [];

  // History
  historyData: FlatHistory[] = [];
  loadingHistory = false;
  historyGroupBy: 'district' | 'rooms' = 'district';
  chartData: { date: string; price: number; label: string }[] = [];
  chart: any;

  // History filters
  availableHistoryDistricts: string[] = [];
  availableHistoryRooms: number[] = [];
  selectedHistoryDistricts: string[] = [];
  selectedHistoryRooms: number[] = [];

  @ViewChild('historyChart') historyChart: ElementRef<HTMLCanvasElement> | undefined;

  // Last scraping (any status)
  lastScrapingTime: Date | null = null;
  lastScrapingStatus: string | null = null;
  lastScrapingCount = 0;

  // Last successful scraping
  lastSuccessfulScrapingTime: Date | null = null;
  lastSuccessfulScrapingCount = 0;

  selectedDistricts: string[] = [];
  selectedRoomCounts: number[] = [];
  groupByDistrict = true;
  groupByRooms = true;

  availableDistricts: string[] = [];
  availableRooms: (number | null)[] = [];

  private destroy$ = new Subject<void>();

  constructor(
    private flatsService: FlatsService,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void {
    this.loadAggregatedData();
    this.loadCurrentOffers();
    this.loadHistory();
    this.loadScrapingStatus();

    // Poll scraping status: fast while scraping, slow otherwise
    interval(2000)  // Poll every 2 seconds when scraping
      .pipe(
        switchMap(() => this.flatsService.getScrapeStatus()),
        takeUntil(this.destroy$)
      )
      .subscribe((status) => {
        this.updateScrapingStatus(status);
        if (status.isScrapingInProgress) {
          this.scraping = true;
        } else if (this.scraping) {
          // Scraping just finished, reload data
          this.scraping = false;
          this.loadAggregatedData();
        }
      });
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
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAggregatedData(): void {
    this.loading = true;
    this.error = null;
    this.flatsService.getAggregated().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.rawData = response.data;
          this.updateAvailableFilters();
          this.applyFiltersAndGrouping();
          this.lastUpdated = new Date();
        } else {
          this.error = response.message || 'Failed to load data';
        }
        this.loading = false;
      },
      error: (err) => {
        this.error = `Error loading data: ${err.message}`;
        this.loading = false;
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
      if (item.roomsCount !== undefined && item.roomsCount !== null) {
        rooms.add(item.roomsCount);
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

    // Apply district filter
    if (this.selectedDistricts.length > 0) {
      filtered = filtered.filter(item => this.selectedDistricts.includes(item.district || ''));
    }

    // Apply rooms filter
    if (this.selectedRoomCounts.length > 0) {
      filtered = filtered.filter(item =>
        item.roomsCount !== undefined && this.selectedRoomCounts.includes(item.roomsCount)
      );
    }

    // Apply grouping after filters
    if (this.groupByDistrict && this.groupByRooms) {
      this.aggregatedData = filtered;
    } else if (this.groupByDistrict && !this.groupByRooms) {
      this.aggregatedData = this.aggregateByDistrict(filtered);
    } else if (!this.groupByDistrict && this.groupByRooms) {
      this.aggregatedData = this.aggregateByRooms(filtered);
    } else {
      this.aggregatedData = this.aggregateAll(filtered);
    }
  }

  onFiltersChange(): void {
    this.applyFiltersAndGrouping();
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
      const oldCount = current.count;
      const newTotalCount = oldCount + item.count;

      current.avgPricePerMeter =
        (oldCount * current.avgPricePerMeter + item.count * item.avgPricePerMeter) /
        newTotalCount;
      current.count = newTotalCount;
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
      const oldCount = current.count;
      const newTotalCount = oldCount + item.count;

      current.avgPricePerMeter =
        (oldCount * current.avgPricePerMeter + item.count * item.avgPricePerMeter) /
        newTotalCount;
      current.count = newTotalCount;
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
      const oldCount = aggregated.count;
      const newTotalCount = oldCount + item.count;

      aggregated.avgPricePerMeter =
        (oldCount * aggregated.avgPricePerMeter + item.count * item.avgPricePerMeter) /
        newTotalCount;
      aggregated.count = newTotalCount;
    });

    return [aggregated];
  }

  onFilterChange(): void {
    this.applyFiltersAndGrouping();
  }

  onGroupingToggle(): void {
    this.applyFiltersAndGrouping();
  }

  loadScrapingStatus(): void {
    this.flatsService.getScrapeStatus().subscribe({
      next: (status) => {
        this.updateScrapingStatus(status);
      },
      error: (err) => {
        console.error('Failed to load scraping status:', err);
      }
    });
  }

  private updateScrapingStatus(status: ScrapingStatusResponse): void {
    this.scraping = status.isScrapingInProgress;

    // Last scraping (any status)
    if (status.lastScrapingHistory) {
      this.lastScrapingTime = new Date(status.lastScrapingHistory.startTimeUtc);
      this.lastScrapingStatus = status.lastScrapingHistory.status;
      this.lastScrapingCount = status.lastScrapingHistory.flatsScrapedCount;
    }

    // Last successful scraping
    if (status.lastSuccessfulScrapingHistory) {
      this.lastSuccessfulScrapingTime = new Date(status.lastSuccessfulScrapingHistory.startTimeUtc);
      this.lastSuccessfulScrapingCount = status.lastSuccessfulScrapingHistory.flatsScrapedCount;
    }
  }

  triggerScrape(): void {
    this.error = null;
    this.success = null;
    this.flatsService.triggerScrape().subscribe({
      next: (response) => {
        if (response.success) {
          this.success = response.message || 'Scrape job queued';
          this.scraping = true;
          // Immediately check status to update UI
          this.loadScrapingStatus();
        } else {
          this.error = response.message || 'Scrape failed';
        }
      },
      error: (err) => {
        this.error = `Scrape error: ${err.message}`;
        this.scraping = false;
      }
    });
  }

  cancelScrape(): void {
    this.flatsService.cancelScrape().subscribe({
      next: () => {
        this.error = null;
      },
      error: (err) => {
        this.error = `Cancel error: ${err.message}`;
      }
    });
  }

  refresh(): void {
    this.loadAggregatedData();
  }

  loadCurrentOffers(): void {
    this.loadingOffers = true;
    this.error = null;
    this.flatsService.getCurrentOffers().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.currentOffers = response.data;
          this.updateAvailableOfferFilters();
          this.applyOfferFilters();
        } else {
          this.error = response.message || 'Failed to load offers';
        }
        this.loadingOffers = false;
      },
      error: (err) => {
        this.error = `Error loading offers: ${err.message}`;
        this.loadingOffers = false;
      }
    });
  }

  private updateAvailableOfferFilters(): void {
    const districts = new Set<string>();
    const rooms = new Set<number>();

    this.currentOffers.forEach(offer => {
      if (offer.district) {
        districts.add(offer.district);
      }
      if (offer.roomsNumber !== undefined && offer.roomsNumber !== null) {
        rooms.add(Math.round(offer.roomsNumber));
      }
    });

    this.availableOfferDistricts = Array.from(districts).sort();
    this.availableOfferRooms = Array.from(rooms).sort((a, b) => a - b);
  }

  applyOfferFilters(): void {
    let filtered = this.currentOffers;

    // Apply district filter
    if (this.selectedOfferDistricts.length > 0) {
      filtered = filtered.filter(offer => this.selectedOfferDistricts.includes(offer.district || ''));
    }

    // Apply rooms filter
    if (this.selectedOfferRooms.length > 0) {
      filtered = filtered.filter(offer =>
        offer.roomsNumber !== undefined && this.selectedOfferRooms.includes(Math.round(offer.roomsNumber))
      );
    }

    // Apply price range filter
    if (this.priceRangeMin !== null && this.priceRangeMin > 0) {
      filtered = filtered.filter(offer => offer.price !== undefined && offer.price >= this.priceRangeMin!);
    }
    if (this.priceRangeMax !== null && this.priceRangeMax > 0) {
      filtered = filtered.filter(offer => offer.price !== undefined && offer.price <= this.priceRangeMax!);
    }

    this.filteredOffers = filtered;
  }

  onOfferFiltersChange(): void {
    this.applyOfferFilters();
  }

  getSafeUrl(url: string | undefined): SafeUrl {
    return url ? this.sanitizer.bypassSecurityTrustUrl(url) : this.sanitizer.bypassSecurityTrustUrl('');
  }

  isValidUrl(url: string | undefined): boolean {
    if (!url) return false;
    try {
      new URL(url);
      return true;
    } catch {
      return false;
    }
  }

  loadHistory(): void {
    this.loadingHistory = true;
    this.error = null;
    this.flatsService.getHistory().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.historyData = response.data;
          this.updateAvailableHistoryFilters();
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

  private updateAvailableHistoryFilters(): void {
    const districts = new Set<string>();
    const rooms = new Set<number>();

    this.historyData.forEach(item => {
      if (item.district) {
        districts.add(item.district);
      }
      if (item.roomsCount !== undefined && item.roomsCount !== null) {
        rooms.add(Math.round(item.roomsCount * 10) / 10);
      }
    });

    this.availableHistoryDistricts = Array.from(districts).sort();
    this.availableHistoryRooms = Array.from(rooms).sort((a, b) => a - b);

    // Select all by default
    this.selectedHistoryDistricts = [...this.availableHistoryDistricts];
    this.selectedHistoryRooms = [...this.availableHistoryRooms];
  }

  onHistoryGroupByChange(): void {
    this.updateHistoryChart();
    this.renderChart();
  }

  onHistoryFiltersChange(): void {
    this.updateHistoryChart();
    this.renderChart();
  }

  toggleDistrictFilter(district: string, checked: boolean): void {
    if (checked) {
      if (!this.selectedHistoryDistricts.includes(district)) {
        this.selectedHistoryDistricts.push(district);
      }
    } else {
      this.selectedHistoryDistricts = this.selectedHistoryDistricts.filter(d => d !== district);
    }
    this.onHistoryFiltersChange();
  }

  toggleRoomFilter(rooms: number, checked: boolean): void {
    if (checked) {
      if (!this.selectedHistoryRooms.includes(rooms)) {
        this.selectedHistoryRooms.push(rooms);
      }
    } else {
      this.selectedHistoryRooms = this.selectedHistoryRooms.filter(r => r !== rooms);
    }
    this.onHistoryFiltersChange();
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

      // Skip if district is not selected
      if (!this.selectedHistoryDistricts.includes(district)) {
        return;
      }

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

      // Skip if room number is not selected
      if (rooms === null || !this.selectedHistoryRooms.includes(rooms)) {
        return;
      }

      const date = item.scrapingDate || '';
      const price = this.parsePrice(item.avgPricePerMeter);

      const roomsKey = rooms.toString();
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

    // Get unique dates and labels
    const uniqueDates = Array.from(new Set(this.chartData.map(d => d.date))).sort();
    const uniqueLabels = Array.from(new Set(this.chartData.map(d => d.label)));

    // Create datasets for each label (district/room)
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
            beginAtZero: true,
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
