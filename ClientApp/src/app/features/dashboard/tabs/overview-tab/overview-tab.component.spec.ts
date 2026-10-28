import { ComponentFixture, TestBed } from '@angular/core/testing';
import { APP_BASE_HREF } from '@angular/common';
import { provideRouter } from '@angular/router';
import { NEVER, throwError, Subject } from 'rxjs';
import { OverviewTabComponent } from './overview-tab.component';
import { FlatsService } from '../../../../core/services/flats.service';
import { ScrapeStatusService } from '../../../../core/services/scrape-status.service';
import { apiResult } from '../../../../core/testing/api-response.testing';
import { ApiResponse } from '../../../../core/models/api-response.model';
import { FlatAggregation } from '../../../../core/models/flat-aggregation.model';
import { FilterPresetsService } from '../../../../core/services/filter-presets.service';
import {
  createFilterPresetsServiceStub,
  filterPreset,
  FilterPresetsServiceStub
} from '../../../../core/testing/filter-presets.testing';

describe('OverviewTabComponent', () => {
  let component: OverviewTabComponent;
  let fixture: ComponentFixture<OverviewTabComponent>;
  let flatsService: jasmine.SpyObj<FlatsService>;
  let scrapeFinished$: Subject<void>;
  let presets: FilterPresetsServiceStub;

  const mockAggregatedResponse: ApiResponse<FlatAggregation[]> = {
    success: true,
    data: [
      {
        postalCode: '10115',
        district: 'Mitte',
        roomsCount: 2,
        count: 5,
        avgPricePerMeter: 12.50
      },
      {
        postalCode: '10119',
        district: 'Prenzlauer Berg',
        roomsCount: 3,
        count: 8,
        avgPricePerMeter: 11.20
      }
    ],
    message: 'Success'
  };

  beforeEach(async () => {
    const flatsServiceSpy = jasmine.createSpyObj('FlatsService', ['getAggregated']);
    scrapeFinished$ = new Subject<void>();
    presets = createFilterPresetsServiceStub();
    const scrapeStatusStub = { status$: NEVER, scrapeFinished$ };

    await TestBed.configureTestingModule({
      imports: [OverviewTabComponent],
      providers: [
        { provide: FlatsService, useValue: flatsServiceSpy },
        { provide: ScrapeStatusService, useValue: scrapeStatusStub },
        { provide: FilterPresetsService, useValue: presets.service },
        provideRouter([]),
        { provide: APP_BASE_HREF, useValue: '/' }
      ]
    }).compileComponents();

    flatsService = TestBed.inject(FlatsService) as jasmine.SpyObj<FlatsService>;
    fixture = TestBed.createComponent(OverviewTabComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    if (fixture) {
      fixture.destroy();
    }
  });

  describe('Initialization', () => {
    it('should create the component', () => {
      expect(component).toBeTruthy();
    });

    it('should initialize with default values', () => {
      expect(component.aggregatedData).toEqual([]);
      expect(component.loading).toBe(false);
      expect(component.error).toBeNull();
      expect(component.lastUpdated).toBeNull();
    });

    it('should load aggregated data on init', () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));

      fixture.detectChanges();

      expect(flatsService.getAggregated).toHaveBeenCalled();
      expect(component.aggregatedData).toEqual(mockAggregatedResponse.data!);
      expect(component.lastUpdated).toBeTruthy();
    });
  });

  describe('loadAggregatedData', () => {
    it('should load and display aggregated data', () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));

      component.loadAggregatedData();

      expect(component.loading).toBe(false);
      expect(component.error).toBeNull();
      expect(component.aggregatedData.length).toBe(2);
      expect(component.aggregatedData[0].postalCode).toBe('10115');
      expect(component.lastUpdated).toBeTruthy();
    });

    it('should set loading flag after data fetch', () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));

      component.loadAggregatedData();

      expect(component.loading).toBe(false);
      expect(component.aggregatedData).toEqual(mockAggregatedResponse.data!);
    });

    it('should handle API response failure', () => {
      const failureResponse: ApiResponse<FlatAggregation[]> = {
        success: false,
        message: 'Database error',
      };
      flatsService.getAggregated.and.returnValue(apiResult(failureResponse));

      component.loadAggregatedData();

      expect(component.error).toBe('Database error');
      expect(component.loading).toBe(false);
      expect(component.aggregatedData).toEqual([]);
    });

    it('should handle missing data in response', () => {
      const responseWithoutData: ApiResponse<FlatAggregation[]> = {
        success: true,
        message: 'Success'
      };
      flatsService.getAggregated.and.returnValue(apiResult(responseWithoutData));

      component.loadAggregatedData();

      expect(component.loading).toBe(false);
      expect(component.aggregatedData).toEqual([]);
    });

    it('should handle HTTP errors', () => {
      const error = new Error('Network error');
      flatsService.getAggregated.and.returnValue(throwError(() => error));

      component.loadAggregatedData();

      expect(component.error).toContain('Error loading data');
      expect(component.loading).toBe(false);
    });
  });

  describe('Filter controls, spinner and alerts (DOM)', () => {
    const chooseOption = async (selectId: string, optionText: string) => {
      const select = fixture.nativeElement.querySelector(`#${selectId}`) as HTMLSelectElement;
      const option = Array.from(select.options).find(o => o.textContent!.trim() === optionText)!;
      option.selected = true;
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      await fixture.whenStable();
    };

    const render = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };

    it('filters by district through the district dropdown', async () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));
      await render();

      const label = fixture.nativeElement.querySelector('label[for="districtSelect"]') as HTMLLabelElement;
      expect(label.textContent!.trim()).toBe('Filter District:');
      await chooseOption('districtSelect', 'Mitte');

      expect(component.selectedDistricts).toEqual(['Mitte']);
      expect(component.aggregatedData.map(row => row.district)).toEqual(['Mitte']);
    });

    it('filters by rooms through the rooms dropdown, keeping numeric values', async () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));
      await render();

      await chooseOption('roomsSelect', '3');

      expect(component.selectedRooms).toEqual([3]);
      expect(component.aggregatedData.map(row => row.roomsCount)).toEqual([3]);
    });

    it('shows average prices rounded to whole euros', () => {
      flatsService.getAggregated.and.returnValue(apiResult({
        success: true,
        data: [
          { district: 'Aachen', roomsCount: 1, count: 1, avgPricePerMeter: 12.49 },
          { district: 'Bonn', roomsCount: 1, count: 1, avgPricePerMeter: 12.5 },
          { district: 'Celle', roomsCount: 1, count: 1, avgPricePerMeter: 4793.6 }
        ],
        message: 'Success'
      }));
      fixture.detectChanges();

      const prices = Array.from(fixture.nativeElement.querySelectorAll('tbody tr td:nth-child(4)'))
        .map(cell => (cell as HTMLElement).textContent!.trim());
      expect(prices).toEqual(['€12', '€13', '€4,794']);
    });

    it('lists empty-free, distinct and sorted options, keeping zero rooms', async () => {
      flatsService.getAggregated.and.returnValue(apiResult({
        success: true,
        data: [
          { district: 'Weststadt', roomsCount: 10, count: 1, avgPricePerMeter: 1 },
          { district: '', roomsCount: 2, count: 1, avgPricePerMeter: 1 },
          { district: 'Mitte', roomsCount: 0, count: 1, avgPricePerMeter: 1 },
          { district: 'Weststadt', roomsCount: undefined, count: 1, avgPricePerMeter: 1 },
          { district: undefined, roomsCount: 2.54, count: 1, avgPricePerMeter: 1 }
        ],
        message: 'Success'
      }));
      component.loadAggregatedData();

      expect(component.availableDistricts).toEqual(['Mitte', 'Weststadt']);
      expect(component.availableRooms).toEqual([0, 2, 2.5, 10]);
    });

    it('lists the available options in the dropdowns', async () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));
      await render();

      const optionTexts = (id: string) =>
        (Array.from((fixture.nativeElement.querySelector(`#${id}`) as HTMLSelectElement).options) as HTMLOptionElement[])
          .filter(option => !option.hidden)
          .map(option => option.textContent!.trim());
      expect(optionTexts('districtSelect')).toEqual(['Mitte', 'Prenzlauer Berg']);
      expect(optionTexts('roomsSelect')).toEqual(['2', '3']);
    });

    it('shows a loading spinner while data is loading', () => {
      flatsService.getAggregated.and.returnValue(NEVER);
      fixture.detectChanges();

      const spinner = fixture.nativeElement.querySelector('.spinner-border.text-primary') as HTMLElement;
      expect(spinner).toBeTruthy();
      expect(spinner.textContent).toContain('Loading...');
    });

    it('shows load errors in an alert that can be dismissed', () => {
      flatsService.getAggregated.and.returnValue(throwError(() => new Error('Network error')));
      fixture.detectChanges();

      const alert = fixture.nativeElement.querySelector('.alert-danger') as HTMLElement;
      expect(alert.textContent).toContain('Error loading data: Network error');

      (alert.querySelector('.btn-close') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(component.error).toBeNull();
      expect(fixture.nativeElement.querySelector('.alert-danger')).toBeNull();
    });
  });

  describe('Scrape lifecycle', () => {
    it('reloads aggregated data each time a scrape finishes', () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));
      fixture.detectChanges();
      const initialLoadCount = flatsService.getAggregated.calls.count();

      scrapeFinished$.next();
      scrapeFinished$.next();

      expect(flatsService.getAggregated.calls.count()).toBe(initialLoadCount + 2);
    });

    it('stops listening for finished scrapes when destroyed', () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));
      fixture.detectChanges();

      fixture.destroy();

      expect(scrapeFinished$.observed).toBe(false);
    });

    it('renders the scraping status with the data timestamp and Refresh button in the same row', () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));
      fixture.detectChanges();

      const status = fixture.nativeElement.querySelector('app-scraping-status') as HTMLElement;
      expect(status).toBeTruthy();
      expect(status.querySelector('.text-muted')?.textContent).toContain('Data last updated:');
      const buttons = Array.from(status.querySelectorAll('.col-auto button')) as HTMLElement[];
      expect(buttons.map(button => button.textContent!.trim())).toEqual(['Refresh Data', 'Manual Scrape']);
    });
  });

  describe('refresh', () => {
    it('should reload aggregated data', () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));

      const initialCallCount = flatsService.getAggregated.calls.count();

      component.refresh();

      expect(flatsService.getAggregated.calls.count()).toBe(initialCallCount + 1);
    });
  });

  describe('Error handling edge cases', () => {
    it('should handle response with success=true but no data field', () => {
      const responseWithoutData: ApiResponse<FlatAggregation[]> = {
        success: true,
        message: 'No data available'
      };
      flatsService.getAggregated.and.returnValue(apiResult(responseWithoutData));

      component.loadAggregatedData();

      expect(component.aggregatedData).toEqual([]);
      expect(component.error).toBe('No data available');
    });

    it('should display default error message when response lacks message', () => {
      const responseWithoutMessage: ApiResponse<FlatAggregation[]> = {
        success: false,
        message: ''
      };
      flatsService.getAggregated.and.returnValue(apiResult(responseWithoutMessage));

      component.loadAggregatedData();

      expect(component.error).toBe('Failed to load data');
    });
  });

  describe('Filtering and Grouping', () => {
    const multiRoomData: ApiResponse<FlatAggregation[]> = {
      success: true,
      data: [
        { district: 'Mitte', roomsCount: 1, count: 3, avgPricePerMeter: 15.00, postalCode: '10115' },
        { district: 'Mitte', roomsCount: 2, count: 5, avgPricePerMeter: 12.50, postalCode: '10115' },
        { district: 'Mitte', roomsCount: 3, count: 2, avgPricePerMeter: 10.00, postalCode: '10115' },
        { district: 'Prenzlauer Berg', roomsCount: 1, count: 4, avgPricePerMeter: 14.00, postalCode: '10119' },
        { district: 'Prenzlauer Berg', roomsCount: 2, count: 6, avgPricePerMeter: 11.20, postalCode: '10119' }
      ],
      message: 'Success'
    };

    it('should populate available districts and rooms from data', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));

      component.loadAggregatedData();

      expect(component.availableDistricts).toEqual(['Mitte', 'Prenzlauer Berg']);
      expect(component.availableRooms).toEqual([1, 2, 3]);
    });

    it('should normalize fractional room counts to one decimal in filter options and filtering', () => {
      flatsService.getAggregated.and.returnValue(apiResult({
        success: true,
        data: [
          { district: 'Mitte', roomsCount: 2, count: 3, avgPricePerMeter: 10 },
          { district: 'Mitte', roomsCount: 2.54, count: 5, avgPricePerMeter: 12 }
        ],
        message: 'Success'
      }));
      component.loadAggregatedData();

      expect(component.availableRooms).toEqual([2, 2.5]);

      component.selectedRooms = [2.5];
      component.applyFiltersAndGrouping();

      expect(component.aggregatedData.map(item => item.count)).toEqual([5]);
    });

    it('should render fractional room counts with one decimal', () => {
      flatsService.getAggregated.and.returnValue(apiResult({
        success: true,
        data: [{ district: 'Mitte', roomsCount: 2.5, count: 5, avgPricePerMeter: 12 }],
        message: 'Success'
      }));
      fixture.detectChanges();

      const roomCell = fixture.nativeElement.querySelector('tbody tr td:nth-child(2)') as HTMLElement;
      expect(roomCell.textContent?.trim()).toBe('2.5');
    });

    it('should filter by single district', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.selectedDistricts = ['Mitte'];
      component.applyFiltersAndGrouping();

      expect(component.aggregatedData.length).toBe(3);
      expect(component.aggregatedData.every(item => item.district === 'Mitte')).toBe(true);
    });

    it('should filter by multiple districts', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.selectedDistricts = ['Mitte', 'Prenzlauer Berg'];
      component.applyFiltersAndGrouping();

      expect(component.aggregatedData.length).toBe(5);
      expect(component.aggregatedData.every(item =>
        item.district === 'Mitte' || item.district === 'Prenzlauer Berg'
      )).toBe(true);
    });

    it('should filter by single room count', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.selectedRooms = [2];
      component.applyFiltersAndGrouping();

      expect(component.aggregatedData.length).toBe(2);
      expect(component.aggregatedData.every(item => item.roomsCount === 2)).toBe(true);
    });

    it('should filter by multiple room counts', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.selectedRooms = [1, 2];
      component.applyFiltersAndGrouping();

      expect(component.aggregatedData.length).toBe(4);
      expect(component.aggregatedData.every(item =>
        item.roomsCount === 1 || item.roomsCount === 2
      )).toBe(true);
    });

    it('should filter by both districts and rooms', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.selectedDistricts = ['Mitte'];
      component.selectedRooms = [2];
      component.applyFiltersAndGrouping();

      expect(component.aggregatedData.length).toBe(1);
      expect(component.aggregatedData[0].district).toBe('Mitte');
      expect(component.aggregatedData[0].roomsCount).toBe(2);
    });

    it('should aggregate by district only when groupByRooms is false', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.groupByDistrict = true;
      component.groupByRooms = false;
      component.applyFiltersAndGrouping();

      expect(component.aggregatedData.length).toBe(2);
      expect(component.aggregatedData[0].district).toBe('Mitte');
      expect(component.aggregatedData[0].roomsCount).toBeUndefined();
      expect(component.aggregatedData[0].count).toBe(10);
    });

    it('should aggregate by rooms only when groupByDistrict is false', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.groupByDistrict = false;
      component.groupByRooms = true;
      component.applyFiltersAndGrouping();

      expect(component.aggregatedData.length).toBe(3);
      expect(component.aggregatedData[0].roomsCount).toBe(1);
      expect(component.aggregatedData[0].district).toBeUndefined();
      expect(component.aggregatedData[0].count).toBe(7);
    });

    it('should aggregate all data when both grouping options are disabled', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.groupByDistrict = false;
      component.groupByRooms = false;
      component.applyFiltersAndGrouping();

      expect(component.aggregatedData.length).toBe(1);
      expect(component.aggregatedData[0].district).toBeUndefined();
      expect(component.aggregatedData[0].roomsCount).toBeUndefined();
      expect(component.aggregatedData[0].count).toBe(20);
    });

    it('should calculate weighted average price correctly when aggregating by district', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.groupByDistrict = true;
      component.groupByRooms = false;
      component.applyFiltersAndGrouping();

      const mitteRow = component.aggregatedData.find(item => item.district === 'Mitte');
      expect(mitteRow?.avgPricePerMeter).toBeCloseTo(12.75, 2);
    });

    it('should calculate weighted average price correctly when aggregating by rooms', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.groupByDistrict = false;
      component.groupByRooms = true;
      component.applyFiltersAndGrouping();

      const room1Row = component.aggregatedData.find(item => item.roomsCount === 1);
      expect(room1Row?.avgPricePerMeter).toBeCloseTo(14.43, 2);
    });

    it('should calculate weighted average price correctly when aggregating all', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.groupByDistrict = false;
      component.groupByRooms = false;
      component.applyFiltersAndGrouping();

      expect(component.aggregatedData[0].avgPricePerMeter).toBeCloseTo(12.535, 2);
    });

    it('should not produce NaN averages when aggregating rows with a zero count', () => {
      flatsService.getAggregated.and.returnValue(apiResult({
        success: true,
        data: [
          { district: 'Mitte', roomsCount: 1, count: 0, avgPricePerMeter: 99 },
          { district: 'Mitte', roomsCount: 2, count: 5, avgPricePerMeter: 10 },
          { district: 'Weststadt', roomsCount: 1, count: 0, avgPricePerMeter: 50 }
        ],
        message: 'Success'
      }));
      component.loadAggregatedData();

      component.groupByDistrict = true;
      component.groupByRooms = false;
      component.applyFiltersAndGrouping();

      expect(component.aggregatedData.map(item => [item.district, item.count, item.avgPricePerMeter]))
        .toEqual([['Mitte', 5, 10], ['Weststadt', 0, 0]]);
    });

    describe('exact grouped rows', () => {
      const detailedRows: FlatAggregation[] = [
        { district: 'Weststadt', roomsCount: 3, count: 2, avgPricePerMeter: 10, postalCode: '76135', availability: 'sofort' },
        { district: 'Mitte', roomsCount: 2, count: 1, avgPricePerMeter: 20, postalCode: '76133', availability: 'ab 01.05.' },
        { district: 'Weststadt', roomsCount: 2, count: 3, avgPricePerMeter: 30, postalCode: '76137', availability: 'nach Vereinbarung' },
        { district: undefined, roomsCount: undefined, count: 4, avgPricePerMeter: 40, postalCode: '76131', availability: undefined }
      ];

      const group = (groupByDistrict: boolean, groupByRooms: boolean, rows = detailedRows) => {
        flatsService.getAggregated.and.returnValue(apiResult({ success: true, data: rows, message: 'Success' }));
        component.loadAggregatedData();
        component.groupByDistrict = groupByDistrict;
        component.groupByRooms = groupByRooms;
        component.applyFiltersAndGrouping();
        return component.aggregatedData.map(row => ({ ...row, avgPricePerMeter: Number(row.avgPricePerMeter.toFixed(6)) }));
      };

      it('keeps the raw rows when grouping by district and rooms', () => {
        expect(group(true, true)).toEqual(detailedRows);
      });

      it('groups by district, sorted by district with missing first, keeping the first row details', () => {
        expect(group(true, false)).toEqual([
          { district: undefined, roomsCount: undefined, count: 4, avgPricePerMeter: 40, postalCode: '76131', availability: undefined },
          { district: 'Mitte', roomsCount: undefined, count: 1, avgPricePerMeter: 20, postalCode: '76133', availability: 'ab 01.05.' },
          { district: 'Weststadt', roomsCount: undefined, count: 5, avgPricePerMeter: 22, postalCode: '76135', availability: 'sofort' }
        ]);
      });

      it('groups by rooms, sorted by room count with missing first, keeping the first row details', () => {
        expect(group(false, true)).toEqual([
          { district: undefined, roomsCount: undefined, count: 4, avgPricePerMeter: 40, postalCode: '76131', availability: undefined },
          { district: undefined, roomsCount: 2, count: 4, avgPricePerMeter: 27.5, postalCode: '76133', availability: 'ab 01.05.' },
          { district: undefined, roomsCount: 3, count: 2, avgPricePerMeter: 10, postalCode: '76135', availability: 'sofort' }
        ]);
      });

      it('merges everything into one row without details when no grouping is selected', () => {
        expect(group(false, false)).toEqual([
          { district: undefined, roomsCount: undefined, count: 10, avgPricePerMeter: 29, postalCode: undefined, availability: undefined }
        ]);
      });

      it('returns no rows when no grouping is selected and there is no data', () => {
        expect(group(false, false, [])).toEqual([]);
      });

      it('does not modify the loaded rows while grouping', () => {
        const rows = detailedRows.map(row => ({ ...row }));

        group(true, false, rows);
        group(false, true, rows);
        group(false, false, rows);

        expect(rows).toEqual(detailedRows);
      });
    });

    it('should apply filter and then aggregate correctly', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.selectedDistricts = ['Mitte'];
      component.groupByDistrict = true;
      component.groupByRooms = false;
      component.applyFiltersAndGrouping();

      expect(component.aggregatedData.length).toBe(1);
      expect(component.aggregatedData[0].count).toBe(10);
      expect(component.aggregatedData[0].avgPricePerMeter).toBeCloseTo(12.75, 2);
    });
  });

  describe('Table sorting', () => {
    beforeEach(() => {
      component.aggregatedData = [
        { district: 'Zürich', roomsCount: 3, count: 2, avgPricePerMeter: 20 },
        { district: 'Aachen', roomsCount: 1, count: 8, avgPricePerMeter: 10 }
      ];
    });

    it('sorts every displayed column ascending and toggles the active column descending', () => {
      const cases = [
        { column: 'district' as const, firstValue: 'Aachen' },
        { column: 'roomsCount' as const, firstValue: 1 },
        { column: 'count' as const, firstValue: 2 },
        { column: 'avgPricePerMeter' as const, firstValue: 10 }
      ];

      for (const { column, firstValue } of cases) {
        component.onSort(column);
        expect(component.aggregatedData[0][column]).toBe(firstValue);
        component.onSort(column);
        expect(component.aggregatedData[0][column]).not.toBe(firstValue);
      }
    });

    it('renders a sortable control for every table column', () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));
      component.aggregatedData = mockAggregatedResponse.data!;
      fixture.detectChanges();

      const buttons = fixture.nativeElement.querySelectorAll('thead button[aria-label^="Sort by"]') as NodeListOf<HTMLButtonElement>;
      expect(buttons.length).toBe(4);
      buttons[2].click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('thead th:nth-child(3)').getAttribute('aria-sort')).toBe('ascending');
      expect(component.aggregatedData[0].count).toBe(5);
    });
  });

  describe('Current offers navigation', () => {
    it('opens a new offers tab with the dimensions represented by the row', () => {
      const openSpy = spyOn(window, 'open');
      component.groupByDistrict = true;
      component.groupByRooms = true;

      component.openOffersForRow({
        district: 'Mitte',
        roomsCount: 2,
        count: 5,
        avgPricePerMeter: 12.5
      });

      expect(openSpy).toHaveBeenCalledWith(
        '/offers?district=Mitte&rooms=2',
        '_blank',
        'noopener,noreferrer'
      );
    });

    it('passes fractional room counts to the offers tab rounded to one decimal', () => {
      const openSpy = spyOn(window, 'open');
      component.groupByDistrict = false;
      component.groupByRooms = true;

      component.openOffersForRow({ roomsCount: 2.54, count: 5, avgPricePerMeter: 12.5 });

      expect(openSpy).toHaveBeenCalledWith(
        '/offers?rooms=2.5',
        '_blank',
        'noopener,noreferrer'
      );
    });

    it('does not set a room filter when grouping by rooms is disabled', () => {
      const openSpy = spyOn(window, 'open');
      component.groupByDistrict = true;
      component.groupByRooms = false;

      component.openOffersForRow({
        district: 'Mitte',
        roomsCount: undefined,
        count: 10,
        avgPricePerMeter: 12.5
      });

      expect(openSpy).toHaveBeenCalledWith(
        '/offers?district=Mitte',
        '_blank',
        'noopener,noreferrer'
      );
    });
  });

  describe('Filter presets', () => {
    const mitte = filterPreset(1, 'Center', ['Mitte']);

    it('applies the active preset when the tab opens', () => {
      presets.activePreset$.next(mitte);
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));

      fixture.detectChanges();

      expect(component.selectedDistricts).toEqual(['Mitte']);
      expect(component.aggregatedData.map(row => row.district)).toEqual(['Mitte']);
    });

    it('shows all rows when there is no active preset', () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));

      fixture.detectChanges();

      expect(component.selectedDistricts).toEqual([]);
      expect(component.aggregatedData.length).toBe(2);
    });

    it('applies a preset chosen later', () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));
      fixture.detectChanges();

      presets.activePreset$.next(filterPreset(2, 'North', ['Prenzlauer Berg']));

      expect(component.aggregatedData.map(row => row.district)).toEqual(['Prenzlauer Berg']);
    });

    it('keeps the current selection when the active preset becomes null', () => {
      presets.activePreset$.next(mitte);
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));
      fixture.detectChanges();

      presets.activePreset$.next(null);

      expect(component.selectedDistricts).toEqual(['Mitte']);
    });

    it('keeps a manually changed selection when the data reloads after a scrape', () => {
      presets.activePreset$.next(mitte);
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));
      fixture.detectChanges();
      component.selectedDistricts = ['Prenzlauer Berg'];
      component.applyFiltersAndGrouping();

      scrapeFinished$.next();

      expect(flatsService.getAggregated).toHaveBeenCalledTimes(2);
      expect(component.selectedDistricts).toEqual(['Prenzlauer Berg']);
      expect(component.aggregatedData.map(row => row.district)).toEqual(['Prenzlauer Berg']);
    });

    it('offers preset districts that have no data, so they can be deselected', () => {
      presets.activePreset$.next(filterPreset(3, 'Far', ['Durlach']));
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));

      fixture.detectChanges();

      expect(component.availableDistricts).toEqual(['Durlach', 'Mitte', 'Prenzlauer Berg']);
    });

    it('renders the preset bar', () => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));

      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('app-filter-preset-bar select').id).toBe('presetSelect');
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
          ? `${child.tagName.toLowerCase()}#${child.querySelector('select, input')?.id ?? ''}`
          : child.querySelector('select, input')?.id ?? child.tagName.toLowerCase());

    beforeEach(() => {
      flatsService.getAggregated.and.returnValue(apiResult(mockAggregatedResponse));
      fixture.detectChanges();
    });

    it('puts districts + preset, other filters and grouping on separate rows', () => {
      expect(rowNames()).toEqual(['districts', 'other', 'grouping']);
      expect(rowItems('districts')).toEqual([
        'app-multi-select-filter#districtSelect',
        'app-filter-preset-bar#presetSelect'
      ]);
      expect(rowItems('other')).toEqual(['app-multi-select-filter#roomsSelect']);
      expect(rowItems('grouping')).toEqual(['groupByDistrictToggle', 'groupByRoomsToggle']);
    });
  });
});
