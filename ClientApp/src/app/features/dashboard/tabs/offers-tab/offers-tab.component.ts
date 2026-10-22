import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { FlatsService } from '../../../../core/services/flats.service';
import { FilterService } from '../../../../core/services/filter.service';
import { FlatOffer } from '../../../../core/models/flat-offer.model';

@Component({
  selector: 'app-offers-tab',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div>
      <div *ngIf="loadingOffers" class="d-flex justify-content-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>

      <div *ngIf="!loadingOffers && currentOffers.length > 0" class="mb-3 d-flex gap-3 align-items-center flex-wrap">
        <div class="d-flex align-items-center gap-1">
          <label for="offerDistrictSelect" class="form-label mb-0" style="font-size: 0.875rem; white-space: nowrap;">District:</label>
          <select
            id="offerDistrictSelect"
            class="form-select form-select-sm"
            multiple
            size="1"
            [(ngModel)]="selectedOfferDistricts"
            (change)="onOfferFiltersChange()"
            style="min-width: 140px;"
          >
            <option disabled selected hidden>Select...</option>
            <option *ngFor="let district of availableOfferDistricts" [value]="district">
              {{ district }}
            </option>
          </select>
        </div>

        <div class="d-flex align-items-center gap-1">
          <label for="offerRoomsSelect" class="form-label mb-0" style="font-size: 0.875rem; white-space: nowrap;">Rooms:</label>
          <select
            id="offerRoomsSelect"
            class="form-select form-select-sm"
            multiple
            size="1"
            [(ngModel)]="selectedOfferRooms"
            (change)="onOfferFiltersChange()"
            style="min-width: 110px;"
          >
            <option disabled selected hidden>Select...</option>
            <option *ngFor="let rooms of availableOfferRooms" [value]="rooms">
              {{ rooms }}
            </option>
          </select>
        </div>

        <div class="d-flex align-items-center gap-1">
          <label for="priceMin" class="form-label mb-0" style="font-size: 0.875rem; white-space: nowrap;">Price (min):</label>
          <input
            id="priceMin"
            type="number"
            class="form-control form-control-sm"
            [(ngModel)]="priceRangeMin"
            (change)="onOfferFiltersChange()"
            style="width: 100px;"
            placeholder="Min"
          />
        </div>

        <div class="d-flex align-items-center gap-1">
          <label for="priceMax" class="form-label mb-0" style="font-size: 0.875rem; white-space: nowrap;">Price (max):</label>
          <input
            id="priceMax"
            type="number"
            class="form-control form-control-sm"
            [(ngModel)]="priceRangeMax"
            (change)="onOfferFiltersChange()"
            style="width: 100px;"
            placeholder="Max"
          />
        </div>
      </div>

      <div *ngIf="!loadingOffers && filteredOffers.length > 0" class="table-responsive">
        <table class="table table-striped table-hover table-sm">
          <thead class="table-dark">
            <tr>
              <th>District</th>
              <th class="text-center">Rooms</th>
              <th class="text-end">Price (€)</th>
              <th class="text-end">Original price (€)</th>
              <th class="text-end">Living area (m²)</th>
              <th class="text-end">Price/m² (€)</th>
              <th>Link</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let offer of filteredOffers">
              <td>{{ offer.district || '-' }}</td>
              <td class="text-center">{{ offer.roomsCount ? (offer.roomsCount | number: '1.0-0') : '-' }}</td>
              <td class="text-end">
                {{ offer.priceEur ? (offer.priceEur | currency: 'EUR': 'symbol': '1.0-0') : '-' }}
                <span *ngIf="getDiscountPercentage(offer) !== null">
                  (- {{ getDiscountPercentage(offer) | number: '1.0-1' }}%)
                </span>
              </td>
              <td class="text-end">
                <span
                  [class.text-danger]="hasChangedPrice(offer)"
                  [class.text-decoration-line-through]="hasChangedPrice(offer)"
                >
                  {{ offer.originalPriceEur ? (offer.originalPriceEur | currency: 'EUR': 'symbol': '1.0-0') : '-' }}
                </span>
              </td>
              <td class="text-end">{{ offer.livingAreaSqm ? (offer.livingAreaSqm | number: '1.0-1') : '-' }}</td>
              <td class="text-end">{{ offer.pricePerMeter ? (offer.pricePerMeter | number: '1.0-0') : '-' }}</td>
              <td>
                <a *ngIf="isValidUrl(offer.url)" [href]="getSafeUrl(offer.url)" target="_blank" rel="noopener noreferrer" class="btn btn-sm btn-outline-primary">
                  View ({{ offer.source }})
                </a>
                <span *ngIf="!isValidUrl(offer.url)" class="text-muted">-</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div *ngIf="!loadingOffers && currentOffers.length > 0 && filteredOffers.length === 0" class="alert alert-info">
        No offers match the selected filters.
      </div>

      <div *ngIf="!loadingOffers && currentOffers.length === 0" class="alert alert-info">
        No current offers available. Try clicking "Manual Scrape" on the Dashboard tab to fetch data.
      </div>

      <div *ngIf="error" class="alert alert-danger alert-dismissible fade show" role="alert">
        {{ error }}
        <button type="button" class="btn-close" (click)="error = null"></button>
      </div>
    </div>
  `
})
export class OffersTabComponent implements OnInit {
  currentOffers: FlatOffer[] = [];
  filteredOffers: FlatOffer[] = [];
  loadingOffers = false;
  error: string | null = null;

  selectedOfferDistricts: string[] = [];
  selectedOfferRooms: number[] = [];
  priceRangeMin: number | null = null;
  priceRangeMax: number | null = null;
  availableOfferDistricts: string[] = [];
  availableOfferRooms: number[] = [];

  constructor(
    private flatsService: FlatsService,
    private filterService: FilterService,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void {
    this.loadInitialFiltersFromUrl();
    this.loadCurrentOffers();
  }

  hasChangedPrice(offer: FlatOffer): boolean {
    return offer.priceEur != null &&
      offer.originalPriceEur != null &&
      offer.priceEur !== offer.originalPriceEur;
  }

  getDiscountPercentage(offer: FlatOffer): number | null {
    if (!this.hasChangedPrice(offer) || offer.originalPriceEur! <= 0) {
      return null;
    }

    const discountPercentage = ((offer.originalPriceEur! - offer.priceEur!) / offer.originalPriceEur!) * 100;
    return discountPercentage > 0 ? discountPercentage : null;
  }

  private loadInitialFiltersFromUrl(): void {
    const params = new URLSearchParams(window.location.search);
    this.selectedOfferDistricts = params.getAll('district');
    this.selectedOfferRooms = params.getAll('rooms')
      .map(value => Number(value))
      .filter(value => Number.isFinite(value));
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
      if (offer.roomsCount !== undefined && offer.roomsCount !== null) {
        rooms.add(Math.round(offer.roomsCount));
      }
    });

    this.availableOfferDistricts = Array.from(districts).sort();
    this.availableOfferRooms = Array.from(rooms).sort((a, b) => a - b);
  }

  applyOfferFilters(): void {
    let filtered = this.currentOffers;
    filtered = this.filterService.filterByDistricts(filtered, this.selectedOfferDistricts, offer => offer.district);
    filtered = this.filterService.filterByRooms(
      filtered,
      this.selectedOfferRooms,
      offer => offer.roomsCount !== undefined ? Math.round(offer.roomsCount) : undefined
    );
    filtered = this.filterService.filterByPriceRange(
      filtered,
      this.priceRangeMin,
      this.priceRangeMax,
      offer => offer.priceEur
    );

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
