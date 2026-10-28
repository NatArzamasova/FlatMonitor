import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { DistrictsService } from './districts.service';

describe('DistrictsService', () => {
  const url = '/api/districts';

  let service: DistrictsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    spyOn(console, 'error');
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [DistrictsService]
    });
    service = TestBed.inject(DistrictsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should not request anything before the first subscribe', () => {
    httpMock.expectNone(url);
  });

  it('should load the districts once, sorted by name, and share them', () => {
    const first: string[][] = [];
    const second: string[][] = [];
    service.districts$.subscribe(value => first.push(value));

    httpMock.expectOne(url).flush({ success: true, data: ['Südstadt', 'Durlach', 'Rüppurr'], message: 'ok' });
    service.districts$.subscribe(value => second.push(value));

    httpMock.expectNone(url);
    expect(first).toEqual([['Durlach', 'Rüppurr', 'Südstadt']]);
    expect(second).toEqual(first);
  });

  it('should emit an empty list when loading fails', () => {
    const emissions: string[][] = [];
    service.districts$.subscribe(value => emissions.push(value));

    httpMock.expectOne(url).flush('down', { status: 500, statusText: 'Server Error' });

    expect(emissions).toEqual([[]]);
  });
});
