import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { DataReloadService } from './data-reload.service';
import { ScrapeStatusService } from './scrape-status.service';

describe('DataReloadService', () => {
  let service: DataReloadService;
  let scrapeFinished$: Subject<void>;

  beforeEach(() => {
    scrapeFinished$ = new Subject<void>();
    TestBed.configureTestingModule({
      providers: [{ provide: ScrapeStatusService, useValue: { scrapeFinished$ } }]
    });
    service = TestBed.inject(DataReloadService);
  });

  it('emits when a reload is requested and when a scrape finishes', () => {
    let reloads = 0;
    service.reload$.subscribe(() => reloads++);

    service.requestReload();
    scrapeFinished$.next();
    service.requestReload();

    expect(reloads).toBe(3);
  });

  it('does not replay earlier requests to new subscribers', () => {
    service.requestReload();
    let reloads = 0;

    service.reload$.subscribe(() => reloads++);

    expect(reloads).toBe(0);
  });
});
