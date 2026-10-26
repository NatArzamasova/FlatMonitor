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

  describe('Table sorting', () => {
    const sortableOffers: FlatOffer[] = [
      {
        district: 'Zürich',
        roomsCount: 3,
        priceEur: 3000,
        originalPriceEur: 3500,
        livingAreaSqm: 90,
        pricePerMeter: 30,
        source: 'ZSource'
      },
      {
        district: 'Aachen',
        roomsCount: 1,
        priceEur: 1000,
        originalPriceEur: 1200,
        livingAreaSqm: 40,
        pricePerMeter: 10,
        source: 'ASource'
      }
    ];

    beforeEach(() => {
      component.currentOffers = sortableOffers;
      component.applyOfferFilters();
    });

    it('sorts every displayed column ascending and toggles the active column descending', () => {
      const cases = [
        { column: 'district' as const, firstValue: 'Aachen' },
        { column: 'roomsCount' as const, firstValue: 1 },
        { column: 'priceEur' as const, firstValue: 1000 },
        { column: 'originalPriceEur' as const, firstValue: 1200 },
        { column: 'livingAreaSqm' as const, firstValue: 40 },
        { column: 'pricePerMeter' as const, firstValue: 10 },
        { column: 'source' as const, firstValue: 'ASource' }
      ];

      for (const { column, firstValue } of cases) {
        component.onSort(column);
        expect(component.filteredOffers[0][column]).toBe(firstValue);
        component.onSort(column);
        expect(component.filteredOffers[0][column]).not.toBe(firstValue);
      }
    });

    it('renders a sortable control for every table column', () => {
      flatsService.getCurrentOffers.and.returnValue(of(mockOffersResponse));
      component.filteredOffers = mockOffersResponse.data!;
      fixture.detectChanges();

      const buttons = fixture.nativeElement.querySelectorAll('thead button[aria-label^="Sort by"]') as NodeListOf<HTMLButtonElement>;
      expect(buttons.length).toBe(7);
      buttons[2].click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('thead th:nth-child(3)').getAttribute('aria-sort')).toBe('ascending');
      expect(component.filteredOffers[0].priceEur).toBe(2000);
    });

    it('reapplies the active sort after filters change', () => {
      component.onSort('priceEur');
      component.selectedOfferDistricts = ['Aachen', 'Zürich'];
      component.onOfferFiltersChange();

      expect(component.filteredOffers.map(offer => offer.priceEur)).toEqual([1000, 3000]);
    });
  });

  describe('Price display', () => {
    it('identifies offers with a changed original price', () => {
      expect(component.hasChangedPrice(mockOffersResponse.data![0])).toBe(true);
      expect(component.hasChangedPrice(mockOffersResponse.data![1])).toBe(false);
    });

    it('does not identify offers missing either price', () => {
      expect(component.hasChangedPrice({ priceEur: 2000 })).toBe(false);
      expect(component.hasChangedPrice({ originalPriceEur: 2200 })).toBe(false);
    });

    it('calculates the discount percentage from the original price', () => {
      expect(component.getDiscountPercentage(mockOffersResponse.data![0])).toBeCloseTo(9.0909, 3);
      expect(component.getDiscountPercentage(mockOffersResponse.data![1])).toBeNull();
    });

    it('renders the discount and red strikethrough original price', () => {
      flatsService.getCurrentOffers.and.returnValue(of(mockOffersResponse));

      fixture.detectChanges();

      const rows = fixture.nativeElement.querySelectorAll('tbody tr');
      const discountedPriceCell = rows[0].querySelectorAll('td')[2] as HTMLElement;
      const originalPrice = rows[0].querySelectorAll('td')[3].querySelector('span') as HTMLElement;
      const unchangedPriceCell = rows[1].querySelectorAll('td')[2] as HTMLElement;
      const unchangedOriginalPrice = rows[1].querySelectorAll('td')[3].querySelector('span') as HTMLElement;

      expect(discountedPriceCell.textContent).toContain('(- 9.1%)');
      expect(originalPrice.classList.contains('text-danger')).toBe(true);
      expect(originalPrice.classList.contains('text-decoration-line-through')).toBe(true);
      expect(unchangedPriceCell.textContent).not.toContain('(-');
      expect(unchangedOriginalPrice.classList.contains('text-danger')).toBe(false);
      expect(unchangedOriginalPrice.classList.contains('text-decoration-line-through')).toBe(false);
    });
  });

  describe('Initial URL filters', () => {
    it('loads district and room filters from the URL', () => {
      const originalSearch = window.location.search;
      window.history.replaceState({}, '', '/?tab=offers&district=Mitte&rooms=2');
      flatsService.getCurrentOffers.and.returnValue(of(mockOffersResponse));

      component.ngOnInit();

      expect(component.selectedOfferDistricts).toEqual(['Mitte']);
      expect(component.selectedOfferRooms).toEqual([2]);

      window.history.replaceState({}, '', `/${originalSearch}`);
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
