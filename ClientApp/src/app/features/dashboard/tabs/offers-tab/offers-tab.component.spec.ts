import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { OffersTabComponent } from './offers-tab.component';
import { FlatsService } from '../../../../core/services/flats.service';
import { ApiResponse } from '../../../../core/models/api-response.model';
import { FlatOffer } from '../../../../core/models/flat-offer.model';

describe('OffersTabComponent', () => {
  let component: OffersTabComponent;
  let fixture: ComponentFixture<OffersTabComponent>;
  let flatsService: jasmine.SpyObj<FlatsService>;

  const mockOffersResponse: ApiResponse<FlatOffer[]> = {
    success: true,
    data: [
      {
        district: 'Mitte',
        roomsCount: 2,
        priceEur: 2000,
        originalPriceEur: 2200,
        pricePerMeter: 150,
        livingAreaSqm: 65,
        url: 'https://example.com/flat1',
        source: 'OhneMakler'
      },
      {
        district: 'Charlottenburg',
        roomsCount: 3,
        priceEur: 2500,
        originalPriceEur: 2500,
        pricePerMeter: 120,
        livingAreaSqm: 80,
        url: 'https://example.com/flat2',
        source: 'ImmobilienScout24'
      }
    ],
    message: 'Success'
  };

  beforeEach(async () => {
    const flatsServiceSpy = jasmine.createSpyObj('FlatsService', [
      'getCurrentOffers'
    ]);

    await TestBed.configureTestingModule({
      imports: [OffersTabComponent],
      providers: [
        { provide: FlatsService, useValue: flatsServiceSpy }
      ]
    }).compileComponents();

    flatsService = TestBed.inject(FlatsService) as jasmine.SpyObj<FlatsService>;
    fixture = TestBed.createComponent(OffersTabComponent);
    component = fixture.componentInstance;
  });

  describe('Initialization', () => {
    it('should create the component', () => {
      expect(component).toBeTruthy();
    });

    it('should initialize with default values', () => {
      expect(component.currentOffers).toEqual([]);
      expect(component.filteredOffers).toEqual([]);
      expect(component.loadingOffers).toBe(false);
      expect(component.error).toBeNull();
    });

    it('should load current offers on init', () => {
      flatsService.getCurrentOffers.and.returnValue(of(mockOffersResponse));

      fixture.detectChanges();

      expect(flatsService.getCurrentOffers).toHaveBeenCalled();
      expect(component.currentOffers.length).toBe(2);
      expect(component.filteredOffers.length).toBe(2);
    });
  });

  describe('loadCurrentOffers', () => {
    it('should load and display current offers', () => {
      flatsService.getCurrentOffers.and.returnValue(of(mockOffersResponse));

      component.loadCurrentOffers();

      expect(component.loadingOffers).toBe(false);
      expect(component.error).toBeNull();
      expect(component.currentOffers.length).toBe(2);
      expect(component.filteredOffers.length).toBe(2);
    });

    it('should handle API response failure', () => {
      const failureResponse: ApiResponse<FlatOffer[]> = {
        success: false,
        message: 'Failed to load offers'
      };
      flatsService.getCurrentOffers.and.returnValue(of(failureResponse));

      component.loadCurrentOffers();

      expect(component.error).toBe('Failed to load offers');
      expect(component.loadingOffers).toBe(false);
      expect(component.currentOffers).toEqual([]);
    });

    it('should handle HTTP errors', () => {
      const error = new Error('Network error');
      flatsService.getCurrentOffers.and.returnValue(throwError(() => error));

      component.loadCurrentOffers();

      expect(component.error).toContain('Error loading offers');
      expect(component.loadingOffers).toBe(false);
    });

    it('should populate available filter options', () => {
      flatsService.getCurrentOffers.and.returnValue(of(mockOffersResponse));

      component.loadCurrentOffers();

      expect(component.availableOfferDistricts).toContain('Mitte');
      expect(component.availableOfferDistricts).toContain('Charlottenburg');
      expect(component.availableOfferRooms).toContain(2);
      expect(component.availableOfferRooms).toContain(3);
    });
  });

  describe('Offer Filtering', () => {
    beforeEach(() => {
      flatsService.getCurrentOffers.and.returnValue(of(mockOffersResponse));
      component.loadCurrentOffers();
    });

    it('should filter offers by district', () => {
      component.selectedOfferDistricts = ['Mitte'];
      component.onOfferFiltersChange();

      expect(component.filteredOffers.length).toBe(1);
      expect(component.filteredOffers[0].district).toBe('Mitte');
    });

    it('should filter offers by rooms', () => {
      component.selectedOfferRooms = [3];
      component.onOfferFiltersChange();

      expect(component.filteredOffers.length).toBe(1);
      expect(component.filteredOffers[0].roomsCount).toBe(3);
    });

    it('should filter offers by price range min', () => {
      component.priceRangeMin = 2200;
      component.onOfferFiltersChange();

      expect(component.filteredOffers.length).toBe(1);
      expect(component.filteredOffers[0].priceEur).toBe(2500);
    });

    it('should filter offers by price range max', () => {
      component.priceRangeMax = 2200;
      component.onOfferFiltersChange();

      expect(component.filteredOffers.length).toBe(1);
      expect(component.filteredOffers[0].priceEur).toBe(2000);
    });

    it('should combine multiple filters', () => {
      component.selectedOfferDistricts = ['Mitte', 'Charlottenburg'];
      component.selectedOfferRooms = [2];
      component.priceRangeMin = 1500;
      component.priceRangeMax = 2200;
      component.onOfferFiltersChange();

      expect(component.filteredOffers.length).toBe(1);
      expect(component.filteredOffers[0].district).toBe('Mitte');
    });
  });

  describe('URL Validation', () => {
    it('should validate a correct URL', () => {
      const isValid = component.isValidUrl('https://example.com');
      expect(isValid).toBe(true);
    });

    it('should reject invalid URLs', () => {
      const isValid = component.isValidUrl('not-a-url');
      expect(isValid).toBe(false);
    });

    it('should reject undefined URLs', () => {
      const isValid = component.isValidUrl(undefined);
      expect(isValid).toBe(false);
    });

    it('should reject empty URLs', () => {
      const isValid = component.isValidUrl('');
      expect(isValid).toBe(false);
    });
  });

  describe('getSafeUrl', () => {
    it('should bypass security trust for valid URLs', () => {
      const result = component.getSafeUrl('https://example.com');
      expect(result).toBeTruthy();
    });

    it('should handle undefined URLs gracefully', () => {
      const result = component.getSafeUrl(undefined);
      expect(result).toBeTruthy();
    });
  });
});
