import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { routes } from '../../app.routes';
import { FlatsService } from '../../core/services/flats.service';
import { FilterPresetsService } from '../../core/services/filter-presets.service';
import { FilterPreset } from '../../core/models/filter-preset.model';
import { ScrapingStatusResponse } from '../../core/models/scraping-status-response.model';
import { apiResult } from '../../core/testing/api-response.testing';
import { filterPreset } from '../../core/testing/filter-presets.testing';
import { HistoryTabComponent } from './tabs/history-tab/history-tab.component';
import { OverviewTabComponent } from './tabs/overview-tab/overview-tab.component';
import { OffersTabComponent } from './tabs/offers-tab/offers-tab.component';

/** Real FilterPresetsService and routes (History is lazy-loaded): the active preset must reach every tab. */
describe('Filter presets across tabs', () => {
  let harness: RouterTestingHarness;
  let httpMock: HttpTestingController;

  const east = filterPreset(1, 'East', ['Durlach']);
  const west = filterPreset(2, 'West', ['Weststadt']);

  const ok = <T>(data: T) => apiResult({ success: true, data, message: 'ok' });

  /** Navigates, optionally answers the presets request the tab triggers, and returns the tab inside the dashboard. */
  async function open<T>(url: string, component: Type<T>, presets?: FilterPreset[]): Promise<T> {
    await harness.navigateByUrl(url);
    if (presets) {
      httpMock.expectOne('/api/filter-presets').flush({ success: true, data: presets, message: 'ok' });
    }
    harness.detectChanges();
    return harness.routeDebugElement!.query(By.directive(component)).componentInstance as T;
  }

  beforeEach(async () => {
    const flatsService = jasmine.createSpyObj<FlatsService>('FlatsService', [
      'getAggregated', 'getCurrentOffers', 'getHistory', 'getScrapeStatus', 'triggerScrape', 'cancelScrape'
    ]);
    flatsService.getScrapeStatus.and.returnValue(of({ isScrapingInProgress: false } as ScrapingStatusResponse));
    flatsService.getAggregated.and.returnValue(ok([
      { district: 'Durlach', roomsCount: 2, count: 1, avgPricePerMeter: 4000 },
      { district: 'Weststadt', roomsCount: 2, count: 1, avgPricePerMeter: 5000 }
    ]));
    flatsService.getCurrentOffers.and.returnValue(ok([
      { district: 'Durlach', roomsCount: 2, priceEur: 300000, source: 'OhneMakler' },
      { district: 'Weststadt', roomsCount: 2, priceEur: 400000, source: 'OhneMakler' }
    ]));
    flatsService.getHistory.and.returnValue(ok([
      { scrapingDate: '2026-10-27', district: 'Durlach', roomsCount: 2, count: 1, avgPricePerMeter: 4000 },
      { scrapingDate: '2026-10-27', district: 'Weststadt', roomsCount: 2, count: 1, avgPricePerMeter: 5000 }
    ]));

    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: FlatsService, useValue: flatsService }
      ]
    });
    httpMock = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => httpMock.verify());

  it('applies the default preset in the History tab when it is opened first', async () => {
    const history = await open('/history', HistoryTabComponent, [east, { ...west, isDefault: true }]);

    expect(history.selectedDistricts).toEqual(['Weststadt']);
    expect(history.chartData.map(point => point.label)).toEqual(['Weststadt']);
  });

  it('carries a preset chosen in the overview over to the History and offers tabs', async () => {
    await open('/overview', OverviewTabComponent, [east, west]);
    TestBed.inject(FilterPresetsService).selectPreset(east.id);

    const history = await open('/history', HistoryTabComponent);
    expect(history.selectedDistricts).toEqual(['Durlach']);
    expect(history.chartData.map(point => point.label)).toEqual(['Durlach']);

    const offers = await open('/offers', OffersTabComponent);
    expect(offers.selectedDistricts).toEqual(['Durlach']);
  });

  it('carries a preset chosen in the History tab over to the overview', async () => {
    await open('/history', HistoryTabComponent, [east, west]);
    TestBed.inject(FilterPresetsService).selectPreset(west.id);

    const overview = await open('/overview', OverviewTabComponent);

    expect(overview.selectedDistricts).toEqual(['Weststadt']);
  });

  it('applies nothing in a freshly opened tab when no preset is the default', async () => {
    const history = await open('/history', HistoryTabComponent, [east, west]);

    expect(history.selectedDistricts).toEqual([]);
  });
});
