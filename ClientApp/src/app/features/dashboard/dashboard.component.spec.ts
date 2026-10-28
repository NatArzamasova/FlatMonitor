import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { routes } from '../../app.routes';
import { OffersTabComponent } from './tabs/offers-tab/offers-tab.component';
import { FlatsService } from '../../core/services/flats.service';
import { apiResult } from '../../core/testing/api-response.testing';
import { FilterPresetsService } from '../../core/services/filter-presets.service';
import { createFilterPresetsServiceStub } from '../../core/testing/filter-presets.testing';

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
        { provide: FlatsService, useValue: flatsServiceSpy },
        { provide: FilterPresetsService, useValue: createFilterPresetsServiceStub().service }
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

    const links = Array.from(harness.routeNativeElement!.querySelectorAll('.nav-tabs a')) as HTMLAnchorElement[];
    expect(links.map(link => link.getAttribute('href'))).toEqual(['/overview', '/offers', '/history']);
    expect(links.map(link => link.classList.contains('active'))).toEqual([false, true, false]);
    expect(links[1].getAttribute('aria-current')).toBe('page');
  });

  it('switches tabs when a tab link is clicked', async () => {
    await harness.navigateByUrl('/overview');

    const historyLink = harness.routeNativeElement!.querySelectorAll('.nav-tabs a')[2] as HTMLAnchorElement;
    historyLink.click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(router.url).toBe('/history');
    expect(rendered('app-history-tab')).toBeTruthy();
  });
});
