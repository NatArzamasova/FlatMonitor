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

describe('OverviewTabComponent', () => {
  let component: OverviewTabComponent;
  let fixture: ComponentFixture<OverviewTabComponent>;
  let flatsService: jasmine.SpyObj<FlatsService>;
  let scrapeFinished$: Subject<void>;

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
    const scrapeStatusStub = { status$: NEVER, scrapeFinished$ };

    await TestBed.configureTestingModule({
      imports: [OverviewTabComponent],
      providers: [
        { provide: FlatsService, useValue: flatsServiceSpy },
        { provide: ScrapeStatusService, useValue: scrapeStatusStub },
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

  describe('Cleanup', () => {
    it('should complete destroy subject', () => {
      const destroySubject = (component as any).destroy$ as Subject<void>;
      const completeSpy = spyOn(destroySubject, 'complete');

      component.ngOnDestroy();

      expect(completeSpy).toHaveBeenCalled();
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

      component.selectedRoomCounts = [2.5];
      component.onFiltersChange();

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
      component.onFiltersChange();

      expect(component.aggregatedData.length).toBe(3);
      expect(component.aggregatedData.every(item => item.district === 'Mitte')).toBe(true);
    });

    it('should filter by multiple districts', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.selectedDistricts = ['Mitte', 'Prenzlauer Berg'];
      component.onFiltersChange();

      expect(component.aggregatedData.length).toBe(5);
      expect(component.aggregatedData.every(item =>
        item.district === 'Mitte' || item.district === 'Prenzlauer Berg'
      )).toBe(true);
    });

    it('should filter by single room count', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.selectedRoomCounts = [2];
      component.onFiltersChange();

      expect(component.aggregatedData.length).toBe(2);
      expect(component.aggregatedData.every(item => item.roomsCount === 2)).toBe(true);
    });

    it('should filter by multiple room counts', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.selectedRoomCounts = [1, 2];
      component.onFiltersChange();

      expect(component.aggregatedData.length).toBe(4);
      expect(component.aggregatedData.every(item =>
        item.roomsCount === 1 || item.roomsCount === 2
      )).toBe(true);
    });

    it('should filter by both districts and rooms', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.selectedDistricts = ['Mitte'];
      component.selectedRoomCounts = [2];
      component.onFiltersChange();

      expect(component.aggregatedData.length).toBe(1);
      expect(component.aggregatedData[0].district).toBe('Mitte');
      expect(component.aggregatedData[0].roomsCount).toBe(2);
    });

    it('should aggregate by district only when groupByRooms is false', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.groupByDistrict = true;
      component.groupByRooms = false;
      component.onGroupingToggle();

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
      component.onGroupingToggle();

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
      component.onGroupingToggle();

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
      component.onGroupingToggle();

      const mitteRow = component.aggregatedData.find(item => item.district === 'Mitte');
      expect(mitteRow?.avgPricePerMeter).toBeCloseTo(12.75, 2);
    });

    it('should calculate weighted average price correctly when aggregating by rooms', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.groupByDistrict = false;
      component.groupByRooms = true;
      component.onGroupingToggle();

      const room1Row = component.aggregatedData.find(item => item.roomsCount === 1);
      expect(room1Row?.avgPricePerMeter).toBeCloseTo(14.43, 2);
    });

    it('should calculate weighted average price correctly when aggregating all', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.groupByDistrict = false;
      component.groupByRooms = false;
      component.onGroupingToggle();

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
      component.onGroupingToggle();

      expect(component.aggregatedData.map(item => [item.district, item.count, item.avgPricePerMeter]))
        .toEqual([['Mitte', 5, 10], ['Weststadt', 0, 0]]);
    });

    it('should apply filter and then aggregate correctly', () => {
      flatsService.getAggregated.and.returnValue(apiResult(multiRoomData));
      component.loadAggregatedData();

      component.selectedDistricts = ['Mitte'];
      component.groupByDistrict = true;
      component.groupByRooms = false;
      component.onFiltersChange();

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
});
