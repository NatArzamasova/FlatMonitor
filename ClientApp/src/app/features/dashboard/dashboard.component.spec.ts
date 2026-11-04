import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { NEVER, of } from 'rxjs';
import { routes } from '../../app.routes';
import { OffersTabComponent } from './tabs/offers-tab/offers-tab.component';
import { FlatsService } from '../../core/services/flats.service';
import { apiResult } from '../../core/testing/api-response.testing';
import { FilterPresetsService } from '../../core/services/filter-presets.service';
import { createFilterPresetsServiceStub } from '../../core/testing/filter-presets.testing';
import { DistrictsService } from '../../core/services/districts.service';
import { DataReloadService } from '../../core/services/data-reload.service';
import { ScrapeStatusService } from '../../core/services/scrape-status.service';

describe('DashboardComponent (Shell) routing', () => {
  let harness: RouterTestingHarness;
  let router: Router;

  beforeEach(async () => {
    const flatsServiceSpy = jasmine.createSpyObj('FlatsService', [
      'getAggregated',
      'getCurrentOffers',
      'getHistory',
      'getScrapeStatus',
      'triggerScrape',
      'cancelScrape'
    ]);
    flatsServiceSpy.getScrapeStatus.and.returnValue(of({ isScrapingInProgress: false }));
    flatsServiceSpy.getAggregated.and.returnValue(apiResult({ success: true, data: [], message: 'Success' }));
    flatsServiceSpy.getCurrentOffers.and.returnValue(apiResult({ success: true, data: [], message: 'Success' }));
    flatsServiceSpy.getHistory.and.returnValue(apiResult({ success: true, data: [], message: 'Success' }));

    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        // The header polls the scrape status every 2 s; that timer would keep the zone from ever becoming stable.
        { provide: ScrapeStatusService, useValue: { status$: NEVER, scrapeFinished$: NEVER, sources$: NEVER } },
        { provide: FlatsService, useValue: flatsServiceSpy },
        { provide: FilterPresetsService, useValue: createFilterPresetsServiceStub().service },
        { provide: DistrictsService, useValue: { districts$: of([]) } }
      ]
    });

    harness = await RouterTestingHarness.create();
    router = TestBed.inject(Router);
  });

  const rendered = (selector: string): Element | null =>
    harness.routeNativeElement?.querySelector(selector) ?? null;

  it('redirects the root URL to the overview tab', async () => {
    await harness.navigateByUrl('/');

    expect(router.url).toBe('/overview');
    expect(rendered('app-overview-tab')).toBeTruthy();
    expect(rendered('app-offers-tab')).toBeNull();
    expect(rendered('app-history-tab')).toBeNull();
  });

  it('renders only the offers tab on /offers', async () => {
    await harness.navigateByUrl('/offers');

    expect(rendered('app-offers-tab')).toBeTruthy();
    expect(rendered('app-overview-tab')).toBeNull();
  });

  it('renders only the history tab on /history', async () => {
    await harness.navigateByUrl('/history');

    expect(rendered('app-history-tab')).toBeTruthy();
    expect(rendered('app-overview-tab')).toBeNull();
  });

  it('passes offers filters from query parameters to the offers tab', async () => {
    await harness.navigateByUrl('/offers?district=Mitte&rooms=2.5');

    const offersTab = harness.fixture.debugElement.query(By.directive(OffersTabComponent))
      .componentInstance as OffersTabComponent;
    expect(offersTab.selectedDistricts).toEqual(['Mitte']);
    expect(offersTab.selectedRooms).toEqual([2.5]);
  });

  it('redirects legacy ?tab=offers links to the offers route with their filters', async () => {
    await harness.navigateByUrl('/?tab=offers&district=Mitte&rooms=2');

    expect(router.url).toBe('/offers?district=Mitte&rooms=2');
    expect(rendered('app-offers-tab')).toBeTruthy();
  });

  it('redirects unknown URLs to the overview tab', async () => {
    await harness.navigateByUrl('/dashboard');

    expect(router.url).toBe('/overview');
  });

  it('renders tab links and marks the current tab as active', async () => {
    await harness.navigateByUrl('/offers');

    const links = Array.from(harness.routeNativeElement!.querySelectorAll('nav[aria-label="Dashboard tabs"] a')) as HTMLAnchorElement[];
    expect(links.map(link => link.getAttribute('href'))).toEqual(['/overview', '/offers', '/history']);
    expect(links.map(link => link.classList.contains('active'))).toEqual([false, true, false]);
    expect(links[1].getAttribute('aria-current')).toBe('page');
  });

  it('switches tabs when a tab link is clicked', async () => {
    await harness.navigateByUrl('/overview');

    const historyLink = harness.routeNativeElement!.querySelectorAll('nav[aria-label="Dashboard tabs"] a')[2] as HTMLAnchorElement;
    historyLink.click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(router.url).toBe('/history');
    expect(rendered('app-history-tab')).toBeTruthy();
  });

  it('shows the scrape control and a reload button in the header, not on the Dashboard tab', async () => {
    await harness.navigateByUrl('/overview');

    expect(rendered('app-header app-scrape-control')).toBeTruthy();
    expect(rendered('app-overview-tab app-scrape-control')).toBeNull();
    expect(rendered('app-header button[aria-label="Reload data"]')).toBeTruthy();
  });

  it('asks the open tab to load its data again from the reload button', async () => {
    await harness.navigateByUrl('/offers');
    const reloads = spyOn(TestBed.inject(DataReloadService), 'requestReload').and.callThrough();

    (rendered('app-header button[aria-label="Reload data"]') as HTMLButtonElement).click();

    expect(reloads).toHaveBeenCalled();
  });

  it('links to the presets page in a new browser tab from the header', async () => {
    await harness.navigateByUrl('/overview');

    const link = harness.routeNativeElement!.querySelector('a[href="/presets"]') as HTMLAnchorElement;
    expect(link.textContent).toContain('Configure filters');
    expect(link.target).toBe('_blank');
    expect(link.rel).toBe('noopener');
  });

  it('renders the presets page on /presets without the dashboard tabs', async () => {
    await harness.navigateByUrl('/presets');

    const page = harness.fixture.nativeElement as HTMLElement;
    expect(page.querySelector('app-filter-presets-page')).toBeTruthy();
    expect(page.querySelector('nav[aria-label="Dashboard tabs"]')).toBeNull();
  });

  it('renders the shared header on the dashboard and on the presets page', async () => {
    await harness.navigateByUrl('/overview');
    expect(rendered('app-header a.navbar-brand')).toBeTruthy();

    await harness.navigateByUrl('/presets');
    const backLink = rendered('app-header a[href="/overview"]:not(.navbar-brand)') as HTMLAnchorElement;
    expect(backLink.textContent).toContain('Back to dashboard');
  });
});
