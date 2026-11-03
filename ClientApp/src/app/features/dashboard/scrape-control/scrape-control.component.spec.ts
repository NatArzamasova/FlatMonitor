import { HttpErrorResponse } from '@angular/common/http';
import { provideAppLocale } from '../../../core/utils/locale.utils';
import { ToastService } from '../../../core/services/toast.service';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { ScrapeControlComponent } from './scrape-control.component';
import { ScrapeStatusService } from '../../../core/services/scrape-status.service';
import { ScrapeResponse } from '../../../core/models/scrape-response.model';
import { ScrapingStatus, ScrapingStatusResponse } from '../../../core/models/scraping-status-response.model';

describe('ScrapeControlComponent', () => {
  let fixture: ComponentFixture<ScrapeControlComponent>;
  let component: ScrapeControlComponent;
  let status$: Subject<ScrapingStatusResponse>;
  let scrapeStatus: jasmine.SpyObj<ScrapeStatusService>;
  let toasts: jasmine.SpyObj<ToastService>;

  const hoursAgo = (hours: number): string => new Date(Date.now() - hours * 3_600_000).toISOString();

  const finished = (status: ScrapingStatus = 'Successful', extra: Partial<ScrapingStatusResponse> = {}): ScrapingStatusResponse => ({
    isScrapingInProgress: false,
    lastScrapingHistory: { id: 2, startTimeUtc: hoursAgo(2), endTimeUtc: hoursAgo(1.75), status, flatsScrapedCount: 246 },
    lastSuccessfulScrapingHistory: { id: 1, startTimeUtc: hoursAgo(26), status: 'Successful', flatsScrapedCount: 240 },
    ...extra
  });

  const running: ScrapingStatusResponse = {
    isScrapingInProgress: true,
    lastScrapingHistory: { id: 3, startTimeUtc: new Date(Date.now() - 5 * 60_000).toISOString(), status: 'Running', flatsScrapedCount: 25 }
  };

  const queued: ScrapeResponse = {
    success: true,
    message: 'Scraping started',
    data: { jobId: '6f9619ff-8b86-d011-b42d-00cf4fc964ff', queuedAtUtc: '2024-01-15T10:30:00Z' }
  };

  const element = (): HTMLElement => fixture.nativeElement;
  const summary = (): string => element().querySelector('.scrape-summary')!.textContent!.trim();
  const dot = (): HTMLElement => element().querySelector('.scrape-dot')!;
  const actionButton = (): HTMLButtonElement =>
    Array.from(element().querySelectorAll('button.btn-sm')).find(button => !button.classList.contains('scrape-status-button')) as HTMLButtonElement;
  const details = (): HTMLElement | null => element().querySelector('#scrape-details');
  const push = (status: ScrapingStatusResponse) => {
    status$.next(status);
    fixture.detectChanges();
  };
  const openDetails = () => {
    (element().querySelector('.scrape-status-button') as HTMLButtonElement).click();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    status$ = new Subject<ScrapingStatusResponse>();
    scrapeStatus = jasmine.createSpyObj('ScrapeStatusService', ['triggerScrape', 'cancelScrape'], { status$ });
    toasts = jasmine.createSpyObj<ToastService>('ToastService', ['show']);

    await TestBed.configureTestingModule({
      imports: [ScrapeControlComponent],
      providers: [
        // Numbers, prices and dates as in the app (German formats).
        provideAppLocale(),
        { provide: ScrapeStatusService, useValue: scrapeStatus },
        { provide: ToastService, useValue: toasts }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ScrapeControlComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  describe('status line', () => {
    it('says when nothing has been scraped yet', () => {
      expect(summary()).toBe('No scrape yet');
      expect(actionButton().textContent!.trim()).toBe('Scrape now');
    });

    it('shows when the last scrape ran and how many flats it found, with a green dot', () => {
      push(finished());

      expect(summary()).toBe('Last scrape 2 h ago · 246 flats');
      expect(dot().classList).toContain('bg-success');
    });

    it('names an unsuccessful outcome and colours the dot by status', () => {
      const dotFor = (status: ScrapingStatus) => {
        push(finished(status));
        return dot().classList;
      };

      expect(dotFor('PartialFailed')).toContain('bg-warning');
      expect(summary()).toBe('Last scrape 2 h ago (partly failed) · 246 flats');
      expect(dotFor('Failed')).toContain('bg-danger');
      expect(dotFor('Canceled')).toContain('bg-secondary');
    });

    it('shows a running scrape with a pulsing dot, a progress bar and a Cancel button', () => {
      push(running);

      expect(summary()).toBe('Scraping… started 5 min ago · 25 flats');
      expect(dot().classList).toContain('bg-info');
      expect(dot().classList).toContain('scrape-dot-pulse');
      expect(element().querySelector('.scrape-progress[role="progressbar"]')).not.toBeNull();
      expect(actionButton().textContent!.trim()).toBe('Cancel');

      push(finished());
      expect(element().querySelector('.scrape-progress')).toBeNull();
      expect(actionButton().textContent!.trim()).toBe('Scrape now');
    });

    it('reads scrape times without an offset as UTC, as the backend sends them', () => {
      push({
        isScrapingInProgress: false,
        lastScrapingHistory: { id: 3, startTimeUtc: '2024-01-15T10:00:00', status: 'Successful', flatsScrapedCount: 1 }
      });

      expect(component.lastRun!.start.toISOString()).toBe('2024-01-15T10:00:00.000Z');
    });

    it('stops listening to status updates when destroyed', () => {
      fixture.destroy();

      expect(status$.observed).toBe(false);
    });
  });

  describe('finished scrapes', () => {
    const finishWith = (status: ScrapingStatus) => {
      push(running);
      push(finished(status));
    };

    it('reports the outcome of a scrape that has just ended', () => {
      finishWith('Successful');
      expect(toasts.show).toHaveBeenCalledWith('Scrape finished: 246 flats', 'success');

      finishWith('PartialFailed');
      expect(toasts.show).toHaveBeenCalledWith('Scrape partly failed: 246 flats saved', 'warning');

      finishWith('Failed');
      expect(toasts.show).toHaveBeenCalledWith('Scrape failed', 'danger');

      finishWith('Canceled');
      expect(toasts.show).toHaveBeenCalledWith('Scrape canceled', 'info');
    });

    it('says nothing about a scrape that ended before the page was opened', () => {
      push(finished());
      push(finished());

      expect(toasts.show).not.toHaveBeenCalled();
    });

    it('waits for the poll, not the optimistic state after starting, to notice the end', () => {
      scrapeStatus.triggerScrape.and.returnValue(of(queued));
      push(finished());
      component.triggerScrape();
      toasts.show.calls.reset();

      push(finished());

      expect(toasts.show).not.toHaveBeenCalled();
    });
  });

  describe('details panel', () => {
    it('opens with the status, times and flats of the last scrape', () => {
      push(finished());
      openDetails();

      expect(details()).not.toBeNull();
      expect(details()!.querySelector('.badge')!.textContent).toBe('Successful');
      expect(details()!.textContent).toContain('Finished');
      expect(details()!.textContent).toContain('246');
      expect(details()!.textContent).not.toContain('Last successful scrape');
    });

    it('shows the times in the German format', () => {
      push(finished());
      openDetails();

      // `date: 'medium'` with de-DE: `15.01.2024, 10:00:00`
      expect(details()!.textContent).toMatch(/\d{2}\.\d{2}\.\d{4}, \d{2}:\d{2}:\d{2}/);
      expect(details()!.textContent).not.toMatch(/AM|PM/);
    });

    it('adds the error and the last successful scrape when the last one did not succeed', () => {
      push(finished('Failed', {
        lastScrapingHistory: {
          id: 2, startTimeUtc: hoursAgo(2), status: 'Failed', flatsScrapedCount: 0, errorMessage: 'Chrome did not start'
        }
      }));
      openDetails();

      expect(details()!.querySelector('.scrape-error-message')!.textContent).toBe('Chrome did not start');
      expect(details()!.textContent).toContain('Last successful scrape');
      expect(details()!.textContent).toContain('1 day ago');
    });

    it('closes on a second click, on Escape and on a click outside', () => {
      push(finished());
      openDetails();
      openDetails();
      expect(details()).toBeNull();

      openDetails();
      details()!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      fixture.detectChanges();
      expect(details()).toBeNull();

      openDetails();
      document.body.click();
      fixture.detectChanges();
      expect(details()).toBeNull();
    });
  });

  describe('starting a scrape', () => {
    it('starts a scrape and switches to the running state', () => {
      scrapeStatus.triggerScrape.and.returnValue(of(queued));

      actionButton().click();
      fixture.detectChanges();

      expect(scrapeStatus.triggerScrape).toHaveBeenCalled();
      expect(component.scraping).toBe(true);
      expect(actionButton().textContent!.trim()).toBe('Cancel');
      expect(component.busy).toBe(false);
      expect(toasts.show).toHaveBeenCalledWith('Scrape started', 'info');
    });

    it('shows the API message when the scrape could not be started', () => {
      scrapeStatus.triggerScrape.and.returnValue(of({ ...queued, success: false, message: 'Failed to start scrape' }));

      component.triggerScrape();
      fixture.detectChanges();

      expect(element().querySelector('.scrape-error[role="alert"]')!.textContent).toContain('Failed to start scrape');
      expect(component.scraping).toBe(false);
    });

    it('shows network errors, which can be dismissed', () => {
      scrapeStatus.triggerScrape.and.returnValue(throwError(() => new Error('Network error')));

      component.triggerScrape();
      fixture.detectChanges();
      expect(element().querySelector('.scrape-error')!.textContent).toContain('Scrape error: Network error');

      (element().querySelector('.scrape-error .btn-close') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(element().querySelector('.scrape-error')).toBeNull();
    });

    it('shows the API message and stays in the running state when a scrape is already running (409)', () => {
      const conflict = new HttpErrorResponse({
        status: 409,
        statusText: 'Conflict',
        error: { success: false, message: 'A scrape is already queued or running' }
      });
      scrapeStatus.triggerScrape.and.returnValue(throwError(() => conflict));

      component.triggerScrape();

      expect(component.error).toBe('A scrape is already queued or running');
      expect(component.scraping).toBe(true);
    });

    it('clears a previous error', () => {
      component.error = 'Previous error';
      scrapeStatus.triggerScrape.and.returnValue(of(queued));

      component.triggerScrape();

      expect(component.error).toBeNull();
    });
  });

  describe('cancelling a scrape', () => {
    beforeEach(() => push(running));

    it('cancels the running scrape', () => {
      scrapeStatus.cancelScrape.and.returnValue(of({ success: true, data: true, message: 'Scrape cancellation requested' }));

      actionButton().click();

      expect(scrapeStatus.cancelScrape).toHaveBeenCalled();
      expect(component.error).toBeNull();
      expect(toasts.show).toHaveBeenCalledWith('Cancelling the scrape…', 'info');
    });

    it('shows cancel errors', () => {
      scrapeStatus.cancelScrape.and.returnValue(throwError(() => new Error('Cancellation failed')));

      component.cancelScrape();

      expect(component.error).toBe('Cancel error: Cancellation failed');
    });
  });
});
