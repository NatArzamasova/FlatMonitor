import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideAppLocale } from '../../../../core/utils/locale.utils';
import { ActivatedRoute, convertToParamMap, Params } from '@angular/router';
import { NEVER, of, Subject, throwError } from 'rxjs';
import { OffersTabComponent } from './offers-tab.component';
import { FlatsService } from '../../../../core/services/flats.service';
import { DataReloadService } from '../../../../core/services/data-reload.service';
import { ScrapeStatusService } from '../../../../core/services/scrape-status.service';
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
  let reload$: Subject<void>;

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
    reload$ = new Subject<void>();
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
        // Numbers, prices and dates as in the app (German formats).
        provideAppLocale(),
        { provide: FlatsService, useValue: flatsServiceSpy },
        { provide: ActivatedRoute, useValue: activatedRouteStub },
        { provide: FilterPresetsService, useValue: presets.service },
        { provide: DataReloadService, useValue: { reload$ } },
        // Read by the empty state's "Start a scrape" button.
        { provide: ScrapeStatusService, useValue: jasmine.createSpyObj('ScrapeStatusService', ['triggerScrape'], { status$: NEVER }) }
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

    /** Opens the multi-select filter `selectId` and ticks the option `optionText`. */
    const chooseOption = async (selectId: string, optionText: string) => {
      (fixture.nativeElement.querySelector(`#${selectId}`) as HTMLButtonElement).click();
      fixture.detectChanges();
      const rows = Array.from(fixture.nativeElement.querySelectorAll(`#${selectId}-menu .multi-select-option`)) as HTMLLabelElement[];
      const row = rows.find(candidate => candidate.textContent!.trim() === optionText)!;
      row.querySelector('input')!.click();
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

    it('shows a loading skeleton while offers are loading', () => {
      flatsService.getCurrentOffers.and.returnValue(NEVER);
      fixture.detectChanges();

      const skeleton = fixture.nativeElement.querySelector('app-loading-skeleton [role=\"status\"]') as HTMLElement;
      expect(skeleton).toBeTruthy();
      expect(skeleton.textContent).toContain('Loading…');
      expect(fixture.nativeElement.querySelector('app-empty-state')).toBeNull();
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
      expect(roomCells).toEqual(['2', '2,5', '3']);
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
      expect(buttons.length).toBe(9);
      buttons[2].click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('thead th:nth-child(3)').getAttribute('aria-sort')).toBe('ascending');
      expect(component.filteredOffers[0].priceEur).toBe(2000);
    });

    it('sorts the cards through their sort control the same way as the header buttons', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();
      const headerOrder = () => {
        (fixture.nativeElement.querySelectorAll('thead button[aria-label^="Sort by"]')[2] as HTMLButtonElement).click();
        fixture.detectChanges();
        return component.filteredOffers.map(offer => offer.priceEur);
      };
      const ascendingByHeader = headerOrder();
      const descendingByHeader = headerOrder();
      component.sort.column = null;
      component.applyFilters();
      fixture.detectChanges();

      const select = fixture.nativeElement.querySelector('app-offer-card-list #offerCardSort') as HTMLSelectElement;
      select.value = 'priceEur';
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      expect(component.filteredOffers.map(offer => offer.priceEur)).toEqual(ascendingByHeader);

      (fixture.nativeElement.querySelector('app-offer-card-list .offer-sort-direction') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(component.filteredOffers.map(offer => offer.priceEur)).toEqual(descendingByHeader);
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

    it('renders the discount as a green badge with the original price in its tooltip', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

      fixture.detectChanges();

      const rows = fixture.nativeElement.querySelectorAll('tbody tr');
      const discountedPriceCell = rows[0].querySelectorAll('td')[2] as HTMLElement;
      const unchangedPriceCell = rows[1].querySelectorAll('td')[2] as HTMLElement;

      const badge = discountedPriceCell.querySelector('.price-drop') as HTMLElement;
      expect(badge.textContent!.trim()).toBe('−9,1 %');
      expect(badge.classList).toContain('bg-success-subtle');
      expect(badge.title).toBe('Was 2.200 € when first listed');
      expect(unchangedPriceCell.querySelector('.price-drop')).toBeNull();
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
    beforeEach(() => flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse)));

    it('loads district and room filters from the URL', () => {
      queryParams = { district: 'Mitte', rooms: '2' };
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));

      component.ngOnInit();

      expect(component.selectedDistricts).toEqual(['Mitte']);
      expect(component.selectedRooms).toEqual([2]);
      expect(component.filteredOffers.map(offer => offer.district)).toEqual(['Mitte']);
    });

    it('selects no preset when the URL has districts but names no preset (no default preset shown as modified)', () => {
      queryParams = { district: 'Daxlagen' };

      component.ngOnInit();

      expect(presets.service.selectPreset).toHaveBeenCalledOnceWith(null);
      expect(component.selectedDistricts).toEqual(['Daxlagen']);
    });

    it('selects the preset named in the URL and keeps the URL districts as the selection', () => {
      queryParams = { district: ['Mitte', 'Charlottenburg'], preset: '7' };

      component.ngOnInit();

      expect(presets.service.selectPreset).toHaveBeenCalledOnceWith(7);
      expect(component.selectedDistricts).toEqual(['Mitte', 'Charlottenburg']);
    });

    it('shows all districts without a preset for preset=none and no districts', () => {
      queryParams = { preset: 'none' };
      presets.activePreset$.next(filterPreset(1, 'Default', ['Mitte'], true));

      component.ngOnInit();

      expect(presets.service.selectPreset).toHaveBeenCalledOnceWith(null);
      expect(component.selectedDistricts).toEqual([]);
    });

    it('treats an invalid preset id as no preset', () => {
      queryParams = { preset: 'abc' };

      component.ngOnInit();

      expect(presets.service.selectPreset).toHaveBeenCalledOnceWith(null);
    });

    it('leaves the preset alone when the URL has no filters', () => {
      queryParams = {};

      component.ngOnInit();

      expect(presets.service.selectPreset).not.toHaveBeenCalled();
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

    it('shows all districts when the active preset becomes null', () => {
      presets.activePreset$.next(east);
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();

      presets.activePreset$.next(null);

      expect(component.selectedDistricts).toEqual([]);
      expect(component.filteredOffers.length).toBe(2);
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
      expect(link.querySelector('.source-badge')!.textContent!.trim()).toBe('ohne-makler');
      expect(link.querySelector('.source-badge')!.classList).toContain('source-ohne-makler');
      expect(link.title).toBe('Open the offer on OhneMakler');
    });

    it('does not render a link for javascript: offer URLs', () => {
      const cell = renderLinkCell('javascript:alert(1)');

      expect(cell.querySelector('a')).toBeNull();
      expect(cell.querySelector('.source-badge')!.textContent!.trim()).toBe('ohne-makler');
    });

    it('does not render a link for non-http offer URLs', () => {
      expect(renderLinkCell('data:text/html,<script>alert(1)</script>').querySelector('a')).toBeNull();
    });

    it('does not render a link when the offer has no URL', () => {
      const cell = renderLinkCell(undefined);

      expect(cell.querySelector('a')).toBeNull();
      expect(cell.querySelector('.source-badge')!.textContent!.trim()).toBe('ohne-makler');
    });
  });

  describe('Availability column', () => {
    const renderOffers = (offers: FlatOffer[]) => {
      flatsService.getCurrentOffers.and.returnValue(apiResult({ success: true, data: offers, message: 'Success' }));
      fixture.detectChanges();
    };
    const availabilityCells = (): HTMLElement[] => Array.from(fixture.nativeElement.querySelectorAll('tbody td.offer-availability'));

    it('has an Availability header after Price/m², without sorting', () => {
      renderOffers([{ district: 'Mitte', availability: 'sofort' }]);

      const headers = Array.from(fixture.nativeElement.querySelectorAll('thead th') as NodeListOf<HTMLElement>);
      expect(headers.map(header => header.textContent!.trim().replace(/[↑↓↕]/g, '').trim()))
        .toEqual(['District', 'Rooms', 'Price (€)', 'Living area (m²)', 'Price/m² (€)', 'Hausgeld (€)', 'Provision', 'EE Class', 'Availability', 'Source']);
      expect(headers[8].querySelector('button')).toBeNull();
    });

    it('shows the availability of each offer, with the full text as title, and - when it is missing', () => {
      renderOffers([
        { district: 'Mitte', priceEur: 1000, availability: '01.03.2027' },
        { district: 'Weststadt', priceEur: 2000 }
      ]);

      expect(availabilityCells().map(cell => cell.textContent!.trim())).toEqual(['01.03.2027', '-']);
      expect(availabilityCells()[0].title).toBe('01.03.2027');
      expect(availabilityCells()[1].title).toBe('');
    });

    it('aligns the header and the cells to the right', () => {
      renderOffers([{ district: 'Mitte', availability: 'sofort' }]);

      const header = fixture.nativeElement.querySelectorAll('thead th')[8] as HTMLElement;
      expect(header.classList).toContain('text-end');
      expect(availabilityCells()[0].classList).toContain('text-end');
      expect(getComputedStyle(header).textAlign).toMatch(/^(right|end)$/);
      expect(getComputedStyle(availabilityCells()[0]).textAlign).toMatch(/^(right|end)$/);
    });

    it('renders ten skeleton columns while loading', () => {
      flatsService.getCurrentOffers.and.returnValue(NEVER);
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelectorAll('app-loading-skeleton thead th').length).toBe(10);
    });
  });

  describe('Hausgeld column', () => {
    const renderOffers = (offers: FlatOffer[]) => {
      flatsService.getCurrentOffers.and.returnValue(apiResult({ success: true, data: offers, message: 'Success' }));
      fixture.detectChanges();
    };
    const cells = (): string[] =>
      Array.from(fixture.nativeElement.querySelectorAll('tbody td.offer-service-charge') as NodeListOf<HTMLElement>)
        .map(cell => cell.textContent!.trim());

    it('has a right-aligned, sortable "Hausgeld (€)" header after Price/m²', () => {
      renderOffers([{ district: 'Mitte', serviceChargeEur: 250 }]);

      const header = fixture.nativeElement.querySelectorAll('thead th')[5] as HTMLElement;
      expect(header.textContent).toContain('Hausgeld (€)');
      expect(header.classList).toContain('text-end');
      expect(header.querySelector('abbr')!.title).toBe('Monthly service charge');
      expect(header.querySelector('button')!.getAttribute('aria-label')).toBe('Sort by Hausgeld');
    });

    it('shows the monthly Hausgeld in euros in German format, 0 as 0 €, and - when it is missing', () => {
      renderOffers([
        { district: 'Mitte', priceEur: 1, serviceChargeEur: 1245 },
        { district: 'Mitte', priceEur: 2, serviceChargeEur: 0 },
        { district: 'Mitte', priceEur: 3 }
      ]);

      expect(cells()).toEqual(['1.245 €', '0 €', '-']);
    });

    it('sorts by Hausgeld, offers without one last', () => {
      renderOffers([
        { district: 'Mitte', serviceChargeEur: 400 },
        { district: 'Mitte' },
        { district: 'Mitte', serviceChargeEur: 150 }
      ]);

      component.onSort('serviceChargeEur');

      expect(component.filteredOffers.map(offer => offer.serviceChargeEur)).toEqual([150, 400, undefined]);
    });
  });

  describe('Provision column', () => {
    const renderOffers = (offers: FlatOffer[]) => {
      flatsService.getCurrentOffers.and.returnValue(apiResult({ success: true, data: offers, message: 'Success' }));
      fixture.detectChanges();
    };
    const cells = (): HTMLElement[] => Array.from(fixture.nativeElement.querySelectorAll('tbody td.offer-commission'));

    it('has a right-aligned, sortable "Provision" header after Hausgeld', () => {
      renderOffers([{ district: 'Mitte', commissionPercent: 3.57 }]);

      const header = fixture.nativeElement.querySelectorAll('thead th')[6] as HTMLElement;
      expect(header.textContent!.trim()).toContain('Provision');
      expect(header.classList).toContain('text-end');
      expect(header.querySelector('abbr')!.title).toBe("Buyer's commission in percent of the price");
      expect(header.querySelector('button')!.getAttribute('aria-label')).toBe('Sort by Provision');
    });

    it('shows the commission in percent with a decimal comma, 0 as 0 %, and - when it is missing', () => {
      renderOffers([
        { district: 'Mitte', priceEur: 1, commissionPercent: 3.57 },
        { district: 'Mitte', priceEur: 2, commissionPercent: 2.975 },
        { district: 'Mitte', priceEur: 3, commissionPercent: 0 },
        { district: 'Mitte', priceEur: 4 }
      ]);

      expect(cells().map(cell => cell.textContent!.trim())).toEqual(['3,57 %', '2,975 %', '0 %', '-']);
      expect(cells()[0].classList).toContain('text-nowrap');
    });

    it('sorts by commission, offers without one last', () => {
      renderOffers([
        { district: 'Mitte', commissionPercent: 3.57 },
        { district: 'Mitte' },
        { district: 'Mitte', commissionPercent: 0 }
      ]);

      component.onSort('commissionPercent');

      expect(component.filteredOffers.map(offer => offer.commissionPercent)).toEqual([0, 3.57, undefined]);
    });
  });

  describe('Column layout', () => {
    it('has one cell per header in every row, in the header order', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult({
        success: true,
        data: [{ district: 'Mitte', priceEur: 1000, serviceChargeEur: 250, commissionPercent: 3.57, availability: 'sofort', energyEfficiencyClass: 'B' }],
        message: 'Success'
      }));
      fixture.detectChanges();

      const headerCount = fixture.nativeElement.querySelectorAll('thead th').length;
      const row = fixture.nativeElement.querySelector('tbody tr') as HTMLTableRowElement;
      expect(row.cells.length).toBe(headerCount);
      expect(row.cells[5].classList).toContain('offer-service-charge');
      expect(row.cells[6].classList).toContain('offer-commission');
      expect(row.cells[7].querySelector('.ee-badge')).not.toBeNull();
      expect(row.cells[8].classList).toContain('offer-availability');
    });
  });

  describe('EE Class column', () => {
    const renderOffers = (offers: FlatOffer[]) => {
      flatsService.getCurrentOffers.and.returnValue(apiResult({ success: true, data: offers, message: 'Success' }));
      fixture.detectChanges();
    };
    const eeCells = (): HTMLElement[] =>
      Array.from(fixture.nativeElement.querySelectorAll('tbody tr td:nth-child(8)'));

    it('has a centred, sortable "EE Class" header explained by an abbreviation', () => {
      renderOffers([{ district: 'Mitte', energyEfficiencyClass: 'B' }]);

      const header = fixture.nativeElement.querySelectorAll('thead th')[7] as HTMLElement;
      expect(header.classList).toContain('text-center');
      expect(header.querySelector('abbr')!.title).toBe('Energy efficiency class');
      expect(header.querySelector('button')!.getAttribute('aria-label')).toBe('Sort by Energy Efficiency Class');
    });

    it('shows each class as a pill in its scale colour, A_PLUS as A+, and - without a class', () => {
      renderOffers([
        { district: 'Mitte', priceEur: 1, energyEfficiencyClass: 'A_PLUS' },
        { district: 'Mitte', priceEur: 2, energyEfficiencyClass: 'G' },
        { district: 'Mitte', priceEur: 3 }
      ]);

      const pills = eeCells().map(cell => cell.querySelector('.ee-badge') as HTMLElement | null);
      expect(pills[0]!.textContent!.trim()).toBe('A+');
      expect(pills[0]!.classList).toContain('ee-a-plus');
      expect(pills[1]!.classList).toContain('ee-g');
      expect(pills[1]!.title).toBe('Energy efficiency class G');
      expect(pills[2]).toBeNull();
      expect(eeCells()[2].textContent!.trim()).toBe('-');
    });

    it('sorts by the scale (A+ first), not alphabetically, with offers without a class last', () => {
      renderOffers([
        { district: 'Mitte', energyEfficiencyClass: 'C' },
        { district: 'Mitte' },
        { district: 'Mitte', energyEfficiencyClass: 'A' },
        { district: 'Mitte', energyEfficiencyClass: 'A_PLUS' },
        { district: 'Mitte', energyEfficiencyClass: 'H' }
      ]);

      component.onSort('energyEfficiencyClass');
      expect(component.filteredOffers.map(offer => offer.energyEfficiencyClass)).toEqual(['A_PLUS', 'A', 'C', 'H', undefined]);

      component.onSort('energyEfficiencyClass');
      expect(component.filteredOffers.map(offer => offer.energyEfficiencyClass)).toEqual(['H', 'C', 'A', 'A_PLUS', undefined]);
    });
  });

  describe('Cards on small screens', () => {
    beforeEach(() => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();
    });

    it('renders the offers as cards below md and the table from md', () => {
      const cardList = fixture.nativeElement.querySelector('app-offer-card-list') as HTMLElement;
      const tableCard = fixture.nativeElement.querySelector('.table-card') as HTMLElement;

      expect(cardList.classList).toContain('d-md-none');
      expect(cardList.classList).toContain('card-list-fill');
      expect(tableCard.classList).toContain('d-none');
      expect(tableCard.classList).toContain('d-md-block');
      expect(cardList.querySelectorAll('article.offer-card').length).toBe(component.offerRows.length);
    });

    it('gives the cards the same rows, in the same order, as the table', () => {
      component.onSort('priceEur');
      fixture.detectChanges();

      const cardPrices = Array.from(fixture.nativeElement.querySelectorAll('app-offer-card-list .offer-card-price'))
        .map(price => (price as HTMLElement).textContent!.trim());
      const tablePrices = Array.from(fixture.nativeElement.querySelectorAll('tbody tr td:nth-child(3)'))
        .map(cell => (cell as HTMLElement).firstChild!.textContent!.trim());
      expect(cardPrices).toEqual(tablePrices);
    });
  });

  describe('Filter layout', () => {
    const row = (name: string): HTMLElement => fixture.nativeElement.querySelector(`[data-filter-row="${name}"]`);
    const rowNames = (): string[] =>
      Array.from(fixture.nativeElement.querySelectorAll('[data-filter-row]') as NodeListOf<HTMLElement>)
        .map(element => element.dataset['filterRow']!);
    /** Direct children of a row, by tag name or by the id of the control inside. */
    const rowItems = (name: string): string[] =>
      Array.from(row(name).children).map(child =>
        child.tagName.toLowerCase().startsWith('app-')
          ? `${child.tagName.toLowerCase()}#${child.querySelector('select, input, button[id]')?.id ?? ''}`
          : child.querySelector('select, input')?.id ?? child.tagName.toLowerCase());

    beforeEach(() => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();
    });

    it('puts the district filter and the preset bar alone in the first row, other filters in the next', () => {
      expect(rowNames()).toEqual(['districts', 'other']);
      expect(rowItems('districts')).toEqual([
        'app-multi-select-filter#offerDistrictSelect',
        'app-filter-preset-bar#offerPresetSelect'
      ]);
      expect(rowItems('other')).toEqual(['app-multi-select-filter#offerRoomsSelect', 'priceMin', 'priceMax']);
    });
  });

  describe('Active filter chips', () => {
    const chipTexts = (): string[] =>
      Array.from(fixture.nativeElement.querySelectorAll('.filter-chip') as NodeListOf<HTMLElement>)
        .map(chip => chip.firstChild!.textContent!.trim());
    const removeChip = (label: string) => {
      (fixture.nativeElement.querySelector(`[aria-label="Remove filter ${label}"]`) as HTMLButtonElement).click();
      fixture.detectChanges();
    };
    const reset = () => {
      (fixture.nativeElement.querySelector('.filter-reset') as HTMLButtonElement).click();
      fixture.detectChanges();
    };

    beforeEach(() => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();
      component.selectedDistricts = ['Mitte', 'Charlottenburg'];
      component.selectedRooms = [2];
      component.priceRangeMin = 1000;
      component.priceRangeMax = 3000;
      component.applyFilters();
      fixture.detectChanges();
    });

    it('shows every active filter as a chip', () => {
      expect(chipTexts()).toEqual(['Mitte', 'Charlottenburg', '2 rooms', 'from 1.000 €', 'up to 3.000 €']);
      expect(component.filteredOffers.map(offer => offer.district)).toEqual(['Mitte']);
    });

    it('removes one filter per chip and filters again', () => {
      removeChip('2 rooms');
      expect(component.selectedRooms).toEqual([]);
      expect(component.filteredOffers.length).toBe(2);

      removeChip('Mitte');
      removeChip('from 1.000 €');
      removeChip('up to 3.000 €');

      expect(component.selectedDistricts).toEqual(['Charlottenburg']);
      expect(component.priceRangeMin).toBeNull();
      expect(component.priceRangeMax).toBeNull();
      expect(chipTexts()).toEqual(['Charlottenburg']);
    });

    it('resets every filter and the preset', () => {
      reset();

      expect(component.selectedDistricts).toEqual([]);
      expect(component.selectedRooms).toEqual([]);
      expect(component.priceRangeMin).toBeNull();
      expect(component.priceRangeMax).toBeNull();
      expect(presets.service.selectPreset).toHaveBeenCalledWith(null);
      expect(component.filteredOffers.length).toBe(2);
      expect(chipTexts()).toEqual([]);
    });
  });

  describe('Reloading', () => {
    it('loads the offers again on each reload request (refresh button, finished scrape)', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();
      const initialLoads = flatsService.getCurrentOffers.calls.count();

      reload$.next();
      reload$.next();

      expect(flatsService.getCurrentOffers.calls.count()).toBe(initialLoads + 2);
    });

    it('stops listening for reload requests when destroyed', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();

      fixture.destroy();

      expect(reload$.observed).toBe(false);
    });
  });

  describe('Empty states', () => {
    const emptyState = (): HTMLElement | null => fixture.nativeElement.querySelector('app-empty-state');
    const heading = (): string | undefined => emptyState()?.querySelector('h2')!.textContent!.trim();
    const clickReset = () => {
      (emptyState()!.querySelector('button') as HTMLButtonElement).click();
      fixture.detectChanges();
    };

    it('offers to start a scrape when there are no offers yet', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult({ success: true, data: [], message: 'Success' }));
      fixture.detectChanges();

      expect(heading()).toBe('No current offers yet');
      expect(emptyState()!.querySelector('app-start-scrape-button')).not.toBeNull();
    });

    it('offers to reset the filters when they match nothing, and resets them', () => {
      flatsService.getCurrentOffers.and.returnValue(apiResult(mockOffersResponse));
      fixture.detectChanges();
      component.priceRangeMax = 100;
      component.applyFilters();
      fixture.detectChanges();

      expect(heading()).toBe('No offers match the filters');
      clickReset();

      expect(component.priceRangeMax).toBeNull();
      expect(component.filteredOffers.length).toBe(2);
      expect(emptyState()).toBeNull();
    });

    it('shows only the error when loading failed', () => {
      flatsService.getCurrentOffers.and.returnValue(throwError(() => new Error('Network error')));
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('.alert-danger')).not.toBeNull();
      expect(emptyState()).toBeNull();
    });
  });
});
