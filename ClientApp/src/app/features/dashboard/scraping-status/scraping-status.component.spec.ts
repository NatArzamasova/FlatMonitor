import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { ScrapingStatusComponent } from './scraping-status.component';
import { ScrapeStatusService } from '../../../core/services/scrape-status.service';
import { ScrapeResponse } from '../../../core/models/scrape-response.model';
import { ScrapingStatus, ScrapingStatusResponse } from '../../../core/models/scraping-status-response.model';

@Component({
  standalone: true,
  imports: [ScrapingStatusComponent],
  template: `
    <app-scraping-status>
      <p scrapingStatusInfo class="projected-info">Projected info</p>
      <button scrapingStatusActions class="projected-action">Projected action</button>
    </app-scraping-status>
  `
})
class HostComponent {}

describe('ScrapingStatusComponent', () => {
  let fixture: ComponentFixture<ScrapingStatusComponent>;
  let component: ScrapingStatusComponent;
  let status$: Subject<ScrapingStatusResponse>;
  let scrapeStatus: jasmine.SpyObj<ScrapeStatusService>;

  const notRunning: ScrapingStatusResponse = {
    isScrapingInProgress: false,
    lastScrapingHistory: {
      id: 1,
      startTimeUtc: '2024-01-15T10:00:00Z',
      endTimeUtc: '2024-01-15T10:15:00Z',
      status: 'Successful',
      flatsScrapedCount: 42
    },
    lastSuccessfulScrapingHistory: {
      id: 1,
      startTimeUtc: '2024-01-15T10:00:00Z',
      status: 'Successful',
      flatsScrapedCount: 42
    }
  };

  const running: ScrapingStatusResponse = {
    isScrapingInProgress: true,
    lastScrapingHistory: {
      id: 2,
      startTimeUtc: '2024-01-15T10:30:00Z',
      status: 'Running',
      flatsScrapedCount: 25
    }
  };

  const queued: ScrapeResponse = {
    success: true,
    message: 'Scraping started',
    scrapedAtUtc: '2024-01-15T10:30:00Z',
    flatsCount: 0
  };

  beforeEach(async () => {
    status$ = new Subject<ScrapingStatusResponse>();
    scrapeStatus = jasmine.createSpyObj('ScrapeStatusService', ['triggerScrape', 'cancelScrape'], { status$ });

    await TestBed.configureTestingModule({
      imports: [ScrapingStatusComponent, HostComponent],
      providers: [{ provide: ScrapeStatusService, useValue: scrapeStatus }]
    }).compileComponents();

    fixture = TestBed.createComponent(ScrapingStatusComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  const text = (): string => fixture.nativeElement.textContent;
  const buttonLabels = (): string[] =>
    (Array.from(fixture.nativeElement.querySelectorAll('button.btn')) as HTMLElement[]).map(b => b.textContent!.trim());

  describe('Status display', () => {
    it('shows the last and last successful scrape from the polled status', () => {
      status$.next(notRunning);
      fixture.detectChanges();

      expect(component.lastScrapingStatus).toBe('Successful');
      expect(component.lastScrapingCount).toBe(42);
      expect(text()).toContain('Last scrape:');
      expect(text()).toContain('Last successful scrape:');
      expect(text()).toContain('42 flats');
      expect(fixture.nativeElement.querySelector('.badge.bg-success')).toBeTruthy();
    });

    it('shows Manual Scrape while idle and Cancel Scraping while running', () => {
      status$.next(notRunning);
      fixture.detectChanges();
      expect(buttonLabels()).toEqual(['Manual Scrape']);

      status$.next(running);
      fixture.detectChanges();
      expect(component.scraping).toBe(true);
      expect(buttonLabels()).toEqual(['Cancel Scraping']);
      expect(fixture.nativeElement.querySelector('.badge.bg-info')?.textContent).toContain('Running');
    });

    it('reads scrape times without an offset as UTC, as the backend sends them', () => {
      status$.next({
        isScrapingInProgress: false,
        lastScrapingHistory: { id: 3, startTimeUtc: '2024-01-15T10:00:00', status: 'Successful', flatsScrapedCount: 1 },
        lastSuccessfulScrapingHistory: { id: 3, startTimeUtc: '2024-01-15T10:00:00', status: 'Successful', flatsScrapedCount: 1 }
      });

      expect(component.lastScrapingTime!.toISOString()).toBe('2024-01-15T10:00:00.000Z');
      expect(component.lastSuccessfulScrapingTime!.toISOString()).toBe('2024-01-15T10:00:00.000Z');
    });

    it('colours every status the backend can report', () => {
      const badgeFor = (status: ScrapingStatus) => {
        status$.next({
          isScrapingInProgress: false,
          lastScrapingHistory: { id: 4, startTimeUtc: '2024-01-15T10:00:00Z', status, flatsScrapedCount: 0 }
        });
        fixture.detectChanges();
        return fixture.nativeElement.querySelector('p .badge') as HTMLElement;
      };

      expect(badgeFor('Running').classList).toContain('bg-info');
      expect(badgeFor('Successful').classList).toContain('bg-success');
      expect(badgeFor('PartialFailed').classList).toContain('bg-warning');
      expect(badgeFor('PartialFailed').textContent).toBe('PartialFailed');
      expect(badgeFor('Failed').classList).toContain('bg-danger');
      expect(badgeFor('Canceled').classList).toContain('bg-warning');
    });

    it('stops listening to status updates when destroyed', () => {
      fixture.destroy();

      expect(status$.observed).toBe(false);
    });
  });

  describe('Alerts (DOM)', () => {
    it('shows the success message with a check mark and dismisses it', () => {
      scrapeStatus.triggerScrape.and.returnValue(of(queued));
      component.triggerScrape();
      fixture.detectChanges();

      const alert = fixture.nativeElement.querySelector('.alert-info') as HTMLElement;
      expect(alert.textContent!.trim()).toContain('✓ Scraping started');

      (alert.querySelector('.btn-close') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(component.success).toBeNull();
      expect(fixture.nativeElement.querySelector('.alert-info')).toBeNull();
    });

    it('shows errors in a danger alert that can be dismissed', () => {
      scrapeStatus.triggerScrape.and.returnValue(throwError(() => new Error('Network error')));
      component.triggerScrape();
      fixture.detectChanges();

      const alert = fixture.nativeElement.querySelector('.alert-danger') as HTMLElement;
      expect(alert.textContent).toContain('Scrape error: Network error');

      (alert.querySelector('.btn-close') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(component.error).toBeNull();
      expect(fixture.nativeElement.querySelector('.alert-danger')).toBeNull();
    });
  });

  describe('triggerScrape', () => {
    it('shows a single queued message after triggering a scrape', () => {
      scrapeStatus.triggerScrape.and.returnValue(of(queued));

      component.triggerScrape();
      fixture.detectChanges();

      const alerts = Array.from(fixture.nativeElement.querySelectorAll('.alert')) as HTMLElement[];
      expect(scrapeStatus.triggerScrape).toHaveBeenCalled();
      expect(component.scraping).toBe(true);
      expect(alerts.length).toBe(1);
      expect(alerts[0].textContent).toContain('Scraping started');
    });

    it('shows the API message when the scrape could not be started', () => {
      scrapeStatus.triggerScrape.and.returnValue(of({ ...queued, success: false, message: 'Failed to start scrape' }));

      component.triggerScrape();

      expect(component.error).toBe('Failed to start scrape');
      expect(component.success).toBeNull();
    });

    it('shows network errors', () => {
      scrapeStatus.triggerScrape.and.returnValue(throwError(() => new Error('Network error')));

      component.triggerScrape();

      expect(component.error).toBe('Scrape error: Network error');
      expect(component.scraping).toBe(false);
    });

    it('clears a previous error', () => {
      component.error = 'Previous error';
      scrapeStatus.triggerScrape.and.returnValue(of(queued));

      component.triggerScrape();

      expect(component.error).toBeNull();
    });
  });

  describe('cancelScrape', () => {
    it('cancels the current scrape job', () => {
      scrapeStatus.cancelScrape.and.returnValue(of({ message: 'Scrape cancellation requested' }));

      component.cancelScrape();

      expect(scrapeStatus.cancelScrape).toHaveBeenCalled();
      expect(component.error).toBeNull();
    });

    it('shows cancel errors', () => {
      scrapeStatus.cancelScrape.and.returnValue(throwError(() => new Error('Cancellation failed')));

      component.cancelScrape();

      expect(component.error).toBe('Cancel error: Cancellation failed');
    });
  });

  describe('Content projection', () => {
    it('renders projected info before the status and projected actions before the scrape button', () => {
      const hostFixture = TestBed.createComponent(HostComponent);
      hostFixture.detectChanges();
      const host: HTMLElement = hostFixture.nativeElement;

      expect(host.querySelector('.text-muted .projected-info')).toBeTruthy();
      const actionButtons = Array.from(host.querySelectorAll('.col-auto button')) as HTMLElement[];
      expect(actionButtons.map(button => button.textContent!.trim())).toEqual(['Projected action', 'Manual Scrape']);
    });
  });
});
