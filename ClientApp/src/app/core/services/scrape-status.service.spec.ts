import { TestBed, fakeAsync, tick, discardPeriodicTasks } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ScrapeStatusService } from './scrape-status.service';
import { FlatsService } from './flats.service';
import { ScrapeResponse } from '../models/scrape-response.model';
import { ScrapingStatusResponse } from '../models/scraping-status-response.model';

describe('ScrapeStatusService', () => {
  let service: ScrapeStatusService;
  let flatsService: jasmine.SpyObj<FlatsService>;

  const notRunning: ScrapingStatusResponse = { isScrapingInProgress: false };
  const running: ScrapingStatusResponse = { isScrapingInProgress: true };

  beforeEach(() => {
    flatsService = jasmine.createSpyObj('FlatsService', ['getScrapeStatus', 'triggerScrape', 'cancelScrape']);
    TestBed.configureTestingModule({
      providers: [{ provide: FlatsService, useValue: flatsService }]
    });
    service = TestBed.inject(ScrapeStatusService);
  });

  describe('status$', () => {
    it('polls right away and then every 2 seconds', fakeAsync(() => {
      flatsService.getScrapeStatus.and.returnValue(of(notRunning));
      const subscription = service.status$.subscribe();

      tick();
      expect(flatsService.getScrapeStatus).toHaveBeenCalledTimes(1);
      tick(2000);
      expect(flatsService.getScrapeStatus).toHaveBeenCalledTimes(2);
      tick(4000);
      expect(flatsService.getScrapeStatus).toHaveBeenCalledTimes(4);

      subscription.unsubscribe();
    }));

    it('shares one poll between subscribers and replays the latest status', fakeAsync(() => {
      flatsService.getScrapeStatus.and.returnValue(of(running));
      const received: ScrapingStatusResponse[] = [];
      const first = service.status$.subscribe();
      tick();
      const second = service.status$.subscribe(status => received.push(status));

      expect(flatsService.getScrapeStatus).toHaveBeenCalledTimes(1);
      expect(received).toEqual([running]);

      first.unsubscribe();
      second.unsubscribe();
    }));

    it('stops polling when the last subscriber unsubscribes', fakeAsync(() => {
      flatsService.getScrapeStatus.and.returnValue(of(notRunning));
      const subscription = service.status$.subscribe();
      tick(2000);

      subscription.unsubscribe();
      tick(10000);

      expect(flatsService.getScrapeStatus).toHaveBeenCalledTimes(2);
    }));

    it('keeps polling after a failed status request', fakeAsync(() => {
      spyOn(console, 'error');
      flatsService.getScrapeStatus.and.returnValues(
        of(notRunning),
        throwError(() => new Error('Network error')),
        of(running)
      );
      const received: ScrapingStatusResponse[] = [];
      const subscription = service.status$.subscribe(status => received.push(status));

      tick(2000);
      tick(2000);

      expect(flatsService.getScrapeStatus).toHaveBeenCalledTimes(3);
      expect(console.error).toHaveBeenCalledWith('Failed to poll scraping status:', jasmine.any(Error));
      expect(received).toEqual([notRunning, running]);

      subscription.unsubscribe();
    }));

    it('polls immediately on refresh()', fakeAsync(() => {
      flatsService.getScrapeStatus.and.returnValue(of(notRunning));
      const subscription = service.status$.subscribe();
      tick();

      service.refresh();

      expect(flatsService.getScrapeStatus).toHaveBeenCalledTimes(2);

      subscription.unsubscribe();
      discardPeriodicTasks();
    }));
  });

  describe('scrapeFinished$', () => {
    it('emits once when a running scrape finishes', fakeAsync(() => {
      flatsService.getScrapeStatus.and.returnValues(of(running), of(running), of(notRunning), of(notRunning));
      let finished = 0;
      const subscription = service.scrapeFinished$.subscribe(() => finished++);

      tick(2000);
      expect(finished).toBe(0);
      tick(2000);
      expect(finished).toBe(1);
      tick(2000);
      expect(finished).toBe(1);

      subscription.unsubscribe();
    }));

    it('emits when a scrape finishes after a failed status request', fakeAsync(() => {
      spyOn(console, 'error');
      flatsService.getScrapeStatus.and.returnValues(
        of(running),
        throwError(() => new Error('Network error')),
        of(notRunning)
      );
      let finished = 0;
      const subscription = service.scrapeFinished$.subscribe(() => finished++);

      tick(4000);

      expect(finished).toBe(1);

      subscription.unsubscribe();
    }));

    it('does not emit while no scrape is running', fakeAsync(() => {
      flatsService.getScrapeStatus.and.returnValue(of(notRunning));
      let finished = 0;
      const subscription = service.scrapeFinished$.subscribe(() => finished++);

      tick(6000);

      expect(finished).toBe(0);

      subscription.unsubscribe();
    }));
  });

  describe('triggerScrape', () => {
    const queued: ScrapeResponse = { success: true, message: 'Scrape job queued', scrapedAtUtc: '2024-01-15T10:30:00Z', flatsCount: 0 };

    it('starts a scrape and refreshes the status on success', fakeAsync(() => {
      flatsService.getScrapeStatus.and.returnValue(of(notRunning));
      flatsService.triggerScrape.and.returnValue(of(queued));
      const subscription = service.status$.subscribe();
      tick();

      let response: ScrapeResponse | undefined;
      service.triggerScrape().subscribe(result => response = result);

      expect(response).toEqual(queued);
      expect(flatsService.getScrapeStatus).toHaveBeenCalledTimes(2);

      subscription.unsubscribe();
      discardPeriodicTasks();
    }));

    it('does not refresh the status when the scrape could not be started', fakeAsync(() => {
      flatsService.getScrapeStatus.and.returnValue(of(notRunning));
      flatsService.triggerScrape.and.returnValue(of({ ...queued, success: false, message: 'Busy' }));
      const subscription = service.status$.subscribe();
      tick();

      service.triggerScrape().subscribe();

      expect(flatsService.getScrapeStatus).toHaveBeenCalledTimes(1);

      subscription.unsubscribe();
      discardPeriodicTasks();
    }));
  });

  describe('cancelScrape', () => {
    it('cancels the running scrape', () => {
      flatsService.cancelScrape.and.returnValue(of({ message: 'Scrape cancellation requested' }));

      service.cancelScrape().subscribe();

      expect(flatsService.cancelScrape).toHaveBeenCalled();
    });
  });
});
