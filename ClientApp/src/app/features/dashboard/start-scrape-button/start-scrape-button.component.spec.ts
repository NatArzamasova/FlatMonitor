import { HttpErrorResponse } from '@angular/common/http';
import { ToastService } from '../../../core/services/toast.service';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { StartScrapeButtonComponent } from './start-scrape-button.component';
import { ScrapeStatusService } from '../../../core/services/scrape-status.service';
import { ScrapingStatusResponse } from '../../../core/models/scraping-status-response.model';

describe('StartScrapeButtonComponent', () => {
  let fixture: ComponentFixture<StartScrapeButtonComponent>;
  let status$: Subject<ScrapingStatusResponse>;
  let scrapeStatus: jasmine.SpyObj<ScrapeStatusService>;
  let toasts: jasmine.SpyObj<ToastService>;

  const button = (): HTMLButtonElement => fixture.nativeElement.querySelector('button');
  const error = (): string | undefined =>
    (fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement | null)?.textContent!.trim();

  beforeEach(async () => {
    status$ = new Subject<ScrapingStatusResponse>();
    scrapeStatus = jasmine.createSpyObj('ScrapeStatusService', ['triggerScrape'], { status$ });
    toasts = jasmine.createSpyObj<ToastService>('ToastService', ['show']);

    await TestBed.configureTestingModule({
      imports: [StartScrapeButtonComponent],
      providers: [
        { provide: ScrapeStatusService, useValue: scrapeStatus },
        { provide: ToastService, useValue: toasts }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StartScrapeButtonComponent);
    fixture.detectChanges();
  });

  it('starts a scrape and then shows that one runs', () => {
    scrapeStatus.triggerScrape.and.returnValue(of({ success: true, message: 'Queued', data: { jobId: '1', queuedAtUtc: '' } }));

    button().click();
    fixture.detectChanges();

    expect(scrapeStatus.triggerScrape).toHaveBeenCalled();
    expect(button().disabled).toBe(true);
    expect(button().textContent!.trim()).toBe('Scraping…');
    expect(toasts.show).toHaveBeenCalledWith('Scrape started', 'info');
  });

  it('follows the polled status', () => {
    status$.next({ isScrapingInProgress: true });
    fixture.detectChanges();
    expect(button().disabled).toBe(true);

    status$.next({ isScrapingInProgress: false });
    fixture.detectChanges();
    expect(button().disabled).toBe(false);
    expect(button().textContent!.trim()).toBe('Start a scrape');
  });

  it('shows why a scrape could not be started', () => {
    scrapeStatus.triggerScrape.and.returnValue(throwError(() => new Error('Network error')));

    button().click();
    fixture.detectChanges();

    expect(error()).toBe('Scrape error: Network error');
    expect(button().disabled).toBe(false);
    expect(toasts.show).not.toHaveBeenCalled();
  });

  it('shows the running state when a scrape is already running (409)', () => {
    scrapeStatus.triggerScrape.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 409, error: { message: 'A scrape is already queued or running' } })));

    button().click();
    fixture.detectChanges();

    expect(error()).toBe('A scrape is already queued or running');
    expect(button().disabled).toBe(true);
  });

  it('shows the API message when the API refuses', () => {
    scrapeStatus.triggerScrape.and.returnValue(of({ success: false, message: 'Sources not configured' }));

    button().click();
    fixture.detectChanges();

    expect(error()).toBe('Sources not configured');
  });
});
