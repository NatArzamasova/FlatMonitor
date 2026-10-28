import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { NEVER, of, throwError } from 'rxjs';
import { HistoryTabComponent } from './history-tab.component';
import { HistoryChartComponent } from './history-chart/history-chart.component';
import { FlatsService } from '../../../../core/services/flats.service';
import { apiResult } from '../../../../core/testing/api-response.testing';
import { ApiResponse } from '../../../../core/models/api-response.model';
import { FlatHistory } from '../../../../core/models/flat-history.model';

describe('HistoryTabComponent', () => {
  let component: HistoryTabComponent;
  let fixture: ComponentFixture<HistoryTabComponent>;
  let flatsService: jasmine.SpyObj<FlatsService>;

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
      expect(component.loading).toBe(false);
      expect(component.error).toBeNull();
      expect(component.groupBy).toBe('district');
    });

    it('should load history data on init', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));

      fixture.detectChanges();

      expect(flatsService.getHistory).toHaveBeenCalled();
      expect(component.historyData.length).toBe(3);
    });
  });

  describe('loadHistory', () => {
    it('should load and display history data', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));

      component.loadHistory();

      expect(component.loading).toBe(false);
      expect(component.error).toBeNull();
      expect(component.historyData.length).toBe(3);
    });

    it('should handle API response failure', () => {
      const failureResponse: ApiResponse<FlatHistory[]> = {
        success: false,
        message: 'Failed to load history'
      };
      flatsService.getHistory.and.returnValue(apiResult(failureResponse));

      component.loadHistory();

      expect(component.error).toBe('Failed to load history');
      expect(component.loading).toBe(false);
      expect(component.historyData).toEqual([]);
    });

    it('should handle HTTP errors', () => {
      const error = new Error('Network error');
      flatsService.getHistory.and.returnValue(throwError(() => error));

      component.loadHistory();

      expect(component.error).toContain('Error loading history');
      expect(component.loading).toBe(false);
    });

  });

  describe('History Grouping', () => {
    beforeEach(() => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      component.loadHistory();
    });

    it('should group by district by default', () => {
      expect(component.groupBy).toBe('district');
    });

    it('should allow grouping by rooms', () => {
      component.groupBy = 'rooms';
      component.onGroupByChange();

      expect(component.groupBy).toBe('rooms');
      expect(component.chartData.length).toBeGreaterThan(0);
    });

    it('should update chart data when group by changes', () => {
      const initialChartDataLength = component.chartData.length;

      component.groupBy = 'rooms';
      component.onGroupByChange();

      expect(component.chartData.length).toBeGreaterThanOrEqual(initialChartDataLength);
    });
  });

  describe('History Chart Data', () => {
    beforeEach(() => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      component.loadHistory();
    });

    it('should include all series in the chart data', () => {
      component.groupBy = 'district';

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
      component.groupBy = 'district';
      (component as any).updateChartData();

      expect(component.chartData).toEqual([
        { date: '2024-01-15', price: 4793, label: 'Mitte' }
      ]);
    });

    it('should calculate room history as a flat-count-weighted average across districts', () => {
      component.historyData = [
        { scrapingDate: '2024-01-15', district: 'Mitte', roomsCount: 2, avgPricePerMeter: 4000, count: 5 },
        { scrapingDate: '2024-01-15', district: 'Charlottenburg', roomsCount: 2, avgPricePerMeter: 8756, count: 1 }
      ];
      component.groupBy = 'rooms';
      (component as any).updateChartData();

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
      component.groupBy = 'district';
      (component as any).updateChartData();

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
      component.groupBy = 'rooms';
      (component as any).updateChartData();

      expect(component.latestAverageData).toEqual([
        { label: '2 rooms', price: 4000 },
        { label: '4 rooms', price: 4000 }
      ]);
    });
  });

  describe('Latest averages table', () => {
    it('renders the latest date and its averages sorted by price', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();

      const heading = fixture.nativeElement.querySelector('h2') as HTMLElement;
      const rows = Array.from(fixture.nativeElement.querySelectorAll('table tbody tr')) as HTMLElement[];
      expect(heading.textContent).toContain('Latest averages (2024-01-16)');
      expect(rows.map(row => Array.from(row.querySelectorAll('td')).map(cell => cell.textContent!.trim())))
        .toEqual([['Mitte', '€13']]);
    });

    it('computes the latest averages when the data changes, not on every change detection', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();
      const latest = component.latestAverageData;

      fixture.detectChanges();

      expect(component.latestAverageData).toBe(latest);

      component.groupBy = 'rooms';
      component.onGroupByChange();

      expect(component.latestAverageData).not.toBe(latest);
      expect(component.latestAverageData).toEqual([{ label: '2 rooms', price: 13 }]);
    });
  });

  describe('Spinner and alerts (DOM)', () => {
    it('shows a loading spinner while history is loading', () => {
      flatsService.getHistory.and.returnValue(NEVER);
      fixture.detectChanges();

      const spinner = fixture.nativeElement.querySelector('.spinner-border.text-primary') as HTMLElement;
      expect(spinner).toBeTruthy();
      expect(spinner.textContent).toContain('Loading...');
    });

    it('shows load errors in an alert that can be dismissed', () => {
      flatsService.getHistory.and.returnValue(throwError(() => new Error('Network error')));
      fixture.detectChanges();

      const alert = fixture.nativeElement.querySelector('.alert-danger') as HTMLElement;
      expect(alert.textContent).toContain('Error loading history: Network error');

      (alert.querySelector('.btn-close') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(component.error).toBeNull();
      expect(fixture.nativeElement.querySelector('.alert-danger')).toBeNull();
    });
  });

  describe('Chart', () => {
    const chartComponent = (): HistoryChartComponent | undefined =>
      fixture.debugElement.query(By.directive(HistoryChartComponent))?.componentInstance;

    it('passes the grouped series and title to the chart as soon as history data is shown', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));

      fixture.detectChanges();

      expect(chartComponent()!.points).toBe(component.chartData);
      expect(chartComponent()!.title).toBe('Price per m² Trend (by District)');
      expect(chartComponent()!.chart).toBeDefined();
    });

    it('passes new series and title when the grouping changes', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();

      component.groupBy = 'rooms';
      component.onGroupByChange();
      fixture.detectChanges();

      expect(chartComponent()!.title).toBe('Price per m² Trend (by Room Number)');
      expect(chartComponent()!.points.map(point => point.label)).toContain('2 rooms');
    });

    it('does not show a chart when there is no history data', () => {
      flatsService.getHistory.and.returnValue(apiResult({ success: true, data: [], message: 'Success' }));

      fixture.detectChanges();

      expect(chartComponent()).toBeUndefined();
    });
  });
});
