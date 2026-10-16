import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { FlatsService } from './flats.service';
import { ApiResponse } from '../models/api-response.model';
import { FlatAggregation } from '../models/flat-aggregation.model';
import { ScrapeResponse } from '../models/scrape-response.model';
import { ScrapingStatusResponse, ScrapingHistoryDto } from '../models/scraping-status-response.model';

describe('FlatsService', () => {
  let service: FlatsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [FlatsService]
    });
    service = TestBed.inject(FlatsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('getAggregated', () => {
    it('should fetch aggregated flat data', () => {
      const mockData: ApiResponse<FlatAggregation[]> = {
        success: true,
        data: [
          {
            postalCode: '10115',
            district: 'Mitte',
            roomsCount: 2,
            availability: '2024-01-15',
            count: 5,
            avgPricePerMeter: 12.50
          }
        ],
        message: 'Success'
      };

      service.getAggregated().subscribe(result => {
        expect(result).toEqual(mockData);
        expect(result.success).toBe(true);
        expect(result.data?.length).toBe(1);
        expect(result.data?.[0].avgPricePerMeter).toBe(12.50);
      });

      const req = httpMock.expectOne('/api/flats/aggregated');
      expect(req.request.method).toBe('GET');
      req.flush(mockData);
    });

    it('should handle empty aggregated data', () => {
      const mockData: ApiResponse<FlatAggregation[]> = {
        success: true,
        data: [],
        message: 'No data available'
      };

      service.getAggregated().subscribe(result => {
        expect(result.data?.length).toBe(0);
      });

      const req = httpMock.expectOne('/api/flats/aggregated');
      req.flush(mockData);
    });

    it('should handle API errors', () => {
      service.getAggregated().subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(500);
        }
      });

      const req = httpMock.expectOne('/api/flats/aggregated');
      req.flush('Server error', { status: 500, statusText: 'Internal Server Error' });
    });
  });

  describe('triggerScrape', () => {
    it('should trigger a scrape job', () => {
      const mockResponse: ScrapeResponse = {
        success: true,
        message: 'Scraping started',
        scrapedAtUtc: new Date('2024-01-15T10:30:00Z'),
        flatsCount: 42
      };

      service.triggerScrape().subscribe(result => {
        expect(result).toEqual(mockResponse);
        expect(result.success).toBe(true);
        expect(result.flatsCount).toBe(42);
      });

      const req = httpMock.expectOne('/api/scrape');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({});
      req.flush(mockResponse);
    });

    it('should handle scrape failure', () => {
      const mockResponse: ScrapeResponse = {
        success: false,
        message: 'Scraping failed',
        scrapedAtUtc: new Date(),
        flatsCount: 0
      };

      service.triggerScrape().subscribe(result => {
        expect(result.success).toBe(false);
        expect(result.message).toContain('failed');
      });

      const req = httpMock.expectOne('/api/scrape');
      req.flush(mockResponse);
    });

    it('should handle network errors', () => {
      service.triggerScrape().subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(503);
        }
      });

      const req = httpMock.expectOne('/api/scrape');
      req.error(new ErrorEvent('Network error'), { status: 503, statusText: 'Service Unavailable' });
    });
  });

  describe('getScrapeStatus', () => {
    it('should fetch scraping status when scraping is in progress', () => {
      const mockStatus: ScrapingStatusResponse = {
        isScrapingInProgress: true,
        lastScrapingHistory: {
          id: 1,
          startTimeUtc: new Date('2024-01-15T10:30:00Z'),
          status: 'Running',
          flatsScrapedCount: 25
        }
      };

      service.getScrapeStatus().subscribe(result => {
        expect(result.isScrapingInProgress).toBe(true);
        expect(result.lastScrapingHistory?.status).toBe('Running');
        expect(result.lastScrapingHistory?.flatsScrapedCount).toBe(25);
      });

      const req = httpMock.expectOne('/api/scrape/status');
      expect(req.request.method).toBe('GET');
      req.flush(mockStatus);
    });

    it('should fetch scraping status when scraping is complete', () => {
      const mockStatus: ScrapingStatusResponse = {
        isScrapingInProgress: false,
        lastScrapingHistory: {
          id: 1,
          startTimeUtc: new Date('2024-01-15T10:30:00Z'),
          endTimeUtc: new Date('2024-01-15T10:45:00Z'),
          status: 'Successful',
          flatsScrapedCount: 150
        }
      };

      service.getScrapeStatus().subscribe(result => {
        expect(result.isScrapingInProgress).toBe(false);
        expect(result.lastScrapingHistory?.status).toBe('Successful');
        expect(result.lastScrapingHistory?.endTimeUtc).toBeDefined();
      });

      const req = httpMock.expectOne('/api/scrape/status');
      req.flush(mockStatus);
    });

    it('should handle status without history', () => {
      const mockStatus: ScrapingStatusResponse = {
        isScrapingInProgress: false
      };

      service.getScrapeStatus().subscribe(result => {
        expect(result.isScrapingInProgress).toBe(false);
        expect(result.lastScrapingHistory).toBeUndefined();
      });

      const req = httpMock.expectOne('/api/scrape/status');
      req.flush(mockStatus);
    });
  });

  describe('cancelScrape', () => {
    it('should cancel the current scrape job', () => {
      service.cancelScrape().subscribe(result => {
        expect(result).toBeDefined();
      });

      const req = httpMock.expectOne('/api/scrape/cancel');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({});
      req.flush({});
    });

    it('should handle cancel errors', () => {
      service.cancelScrape().subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(400);
        }
      });

      const req = httpMock.expectOne('/api/scrape/cancel');
      req.flush('Bad request', { status: 400, statusText: 'Bad Request' });
    });
  });
});
