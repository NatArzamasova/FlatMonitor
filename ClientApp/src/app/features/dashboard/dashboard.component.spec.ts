import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError, Subject } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { FlatsService } from '../../core/services/flats.service';
import { ApiResponse } from '../../core/models/api-response.model';
import { FlatAggregation } from '../../core/models/flat-aggregation.model';
import { ScrapeResponse } from '../../core/models/scrape-response.model';
import { ScrapingStatusResponse, ScrapingHistoryDto } from '../../core/models/scraping-status-response.model';

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let flatsService: jasmine.SpyObj<FlatsService>;

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

  const mockScrapingStatusNotRunning: ScrapingStatusResponse = {
    isScrapingInProgress: false,
    lastScrapingHistory: {
      id: 1,
      startTimeUtc: new Date('2024-01-15T10:00:00Z'),
      endTimeUtc: new Date('2024-01-15T10:15:00Z'),
      status: 'Successful',
      flatsScrapedCount: 42
    }
  };

  const mockScrapingStatusRunning: ScrapingStatusResponse = {
    isScrapingInProgress: true,
    lastScrapingHistory: {
      id: 2,
      startTimeUtc: new Date('2024-01-15T10:30:00Z'),
      status: 'Running',
      flatsScrapedCount: 25
    }
  };

  const mockScrapeResponse: ScrapeResponse = {
    success: true,
    message: 'Scraping started',
    scrapedAtUtc: new Date(),
    flatsCount: 150
  };

  beforeEach(async () => {
    const flatsServiceSpy = jasmine.createSpyObj('FlatsService', [
      'getAggregated',
      'triggerScrape',
      'getScrapeStatus',
      'cancelScrape'
    ]);

    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        { provide: FlatsService, useValue: flatsServiceSpy }
      ]
    }).compileComponents();

    flatsService = TestBed.inject(FlatsService) as jasmine.SpyObj<FlatsService>;
    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    fixture.destroy();
  });

  describe('Initialization', () => {
    it('should create the component', () => {
      expect(component).toBeTruthy();
    });

    it('should initialize with default values', () => {
      expect(component.aggregatedData).toEqual([]);
      expect(component.loading).toBe(false);
      expect(component.scraping).toBe(false);
      expect(component.error).toBeNull();
      expect(component.lastUpdated).toBeNull();
      expect(component.scrapedCount).toBe(0);
    });

    it('should load aggregated data and scraping status on init', () => {
      flatsService.getAggregated.and.returnValue(of(mockAggregatedResponse));
      flatsService.getScrapeStatus.and.returnValue(of(mockScrapingStatusNotRunning));

      fixture.detectChanges();

      expect(flatsService.getAggregated).toHaveBeenCalled();
      expect(flatsService.getScrapeStatus).toHaveBeenCalled();
      expect(component.aggregatedData).toEqual(mockAggregatedResponse.data!);
      expect(component.lastUpdated).toBeTruthy();
    });
  });

  describe('loadAggregatedData', () => {
    beforeEach(() => {
      flatsService.getScrapeStatus.and.returnValue(of(mockScrapingStatusNotRunning));
    });

    it('should load and display aggregated data', () => {
      flatsService.getAggregated.and.returnValue(of(mockAggregatedResponse));

      component.loadAggregatedData();

      expect(component.loading).toBe(false);
      expect(component.error).toBeNull();
      expect(component.aggregatedData.length).toBe(2);
      expect(component.aggregatedData[0].postalCode).toBe('10115');
      expect(component.lastUpdated).toBeTruthy();
    });

    it('should set loading flag after data fetch', () => {
      flatsService.getAggregated.and.returnValue(of(mockAggregatedResponse));

      component.loadAggregatedData();

      expect(component.loading).toBe(false);
      expect(component.aggregatedData).toEqual(mockAggregatedResponse.data!);
    });

    it('should handle API response failure', () => {
      const failureResponse: ApiResponse<FlatAggregation[]> = {
        success: false,
        message: 'Database error',
      };
      flatsService.getAggregated.and.returnValue(of(failureResponse));

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
      flatsService.getAggregated.and.returnValue(of(responseWithoutData));

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

  describe('loadScrapingStatus', () => {
    it('should load scraping status', () => {
      flatsService.getScrapeStatus.and.returnValue(of(mockScrapingStatusNotRunning));

      component.loadScrapingStatus();

      expect(component.scraping).toBe(false);
      expect(component.lastScrapingStatus).toBe('Successful');
      expect(component.lastScrapingCount).toBe(42);
    });

    it('should handle scraping status fetch errors', () => {
      const consoleErrorSpy = spyOn(console, 'error');
      flatsService.getScrapeStatus.and.returnValue(throwError(() => new Error('Network error')));

      component.loadScrapingStatus();

      expect(consoleErrorSpy).toHaveBeenCalled();
    });
  });

  describe('Polling behavior', () => {
    it('should update scraping state from status', () => {
      flatsService.getScrapeStatus.and.returnValue(of(mockScrapingStatusRunning));

      component.loadScrapingStatus();

      expect(component.scraping).toBe(true);
    });

    it('should maintain scraping state as false when not in progress', () => {
      flatsService.getScrapeStatus.and.returnValue(of(mockScrapingStatusNotRunning));

      component.loadScrapingStatus();

      expect(component.scraping).toBe(false);
    });
  });

  describe('triggerScrape', () => {
    beforeEach(() => {
      flatsService.getScrapeStatus.and.returnValue(of(mockScrapingStatusNotRunning));
    });

    it('should trigger a scrape job', () => {
      flatsService.triggerScrape.and.returnValue(of(mockScrapeResponse));
      flatsService.getAggregated.and.returnValue(of(mockAggregatedResponse));

      component.triggerScrape();

      expect(flatsService.triggerScrape).toHaveBeenCalled();
      expect(component.scrapedCount).toBe(150);
      expect(component.error).toBeNull();
    });

    it('should handle scrape failure response', () => {
      const failureResponse: ScrapeResponse = {
        success: false,
        message: 'Failed to start scrape',
        scrapedAtUtc: new Date(),
        flatsCount: 0
      };
      flatsService.triggerScrape.and.returnValue(of(failureResponse));

      component.triggerScrape();

      expect(component.error).toBe('Failed to start scrape');
      expect(component.scrapedCount).toBe(0);
    });

    it('should handle scrape network errors', () => {
      const error = new Error('Network error');
      flatsService.triggerScrape.and.returnValue(throwError(() => error));

      component.triggerScrape();

      expect(component.error).toContain('Scrape error');
      expect(component.scraping).toBe(false);
    });

    it('should clear previous error on new scrape trigger', () => {
      component.error = 'Previous error';
      flatsService.triggerScrape.and.returnValue(of(mockScrapeResponse));
      flatsService.getAggregated.and.returnValue(of(mockAggregatedResponse));

      component.triggerScrape();

      expect(component.error).toBeNull();
    });
  });

  describe('cancelScrape', () => {
    beforeEach(() => {
      flatsService.getScrapeStatus.and.returnValue(of(mockScrapingStatusNotRunning));
    });

    it('should cancel the current scrape job', () => {
      flatsService.cancelScrape.and.returnValue(of({}));

      component.cancelScrape();

      expect(flatsService.cancelScrape).toHaveBeenCalled();
      expect(component.error).toBeNull();
    });

    it('should handle cancel errors', () => {
      const error = new Error('Cancellation failed');
      flatsService.cancelScrape.and.returnValue(throwError(() => error));

      component.cancelScrape();

      expect(component.error).toContain('Cancel error');
    });
  });

  describe('refresh', () => {
    it('should reload aggregated data', () => {
      flatsService.getAggregated.and.returnValue(of(mockAggregatedResponse));
      flatsService.getScrapeStatus.and.returnValue(of(mockScrapingStatusNotRunning));

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
      flatsService.getScrapeStatus.and.returnValue(of(mockScrapingStatusNotRunning));
      const responseWithoutData: ApiResponse<FlatAggregation[]> = {
        success: true,
        message: 'No data available'
      };
      flatsService.getAggregated.and.returnValue(of(responseWithoutData));

      component.loadAggregatedData();

      expect(component.aggregatedData).toEqual([]);
      expect(component.error).toBe('No data available');
    });

    it('should display default error message when response lacks message', () => {
      flatsService.getScrapeStatus.and.returnValue(of(mockScrapingStatusNotRunning));
      const responseWithoutMessage: ApiResponse<FlatAggregation[]> = {
        success: false,
        message: ''
      };
      flatsService.getAggregated.and.returnValue(of(responseWithoutMessage));

      component.loadAggregatedData();

      expect(component.error).toBe('Failed to load data');
    });
  });
});
