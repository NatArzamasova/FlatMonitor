import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { FlatsService } from './flats.service';
import { ApiResponse } from '../models/api-response.model';
import { FlatAggregation } from '../models/flat-aggregation.model';
import { FlatOffer } from '../models/flat-offer.model';
import { FlatHistory } from '../models/flat-history.model';
import { ApiResponseError } from '../utils/api-response.utils';
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
        expect(result).toEqual(mockData.data!);
        expect(result.length).toBe(1);
        expect(result[0].avgPricePerMeter).toBe(12.50);
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
        expect(result).toEqual([]);
      });

      const req = httpMock.expectOne('/api/flats/aggregated');
      req.flush(mockData);
    });

    it('should error with ApiResponseError when the API reports a failure', () => {
      let error: unknown;
      service.getAggregated().subscribe({
        next: () => fail('should have failed'),
        error: err => error = err
      });

      httpMock.expectOne('/api/flats/aggregated').flush({ success: false, message: 'Database error' });

      expect(error).toEqual(jasmine.any(ApiResponseError));
      expect((error as ApiResponseError).message).toBe('Database error');
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

  describe('getCurrentOffers', () => {
    it('should fetch current offers and return the unwrapped data', () => {
      const offers: FlatOffer[] = [{ district: 'Mitte', roomsCount: 2, priceEur: 2000, source: 'OhneMakler' }];
      let result: FlatOffer[] | undefined;

      service.getCurrentOffers().subscribe(data => result = data);

      const req = httpMock.expectOne('/api/flats/current-offers');
      expect(req.request.method).toBe('GET');
      req.flush({ success: true, data: offers, message: 'Success' });
      expect(result).toEqual(offers);
    });

    it('should error with ApiResponseError when the response has no data', () => {
      let error: unknown;
      service.getCurrentOffers().subscribe({ error: err => error = err });

      httpMock.expectOne('/api/flats/current-offers').flush({ success: true, message: 'No offers' });

      expect(error).toEqual(jasmine.any(ApiResponseError));
      expect((error as ApiResponseError).message).toBe('No offers');
    });
  });

  describe('getHistory', () => {
    it('should fetch history and return the unwrapped data', () => {
      const history: FlatHistory[] = [{ scrapingDate: '2024-01-15', district: 'Mitte', avgPricePerMeter: 12, count: 3 }];
      let result: FlatHistory[] | undefined;

      service.getHistory().subscribe(data => result = data);

      const req = httpMock.expectOne('/api/flats/history');
      expect(req.request.method).toBe('GET');
      req.flush({ success: true, data: history, message: 'Success' });
      expect(result).toEqual(history);
    });

    it('should pass HTTP errors through unchanged', () => {
      let error: unknown;
      service.getHistory().subscribe({ error: err => error = err });

      httpMock.expectOne('/api/flats/history').flush('Server error', { status: 500, statusText: 'Internal Server Error' });

      expect(error).not.toEqual(jasmine.any(ApiResponseError));
      expect((error as { status: number }).status).toBe(500);
    });
  });

  describe('triggerScrape', () => {
    it('should trigger a scrape job', () => {
      const mockResponse: ScrapeResponse = {
        success: true,
        message: 'Scrape job queued',
        data: { jobId: '6f9619ff-8b86-d011-b42d-00cf4fc964ff', queuedAtUtc: '2024-01-15T10:30:00Z' }
      };

      service.triggerScrape().subscribe(result => {
        expect(result).toEqual(mockResponse);
        expect(result.success).toBe(true);
        expect(result.data?.jobId).toBe('6f9619ff-8b86-d011-b42d-00cf4fc964ff');
      });

      const req = httpMock.expectOne('/api/scrape');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({});
      req.flush(mockResponse);
    });

    it('should handle scrape failure', () => {
      const mockResponse: ScrapeResponse = {
        success: false,
        message: 'Scraping failed'
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
          startTimeUtc: '2024-01-15T10:30:00Z',
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
      req.flush({ success: true, data: mockStatus, message: 'Scraping status fetched successfully' });
    });

    it('should fetch scraping status when scraping is complete', () => {
      const mockStatus: ScrapingStatusResponse = {
        isScrapingInProgress: false,
        lastScrapingHistory: {
          id: 1,
          startTimeUtc: '2024-01-15T10:30:00Z',
          endTimeUtc: '2024-01-15T10:45:00Z',
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
      req.flush({ success: true, data: mockStatus, message: 'Scraping status fetched successfully' });
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
      req.flush({ success: true, data: mockStatus, message: 'Scraping status fetched successfully' });
    });
  
    it('should report an API failure as ApiResponseError', () => {
      let error: unknown;
      service.getScrapeStatus().subscribe({ error: err => error = err });

      httpMock.expectOne('/api/scrape/status').flush({ success: false, message: 'Status unavailable' });

      expect(error).toEqual(jasmine.any(ApiResponseError));
      expect((error as Error).message).toBe('Status unavailable');
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
      req.flush({ success: true, data: true, message: 'Scrape cancellation requested' });
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
