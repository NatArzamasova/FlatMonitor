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
      config: any;

      constructor(ctx: any, config: any) {
        this.config = config;
      }

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

  describe('History Chart Data', () => {
    beforeEach(() => {
      flatsService.getHistory.and.returnValue(of(mockHistoryResponse));
      component.loadHistory();
    });

    it('should include all series in the chart data', () => {
      component.historyGroupBy = 'district';

      const mitteDataCount = component.chartData.filter(d => d.label === 'Mitte').length;
      const charlottenburgDataCount = component.chartData.filter(d => d.label === 'Charlottenburg').length;
      expect(mitteDataCount).toBeGreaterThan(0);
      expect(charlottenburgDataCount).toBeGreaterThan(0);
    });

    it('should calculate district history as a flat-count-weighted average across room groups', () => {
      component.historyData = [
        { scrapingDate: '2024-01-15', district: 'Mitte', roomsCount: 2, avgPricePerMeter: 4000, count: 5 },
        { scrapingDate: '2024-01-15', district: 'Mitte', roomsCount: 6, avgPricePerMeter: 8756, count: 1 }
      ];
      component.historyGroupBy = 'district';
      (component as any).updateHistoryChart();

      expect(component.chartData).toEqual([
        { date: '2024-01-15', price: 4793, label: 'Mitte' }
      ]);
    });

    it('should calculate room history as a flat-count-weighted average across districts', () => {
      component.historyData = [
        { scrapingDate: '2024-01-15', district: 'Mitte', roomsCount: 2, avgPricePerMeter: 4000, count: 5 },
        { scrapingDate: '2024-01-15', district: 'Charlottenburg', roomsCount: 2, avgPricePerMeter: 8756, count: 1 }
      ];
      component.historyGroupBy = 'rooms';
      (component as any).updateHistoryChart();

      expect(component.chartData).toEqual([
        { date: '2024-01-15', price: 4793, label: '2 rooms' }
      ]);
    });

    it('shows the latest district averages sorted by price descending', () => {
      component.historyData = [
        { scrapingDate: '2024-01-15', district: 'Mitte', roomsCount: 2, avgPricePerMeter: 2000, count: 1 },
        { scrapingDate: '2024-01-16', district: 'Mitte', roomsCount: 2, avgPricePerMeter: 3000, count: 1 },
        { scrapingDate: '2024-01-16', district: 'West', roomsCount: 3, avgPricePerMeter: 5000, count: 1 },
        { scrapingDate: '2024-01-16', district: 'East', roomsCount: 3, avgPricePerMeter: 4000, count: 1 }
      ];
      component.historyGroupBy = 'district';
      (component as any).updateHistoryChart();

      expect(component.latestAverageDate).toBe('2024-01-16');
      expect(component.latestAverageData).toEqual([
        { label: 'West', price: 5000 },
        { label: 'East', price: 4000 },
        { label: 'Mitte', price: 3000 }
      ]);
    });

    it('shows the latest room averages sorted by price descending when grouped by rooms', () => {
      component.historyData = [
        { scrapingDate: '2024-01-16', district: 'Mitte', roomsCount: 2, avgPricePerMeter: 3000, count: 1 },
        { scrapingDate: '2024-01-16', district: 'West', roomsCount: 2, avgPricePerMeter: 5000, count: 1 },
        { scrapingDate: '2024-01-16', district: 'East', roomsCount: 4, avgPricePerMeter: 4000, count: 1 }
      ];
      component.historyGroupBy = 'rooms';
      (component as any).updateHistoryChart();

      expect(component.latestAverageData).toEqual([
        { label: '2 rooms', price: 4000 },
        { label: '4 rooms', price: 4000 }
      ]);
    });
  });

  describe('Chart scaling', () => {
    it('starts the price axis at the minimum plotted price', () => {
      component.chartData = [
        { date: '2024-01-15', price: 12.5, label: 'Mitte' },
        { date: '2024-01-16', price: 13.25, label: 'Mitte' }
      ];
      const canvas = document.createElement('canvas');
      spyOn(canvas, 'getContext').and.returnValue({} as CanvasRenderingContext2D);
      component.historyChart = { nativeElement: canvas } as any;

      (component as any).renderChart();

      expect(component.chart.config.options.scales.y.beginAtZero).toBe(false);
      expect(component.chart.config.options.scales.y.min).toBeLessThan(12.5);
      expect(component.chart.config.options.scales.x.ticks.callback(0)).toBe('15 Jan');
      expect(component.chart.config.options.plugins.tooltip.callbacks.label({
        dataset: { label: 'Mitte' },
        parsed: { y: 12.49 }
      })).toBe('Mitte: €12/m²');
      expect(component.chart.config.options.plugins.tooltip.callbacks.label({
        dataset: { label: 'Mitte' },
        parsed: { y: 12.5 }
      })).toBe('Mitte: €13/m²');
    });
  });

  describe('Chart rendering', () => {
    it('renders the chart as soon as history data is shown, without waiting for timers or tab events', () => {
      flatsService.getHistory.and.returnValue(of(mockHistoryResponse));

      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('canvas')).toBeTruthy();
      expect(component.chart).toBeDefined();
      expect(component.chart.config.data.labels).toEqual(['2024-01-15', '2024-01-16']);
      expect(component.chart.config.data.datasets.map((dataset: any) => dataset.label))
        .toEqual(['Mitte', 'Charlottenburg']);
    });

    it('re-renders the chart when the grouping changes', () => {
      flatsService.getHistory.and.returnValue(of(mockHistoryResponse));
      fixture.detectChanges();
      const firstChart = component.chart;
      spyOn(firstChart, 'destroy').and.callThrough();

      component.historyGroupBy = 'rooms';
      component.onHistoryGroupByChange();

      expect(firstChart.destroy).toHaveBeenCalled();
      expect(component.chart).not.toBe(firstChart);
      expect(component.chart.config.options.plugins.title.text).toContain('by Room Number');
    });

    it('does not render a chart when there is no history data', () => {
      flatsService.getHistory.and.returnValue(of({ success: true, data: [], message: 'Success' }));

      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('canvas')).toBeNull();
      expect(component.chart).toBeUndefined();
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
