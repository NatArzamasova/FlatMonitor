import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Params } from '@angular/router';
import { NEVER, of, throwError } from 'rxjs';
import { OffersTabComponent } from './offers-tab.component';
import { FlatsService } from '../../../../core/services/flats.service';
import { apiResult } from '../../../../core/testing/api-response.testing';
import { ApiResponse } from '../../../../core/models/api-response.model';
import { FlatOffer } from '../../../../core/models/flat-offer.model';
import { FilterPresetsService } from '../../../../core/services/filter-presets.service';
import {
  createFilterPresetsServiceStub,
  filterPreset,
  FilterPresetsServiceStub
} from '../../../../core/testing/filter-presets.testing';

describe('OffersTabComponent', () => {
  let component: OffersTabComponent;
  let fixture: ComponentFixture<OffersTabComponent>;
  let flatsService: jasmine.SpyObj<FlatsService>;
  let queryParams: Params;
  let presets: FilterPresetsServiceStub;

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

    queryParams = {};
    presets = createFilterPresetsServiceStub();
    const activatedRouteStub = {
      snapshot: {
        get queryParamMap() {
          return convertToParamMap(queryParams);
        }
      }
    };

    await TestBed.configureTestingModule({
      imports: [OffersTabComponent],
      providers: [
        { provide: FlatsService, useValue: flatsServiceSpy },
        { provide: ActivatedRoute, useValue: activatedRouteStub },
        { provide: FilterPresetsService, useValue: presets.service }
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
      expect(component.loading).toBe(false);
      expect(component.error).toBeNull();
    });

    it('should load current offers on init', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

      fixture.detectChanges();

      expect(flatsService.getCurrentOffers).toHaveBeenCalled();
      expect(component.currentOffers.length).toBe(2);
      expect(component.filteredOffers.length).toBe(2);
    });
  });

  describe('loadCurrentOffers', () => {
    it('should load and display current offers', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

      component.loadCurrentOffers();

      expect(component.loading).toBe(false);
      expect(component.error).toBeNull();
      expect(component.currentOffers.length).toBe(2);
      expect(component.filteredOffers.length).toBe(2);
    });

    it('should handle API response failure', () => {
      const failureResponse: ApiResponse<FlatOffer[]> = {
        success: false,
        message: 'Failed to load offers'
      };
      flatsService.getCurrentOffers.and.returnValue(apiResult(failureResponse));

      component.loadCurrentOffers();

      expect(component.error).toBe('Failed to load offers');
      expect(component.loading).toBe(false);
      expect(component.currentOffers).toEqual([]);
    });

    it('should handle HTTP errors', () => {
      const error = new Error('Network error');
      flatsService.getCurrentOffers.and.returnValue(throwError(() => error));

      component.loadCurrentOffers();

      expect(component.error).toContain('Error loading offers');
      expect(component.loading).toBe(false);
    });

    it('should populate available filter options', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

      component.loadCurrentOffers();

      expect(component.availableDistricts).toContain('Mitte');
      expect(component.availableDistricts).toContain('Charlottenburg');
      expect(component.availableRooms).toContain(2);
      expect(component.availableRooms).toContain(3);
    });
  });

  describe('Offer Filtering', () => {
    beforeEach(() => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      component.loadCurrentOffers();
    });

    it('should filter offers by district', () => {
      component.selectedDistricts = ['Mitte'];
      component.applyFilters();

      expect(component.filteredOffers.length).toBe(1);
      expect(component.filteredOffers[0].district).toBe('Mitte');
    });

    it('should filter offers by rooms', () => {
      component.selectedRooms = [3];
      component.applyFilters();

      expect(component.filteredOffers.length).toBe(1);
      expect(component.filteredOffers[0].roomsCount).toBe(3);
    });

    it('should filter offers by price range min', () => {
      component.priceRangeMin = 2200;
      component.applyFilters();

      expect(component.filteredOffers.length).toBe(1);
      expect(component.filteredOffers[0].priceEur).toBe(2500);
    });

    it('should filter offers by price range max', () => {
      component.priceRangeMax = 2200;
      component.applyFilters();

      expect(component.filteredOffers.length).toBe(1);
      expect(component.filteredOffers[0].priceEur).toBe(2000);
    });

    it('should combine multiple filters', () => {
      component.selectedDistricts = ['Mitte', 'Charlottenburg'];
      component.selectedRooms = [2];
      component.priceRangeMin = 1500;
      component.priceRangeMax = 2200;
      component.applyFilters();

      expect(component.filteredOffers.length).toBe(1);
      expect(component.filteredOffers[0].district).toBe('Mitte');
    });

    it('should show offers without a price when no price range is set', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult({
        success: true,
        data: [...mockOffersResponse.data!, { district: 'Mitte', roomsCount: 2, source: 'OhneMakler' }],
        message: 'Success'
      }));
      component.loadCurrentOffers();

      expect(component.filteredOffers.length).toBe(3);
    });

    it('should hide offers without a price once a price range is set', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult({
        success: true,
        data: [...mockOffersResponse.data!, { district: 'Mitte', roomsCount: 2, source: 'OhneMakler' }],
        message: 'Success'
      }));
      component.loadCurrentOffers();
      component.priceRangeMax = 5000;
      component.applyFilters();

      expect(component.filteredOffers.map(offer => offer.priceEur)).toEqual([2000, 2500]);
    });
  });

  describe('Filter controls, spinner and alerts (DOM)', () => {
    const render = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };

    const chooseOption = async (selectId: string, optionText: string) => {
      const select = fixture.nativeElement.querySelector(`#${selectId}`) as HTMLSelectElement;
      const option = Array.from(select.options).find(o => o.textContent!.trim() === optionText)!;
      option.selected = true;
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      await fixture.whenStable();
    };

    const enterPrice = async (inputId: string, value: string) => {
      const input = fixture.nativeElement.querySelector(`#${inputId}`) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      input.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      await fixture.whenStable();
    };

    it('filters by district and rooms through the dropdowns', async () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      await render();

      const label = fixture.nativeElement.querySelector('label[for="offerDistrictSelect"]') as HTMLLabelElement;
      expect(label.textContent!.trim()).toBe('District:');
      await chooseOption('offerDistrictSelect', 'Charlottenburg');
      expect(component.selectedDistricts).toEqual(['Charlottenburg']);
      expect(component.filteredOffers.map(offer => offer.district)).toEqual(['Charlottenburg']);

      await chooseOption('offerRoomsSelect', '2');
      expect(component.selectedRooms).toEqual([2]);
      expect(component.filteredOffers).toEqual([]);
    });

    it('filters by price through the min and max inputs', async () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      await render();

      await enterPrice('priceMin', '2200');
      expect(component.priceRangeMin).toBe(2200);
      expect(component.filteredOffers.map(offer => offer.priceEur)).toEqual([2500]);

      await enterPrice('priceMin', '');
      await enterPrice('priceMax', '2200');
      expect(component.filteredOffers.map(offer => offer.priceEur)).toEqual([2000]);
    });

    it('shows a loading spinner while offers are loading', () => {
      flatsService.getCurrentOffers.and.returnValue(NEVER);
      fixture.detectChanges();

      const spinner = fixture.nativeElement.querySelector('.spinner-border.text-primary') as HTMLElement;
      expect(spinner).toBeTruthy();
      expect(spinner.textContent).toContain('Loading...');
    });

    it('shows load errors in an alert that can be dismissed', () => {
      flatsService.getCurrentOffers.and.returnValue(throwError(() => new Error('Network error')));
      fixture.detectChanges();

      const alert = fixture.nativeElement.querySelector('.alert-danger') as HTMLElement;
      expect(alert.textContent).toContain('Error loading offers: Network error');

      (alert.querySelector('.btn-close') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(component.error).toBeNull();
      expect(fixture.nativeElement.querySelector('.alert-danger')).toBeNull();
    });
  });

  describe('Fractional room counts', () => {
    const fractionalOffers: ApiResponse<FlatOffer[]> = {
      success: true,
      data: [
        { district: 'Mitte', roomsCount: 2, priceEur: 1000, source: 'OhneMakler' },
        { district: 'Mitte', roomsCount: 2.5, priceEur: 2000, source: 'OhneMakler' },
        { district: 'Mitte', roomsCount: 3, priceEur: 3000, source: 'OhneMakler' }
      ],
      message: 'Success'
    };

    beforeEach(() => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(fractionalOffers));
    });

    it('offers fractional room counts as filter options instead of rounding them', () => {
      component.loadCurrentOffers();

      expect(component.availableRooms).toEqual([2, 2.5, 3]);
    });

    it('filters by a fractional room count', () => {
      component.loadCurrentOffers();
      component.selectedRooms = [2.5];
      component.applyFilters();

      expect(component.filteredOffers.map(offer => offer.priceEur)).toEqual([2000]);
    });

    it('applies a fractional room filter from the URL', () => {
      queryParams = { rooms: '2.5' };

      component.ngOnInit();

      expect(component.selectedRooms).toEqual([2.5]);
      expect(component.filteredOffers.map(offer => offer.priceEur)).toEqual([2000]);
    });

    it('renders fractional room counts with one decimal', () => {
      fixture.detectChanges();

      const roomCells = Array.from(fixture.nativeElement.querySelectorAll('tbody tr td:nth-child(2)'))
        .map(cell => (cell as HTMLElement).textContent?.trim());
      expect(roomCells).toEqual(['2', '2.5', '3']);
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
      component.applyFilters();
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
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
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
      component.selectedDistricts = ['Aachen', 'Zürich'];
      component.applyFilters();

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
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

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

  describe('Row view state', () => {
    it('computes price-change and link state when the rows change, not on every change detection', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();
      const discountSpy = spyOn(component, 'getDiscountPercentage').and.callThrough();
      const changedPriceSpy = spyOn(component, 'hasChangedPrice').and.callThrough();

      fixture.detectChanges();
      fixture.detectChanges();

      expect(discountSpy).not.toHaveBeenCalled();
      expect(changedPriceSpy).not.toHaveBeenCalled();

      component.onSort('priceEur');

      expect(discountSpy).toHaveBeenCalledTimes(2);
    });

    it('still renders the rows in the filtered and sorted order', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();

      component.onSort('priceEur');
      component.onSort('priceEur');
      fixture.detectChanges();

      const districts = Array.from(fixture.nativeElement.querySelectorAll('tbody tr td:first-child'))
        .map(cell => (cell as HTMLElement).textContent!.trim());
      expect(districts).toEqual(['Charlottenburg', 'Mitte']);
    });
  });

  describe('Initial URL filters', () => {
    it('loads district and room filters from the URL', () => {
      queryParams = { district: 'Mitte', rooms: '2' };
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

      component.ngOnInit();

      expect(component.selectedDistricts).toEqual(['Mitte']);
      expect(component.selectedRooms).toEqual([2]);
      expect(component.filteredOffers.map(offer => offer.district)).toEqual(['Mitte']);
    });

    it('loads repeated district parameters and ignores invalid room values', () => {
      queryParams = { district: ['Mitte', 'Charlottenburg'], rooms: ['abc', '3'] };
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

      component.ngOnInit();

      expect(component.selectedDistricts).toEqual(['Mitte', 'Charlottenburg']);
      expect(component.selectedRooms).toEqual([3]);
    });
  });

  describe('Filter presets', () => {
    const east = filterPreset(1, 'East', ['Mitte']);

    it('applies the active preset when the tab opens', () => {
      presets.activePreset$.next(east);
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

      fixture.detectChanges();

      expect(component.selectedDistricts).toEqual(['Mitte']);
      expect(component.filteredOffers.map(offer => offer.district)).toEqual(['Mitte']);
    });

    it('shows all offers when there is no active preset', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

      fixture.detectChanges();

      expect(component.selectedDistricts).toEqual([]);
      expect(component.filteredOffers.length).toBe(2);
    });

    it('applies a preset that arrives after the offers', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();

      presets.activePreset$.next(filterPreset(2, 'West', ['Charlottenburg']));

      expect(component.selectedDistricts).toEqual(['Charlottenburg']);
      expect(component.filteredOffers.map(offer => offer.district)).toEqual(['Charlottenburg']);
    });

    it('applies a preset that arrives before the offers', () => {
      flatsService.getCurrentOffers.and.returnValue(NEVER);
      fixture.detectChanges();
      presets.activePreset$.next(east);

      component.currentOffers = mockOffersResponse.data!;
      component.applyFilters();

      expect(component.filteredOffers.map(offer => offer.district)).toEqual(['Mitte']);
    });

    it('keeps the current selection when the active preset becomes null', () => {
      presets.activePreset$.next(east);
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();

      presets.activePreset$.next(null);

      expect(component.selectedDistricts).toEqual(['Mitte']);
    });

    it('lets URL districts win over the preset active when the tab opens', () => {
      queryParams = { district: 'Charlottenburg' };
      presets.activePreset$.next(east);
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

      fixture.detectChanges();

      expect(component.selectedDistricts).toEqual(['Charlottenburg']);
      expect(component.filteredOffers.map(offer => offer.district)).toEqual(['Charlottenburg']);
    });

    it('applies a preset chosen later even when the URL set the districts', () => {
      queryParams = { district: 'Charlottenburg' };
      presets.activePreset$.next(east);
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();

      presets.activePreset$.next(filterPreset(3, 'Both', ['Mitte', 'Charlottenburg']));

      expect(component.selectedDistricts).toEqual(['Mitte', 'Charlottenburg']);
    });

    it('offers preset districts that have no offers, so they can be deselected', () => {
      presets.activePreset$.next(filterPreset(4, 'Far', ['Durlach']));
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

      fixture.detectChanges();

      expect(component.availableDistricts).toEqual(['Charlottenburg', 'Durlach', 'Mitte']);
      expect(component.filteredOffers).toEqual([]);
    });

    it('stops following the active preset when destroyed', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();
      fixture.destroy();

      presets.activePreset$.next(east);

      expect(component.selectedDistricts).toEqual([]);
    });

    it('renders the preset bar next to the district filter', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('app-filter-preset-bar select').id).toBe('offerPresetSelect');
    });
  });

  describe('Offer links', () => {
    const renderLinkCell = (url: string | undefined): HTMLElement => {
      flatsService.getCurrentOffers.and.returnValue(apiResult({
        success: true,
        data: [{ district: 'Mitte', roomsCount: 2, priceEur: 2000, url, source: 'OhneMakler' }],
        message: 'Success'
      }));
      fixture.detectChanges();
      const cells = fixture.nativeElement.querySelectorAll('tbody tr td');
      return cells[cells.length - 1] as HTMLElement;
    };

    it('renders a link for https offer URLs', () => {
      const link = renderLinkCell('https://example.com/flat1').querySelector('a') as HTMLAnchorElement;

      expect(link).toBeTruthy();
      expect(link.getAttribute('href')).toBe('https://example.com/flat1');
      expect(link.getAttribute('rel')).toBe('noopener noreferrer');
      expect(link.textContent).toContain('View (OhneMakler)');
    });

    it('does not render a link for javascript: offer URLs', () => {
      const cell = renderLinkCell('javascript:alert(1)');

      expect(cell.querySelector('a')).toBeNull();
      expect(cell.textContent?.trim()).toBe('-');
    });

    it('does not render a link for non-http offer URLs', () => {
      expect(renderLinkCell('data:text/html,<script>alert(1)</script>').querySelector('a')).toBeNull();
    });

    it('does not render a link when the offer has no URL', () => {
      const cell = renderLinkCell(undefined);

      expect(cell.querySelector('a')).toBeNull();
      expect(cell.textContent?.trim()).toBe('-');
    });
  });
});
