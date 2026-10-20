import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { interval, Subject } from 'rxjs';
import { takeUntil, switchMap } from 'rxjs/operators';
import { FlatsService } from '../../../../core/services/flats.service';
import { FlatAggregation } from '../../../../core/models/flat-aggregation.model';
import { ScrapingStatusResponse } from '../../../../core/models/scraping-status-response.model';

@Component({
  selector: 'app-overview-tab',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div>
      <div class="row mb-4">
        <div class="col">
          <div class="text-muted small mt-2">
            <p *ngIf="lastUpdated" class="mb-1">
              Data last updated: {{ lastUpdated | date: 'short' }}
            </p>
            <p *ngIf="lastScrapingTime" class="mb-1">
              <strong>Last scrape:</strong> {{ lastScrapingTime | date: 'short' }}
              <span class="badge" [ngClass]="{
                'bg-success': lastScrapingStatus === 'Successful',
                'bg-danger': lastScrapingStatus === 'Failed',
                'bg-warning': lastScrapingStatus === 'Canceled',
                'bg-info': lastScrapingStatus === 'Running'
              }">{{ lastScrapingStatus }}</span>
              <span class="badge bg-secondary ms-2" *ngIf="lastScrapingCount > 0">
                {{ lastScrapingCount }} flats
              </span>
            </p>
            <p *ngIf="lastSuccessfulScrapingTime" class="mb-1">
              <strong>Last successful scrape:</strong> {{ lastSuccessfulScrapingTime | date: 'short' }}
              <span class="badge bg-success">Successful</span>
              <span class="badge bg-secondary ms-2" *ngIf="lastSuccessfulScrapingCount > 0">
                {{ lastSuccessfulScrapingCount }} flats
              </span>
            </p>
          </div>
        </div>
        <div class="col-auto d-flex gap-2 align-items-center">
          <button
            class="btn btn-primary"
            (click)="refresh()"
            [disabled]="loading"
          >
            <span *ngIf="loading" class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
            Refresh Data
          </button>
          <button
            class="btn btn-success"
            (click)="triggerScrape()"
            [disabled]="scraping"
            *ngIf="!scraping"
          >
            Manual Scrape
          </button>
          <button
            class="btn btn-danger"
            (click)="cancelScrape()"
            *ngIf="scraping"
          >
            <span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
            Cancel Scraping
          </button>
        </div>
      </div>

      <div *ngIf="success" class="alert alert-info alert-dismissible fade show" role="alert">
        ✓ {{ success }}
        <button type="button" class="btn-close" (click)="success = null"></button>
      </div>

      <div *ngIf="error" class="alert alert-danger alert-dismissible fade show" role="alert">
        {{ error }}
        <button type="button" class="btn-close" (click)="error = null"></button>
      </div>

      <div *ngIf="scrapedCount > 0" class="alert alert-success alert-dismissible fade show" role="alert">
        ✓ Scraped {{ scrapedCount }} flats successfully
        <button type="button" class="btn-close" (click)="scrapedCount = 0"></button>
      </div>

      <div *ngIf="loading" class="d-flex justify-content-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>

      <div *ngIf="!loading && rawData.length > 0" class="mb-3 d-flex gap-3 align-items-center flex-wrap">
        <div class="d-flex align-items-center gap-1">
          <label for="districtSelect" class="form-label mb-0" style="font-size: 0.875rem; white-space: nowrap;">Filter District:</label>
          <select
            id="districtSelect"
            class="form-select form-select-sm"
            multiple
            size="1"
            [(ngModel)]="selectedDistricts"
            (change)="onFiltersChange()"
            style="min-width: 140px;"
          >
            <option disabled selected hidden>Select...</option>
            <option *ngFor="let district of availableDistricts" [value]="district">
              {{ district }}
            </option>
          </select>
        </div>

        <div class="d-flex align-items-center gap-1">
          <label for="roomsSelect" class="form-label mb-0" style="font-size: 0.875rem; white-space: nowrap;">Filter Rooms:</label>
          <select
            id="roomsSelect"
            class="form-select form-select-sm"
            multiple
            size="1"
            [(ngModel)]="selectedRoomCounts"
            (change)="onFiltersChange()"
            style="min-width: 110px;"
          >
            <option disabled selected hidden>Select...</option>
            <ng-container *ngFor="let rooms of availableRooms">
              <option *ngIf="rooms !== null" [value]="rooms">
                {{ rooms }}
              </option>
            </ng-container>
          </select>
        </div>

        <div class="d-flex gap-3">
          <div class="form-check">
            <input
              class="form-check-input"
              type="checkbox"
              id="groupByDistrictToggle"
              [(ngModel)]="groupByDistrict"
              (change)="onGroupingToggle()"
            />
            <label class="form-check-label" for="groupByDistrictToggle">
              Group by District
            </label>
          </div>

          <div class="form-check">
            <input
              class="form-check-input"
              type="checkbox"
              id="groupByRoomsToggle"
              [(ngModel)]="groupByRooms"
              (change)="onGroupingToggle()"
            />
            <label class="form-check-label" for="groupByRoomsToggle">
              Group by Rooms
            </label>
          </div>
        </div>
      </div>

      <div *ngIf="!loading && aggregatedData.length > 0" class="table-responsive">
        <table class="table table-striped table-hover">
          <thead class="table-dark">
            <tr>
              <th>District</th>
              <th>Rooms</th>
              <th class="text-end">Count</th>
              <th class="text-end">Avg Price/m²</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let item of aggregatedData">
              <td>{{ groupByDistrict ? (item.district || '-') : '-' }}</td>
              <td>{{ groupByRooms ? (item.roomsCount || '-') : '-' }}</td>
              <td class="text-end"><span class="badge bg-info">{{ item.count }}</span></td>
              <td class="text-end">
                <strong>{{ item.avgPricePerMeter | currency: 'EUR': 'symbol': '1.0-0' }}</strong>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div *ngIf="!loading && aggregatedData.length === 0" class="alert alert-info">
        No data available. Try clicking "Manual Scrape" to fetch data from sources.
      </div>
    </div>
  `
})
export class OverviewTabComponent implements OnInit, OnDestroy {
  aggregatedData: FlatAggregation[] = [];
  rawData: FlatAggregation[] = [];
  loading = false;
  scraping = false;
  error: string | null = null;
  success: string | null = null;
  lastUpdated: Date | null = null;
  scrapedCount = 0;

  selectedDistricts: string[] = [];
  selectedRoomCounts: number[] = [];
  groupByDistrict = true;
  groupByRooms = true;

  availableDistricts: string[] = [];
  availableRooms: (number | null)[] = [];

  lastScrapingTime: Date | null = null;
  lastScrapingStatus: string | null = null;
  lastScrapingCount = 0;

  lastSuccessfulScrapingTime: Date | null = null;
  lastSuccessfulScrapingCount = 0;

  private destroy$ = new Subject<void>();

  constructor(private flatsService: FlatsService) {}

  ngOnInit(): void {
    this.loadAggregatedData();
    this.loadScrapingStatus();

    interval(2000)
      .pipe(
        switchMap(() => this.flatsService.getScrapeStatus()),
        takeUntil(this.destroy$)
      )
      .subscribe((status) => {
        this.updateScrapingStatus(status);
        if (status.isScrapingInProgress) {
          this.scraping = true;
        } else if (this.scraping) {
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

    if (this.selectedDistricts.length > 0) {
      filtered = filtered.filter(item => this.selectedDistricts.includes(item.district || ''));
    }

    if (this.selectedRoomCounts.length > 0) {
      filtered = filtered.filter(item =>
        item.roomsCount !== undefined && this.selectedRoomCounts.includes(item.roomsCount)
      );
    }

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

    if (status.lastScrapingHistory) {
      this.lastScrapingTime = new Date(status.lastScrapingHistory.startTimeUtc);
      this.lastScrapingStatus = status.lastScrapingHistory.status;
      this.lastScrapingCount = status.lastScrapingHistory.flatsScrapedCount;
    }

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
          this.scrapedCount = response.flatsCount;
          this.scraping = true;
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
}
