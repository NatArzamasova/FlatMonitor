import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { interval, Subject } from 'rxjs';
import { takeUntil, switchMap } from 'rxjs/operators';
import { FlatsService } from '../../core/services/flats.service';
import { FlatAggregation } from '../../core/models/flat-aggregation.model';
import { FlatOffer } from '../../core/models/flat-offer.model';
import { ScrapingStatusResponse, ScrapingHistoryDto } from '../../core/models/scraping-status-response.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit, OnDestroy {
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

  ngOnDestroy(): void {
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
}
