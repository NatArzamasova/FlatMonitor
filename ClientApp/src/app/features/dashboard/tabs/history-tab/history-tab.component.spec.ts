import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HistoryTabComponent } from './history-tab.component';
import { FlatsService } from '../../../../core/services/flats.service';
import { ApiResponse } from '../../../../core/models/api-response.model';
import { FlatHistory } from '../../../../core/models/flat-history.model';

describe('HistoryTabComponent', () => {
  let component: HistoryTabComponent;
  let fixture: ComponentFixture<HistoryTabComponent>;
  let flatsService: jasmine.SpyObj<FlatsService>;

  beforeAll(() => {
    (window as any).Chart = class {
      constructor(ctx: any, config: any) {}
      destroy() {}
    };
  });

  const mockHistoryResponse: ApiResponse<FlatHistory[]> = {
    success: true,
    data: [
      {
        scrapingDate: '2024-01-15',
        district: 'Mitte',
        roomsCount: 2,
        avgPricePerMeter: 12.50,
        count: 5
      },
      {
        scrapingDate: '2024-01-16',
        district: 'Mitte',
        roomsCount: 2,
        avgPricePerMeter: 12.75,
        count: 6
      },
      {
        scrapingDate: '2024-01-15',
        district: 'Charlottenburg',
        roomsCount: 3,
        avgPricePerMeter: 11.20,
        count: 4
      }
    ],
    message: 'Success'
  };

  beforeEach(async () => {
    const flatsServiceSpy = jasmine.createSpyObj('FlatsService', [
      'getHistory'
    ]);

    await TestBed.configureTestingModule({
      imports: [HistoryTabComponent],
      providers: [
        { provide: FlatsService, useValue: flatsServiceSpy }
      ]
    }).compileComponents();

    flatsService = TestBed.inject(FlatsService) as jasmine.SpyObj<FlatsService>;
    fixture = TestBed.createComponent(HistoryTabComponent);
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
      expect(component.historyData).toEqual([]);
      expect(component.chartData).toEqual([]);
      expect(component.loadingHistory).toBe(false);
      expect(component.error).toBeNull();
      expect(component.historyGroupBy).toBe('district');
    });

    it('should load history data on init', () => {
      flatsService.getHistory.and.returnValue(of(mockHistoryResponse));

      fixture.detectChanges();

      expect(flatsService.getHistory).toHaveBeenCalled();
      expect(component.historyData.length).toBe(3);
    });
  });

  describe('loadHistory', () => {
    it('should load and display history data', () => {
      flatsService.getHistory.and.returnValue(of(mockHistoryResponse));

      component.loadHistory();

      expect(component.loadingHistory).toBe(false);
      expect(component.error).toBeNull();
      expect(component.historyData.length).toBe(3);
    });

    it('should handle API response failure', () => {
      const failureResponse: ApiResponse<FlatHistory[]> = {
        success: false,
        message: 'Failed to load history'
      };
      flatsService.getHistory.and.returnValue(of(failureResponse));

      component.loadHistory();

      expect(component.error).toBe('Failed to load history');
      expect(component.loadingHistory).toBe(false);
      expect(component.historyData).toEqual([]);
    });

    it('should handle HTTP errors', () => {
      const error = new Error('Network error');
      flatsService.getHistory.and.returnValue(throwError(() => error));

      component.loadHistory();

      expect(component.error).toContain('Error loading history');
      expect(component.loadingHistory).toBe(false);
    });

    it('should populate available filter options', () => {
      flatsService.getHistory.and.returnValue(of(mockHistoryResponse));

      component.loadHistory();

      expect(component.availableHistoryDistricts).toContain('Mitte');
      expect(component.availableHistoryDistricts).toContain('Charlottenburg');
      expect(component.availableHistoryRooms).toContain(2);
      expect(component.availableHistoryRooms).toContain(3);
    });

    it('should select all districts by default', () => {
      flatsService.getHistory.and.returnValue(of(mockHistoryResponse));

      component.loadHistory();

      expect(component.selectedHistoryDistricts.length).toBe(2);
      expect(component.selectedHistoryDistricts).toContain('Mitte');
      expect(component.selectedHistoryDistricts).toContain('Charlottenburg');
    });

    it('should select all rooms by default', () => {
      flatsService.getHistory.and.returnValue(of(mockHistoryResponse));

      component.loadHistory();

      expect(component.selectedHistoryRooms.length).toBe(2);
      expect(component.selectedHistoryRooms).toContain(2);
      expect(component.selectedHistoryRooms).toContain(3);
    });
  });

  describe('History Grouping', () => {
    beforeEach(() => {
      flatsService.getHistory.and.returnValue(of(mockHistoryResponse));
      component.loadHistory();
    });

    it('should group by district by default', () => {
      expect(component.historyGroupBy).toBe('district');
    });

    it('should allow grouping by rooms', () => {
      component.historyGroupBy = 'rooms';
      component.onHistoryGroupByChange();

      expect(component.historyGroupBy).toBe('rooms');
      expect(component.chartData.length).toBeGreaterThan(0);
    });

    it('should update chart data when group by changes', () => {
      const initialChartDataLength = component.chartData.length;

      component.historyGroupBy = 'rooms';
      component.onHistoryGroupByChange();

      expect(component.chartData.length).toBeGreaterThanOrEqual(initialChartDataLength);
    });
  });

  describe('History Filtering', () => {
    beforeEach(() => {
      flatsService.getHistory.and.returnValue(of(mockHistoryResponse));
      component.loadHistory();
    });

    it('should toggle district filter', () => {
      component.selectedHistoryDistricts = ['Mitte', 'Charlottenburg'];
      component.toggleDistrictFilter('Mitte', false);

      expect(component.selectedHistoryDistricts).toContain('Charlottenburg');
      expect(component.selectedHistoryDistricts).not.toContain('Mitte');
    });

    it('should add district to filter when checked', () => {
      component.selectedHistoryDistricts = [];
      component.toggleDistrictFilter('Mitte', true);

      expect(component.selectedHistoryDistricts).toContain('Mitte');
    });

    it('should toggle room filter', () => {
      component.selectedHistoryRooms = [2, 3];
      component.toggleRoomFilter(2, false);

      expect(component.selectedHistoryRooms).toContain(3);
      expect(component.selectedHistoryRooms).not.toContain(2);
    });

    it('should add room to filter when checked', () => {
      component.selectedHistoryRooms = [];
      component.toggleRoomFilter(2, true);

      expect(component.selectedHistoryRooms).toContain(2);
    });

    it('should update chart data when filters change', () => {
      component.historyGroupBy = 'district';
      component.selectedHistoryDistricts = ['Mitte'];
      component.onHistoryFiltersChange();

      const mitteDataCount = component.chartData.filter(d => d.label === 'Mitte').length;
      expect(mitteDataCount).toBeGreaterThan(0);
    });
  });

  describe('Cleanup', () => {
    it('should destroy chart on component destroy', () => {
      flatsService.getHistory.and.returnValue(of(mockHistoryResponse));
      fixture.detectChanges();

      const mockChart = {
        destroy: jasmine.createSpy('destroy')
      };
      component.chart = mockChart;

      component.ngOnDestroy();

      expect(mockChart.destroy).toHaveBeenCalled();
    });
  });
});
