import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { FilterPresetsService } from './filter-presets.service';
import { FilterPreset } from '../models/filter-preset.model';
import { ApiResponseError } from '../utils/api-response.utils';

describe('FilterPresetsService', () => {
  const url = '/api/filter-presets';

  let service: FilterPresetsService;
  let httpMock: HttpTestingController;

  function preset(id: number, name: string, isDefault = false, districts: string[] = []): FilterPreset {
    return { id, name, districts, isDefault, updatedAtUtc: '2026-10-28T09:30:00Z' };
  }

  /** Subscribes to presets$ and records every emission. */
  function recordPresets(): FilterPreset[][] {
    const emissions: FilterPreset[][] = [];
    service.presets$.subscribe(presets => emissions.push(presets));
    return emissions;
  }

  function flushLoad(presets: FilterPreset[]): void {
    httpMock.expectOne(url).flush({ success: true, data: presets, message: 'ok' });
  }

  beforeEach(() => {
    spyOn(console, 'error');
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [FilterPresetsService]
    });
    service = TestBed.inject(FilterPresetsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('presets$', () => {
    it('should not request anything before the first subscribe', () => {
      httpMock.expectNone(url);
    });

    it('should load presets once and share them between subscribers', () => {
      const first = recordPresets();
      const second = recordPresets();

      const req = httpMock.expectOne(url);
      expect(req.request.method).toBe('GET');
      req.flush({ success: true, data: [preset(1, 'East')], message: 'ok' });

      expect(first).toEqual([[preset(1, 'East')]]);
      expect(second).toEqual([[preset(1, 'East')]]);
    });

    it('should replay the loaded presets to late subscribers without a new request', () => {
      recordPresets();
      flushLoad([preset(1, 'East')]);

      const late = recordPresets();

      httpMock.expectNone(url);
      expect(late).toEqual([[preset(1, 'East')]]);
    });

    it('should emit an empty list when the API reports a failure', () => {
      const emissions = recordPresets();

      httpMock.expectOne(url).flush({ success: false, message: 'Database error' });

      expect(emissions).toEqual([[]]);
      expect(console.error).toHaveBeenCalled();
    });

    it('should emit an empty list on an HTTP error', () => {
      const emissions = recordPresets();

      httpMock.expectOne(url).flush('boom', { status: 500, statusText: 'Server Error' });

      expect(emissions).toEqual([[]]);
    });

    it('should retry loading for the next subscriber after a failed load', () => {
      recordPresets();
      httpMock.expectOne(url).flush('boom', { status: 500, statusText: 'Server Error' });

      const retry = recordPresets();
      flushLoad([preset(1, 'East')]);

      expect(retry).toEqual([[], [preset(1, 'East')]]);
    });
  });

  describe('defaultPreset$', () => {
    it('should emit the default preset', () => {
      let result: FilterPreset | null | undefined;
      service.defaultPreset$.subscribe(value => result = value);

      flushLoad([preset(1, 'East'), preset(2, 'West', true)]);

      expect(result).toEqual(preset(2, 'West', true));
    });

    it('should emit null when no preset is the default', () => {
      let result: FilterPreset | null | undefined;
      service.defaultPreset$.subscribe(value => result = value);

      flushLoad([preset(1, 'East')]);

      expect(result).toBeNull();
    });

    it('should emit null when loading fails', () => {
      let result: FilterPreset | null | undefined;
      service.defaultPreset$.subscribe(value => result = value);

      httpMock.expectOne(url).flush('boom', { status: 500, statusText: 'Server Error' });

      expect(result).toBeNull();
    });
  });

  describe('create', () => {
    it('should POST the request and add the created preset to the cache in name order', () => {
      const emissions = recordPresets();
      flushLoad([preset(1, 'East'), preset(2, 'West')]);
      let created: FilterPreset | undefined;

      service.create({ name: 'North', districts: ['Nordstadt'] }).subscribe(value => created = value);

      const req = httpMock.expectOne(url);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ name: 'North', districts: ['Nordstadt'] });
      req.flush({ success: true, data: preset(3, 'North', false, ['Nordstadt']), message: 'Filter preset created' });

      expect(created).toEqual(preset(3, 'North', false, ['Nordstadt']));
      expect(emissions.at(-1)!.map(p => p.name)).toEqual(['East', 'North', 'West']);
    });

    it('should propagate a conflict and leave the cache unchanged', () => {
      const emissions = recordPresets();
      flushLoad([preset(1, 'East')]);
      let error: unknown;

      service.create({ name: 'East', districts: [] }).subscribe({
        next: () => fail('should have failed'),
        error: err => error = err
      });
      httpMock.expectOne(url).flush(
        { success: false, message: "A filter preset named 'East' already exists" },
        { status: 409, statusText: 'Conflict' }
      );

      expect((error as { status: number }).status).toBe(409);
      expect(emissions).toEqual([[preset(1, 'East')]]);
    });
  });

  describe('update', () => {
    it('should PUT the request and replace the preset in the cache', () => {
      const emissions = recordPresets();
      flushLoad([preset(1, 'East'), preset(2, 'West', true)]);

      service.update(2, { name: 'Albtal', districts: ['Rüppurr'] }).subscribe();

      const req = httpMock.expectOne(`${url}/2`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ name: 'Albtal', districts: ['Rüppurr'] });
      req.flush({ success: true, data: preset(2, 'Albtal', true, ['Rüppurr']), message: 'Filter preset updated' });

      expect(emissions.at(-1)).toEqual([preset(2, 'Albtal', true, ['Rüppurr']), preset(1, 'East')]);
    });

    it('should propagate an API failure as ApiResponseError and leave the cache unchanged', () => {
      const emissions = recordPresets();
      flushLoad([preset(1, 'East')]);
      let error: unknown;

      service.update(1, { name: 'East', districts: [] }).subscribe({ error: err => error = err });
      httpMock.expectOne(`${url}/1`).flush({ success: false, message: 'Error updating filter preset' });

      expect(error).toEqual(jasmine.any(ApiResponseError));
      expect(emissions.length).toBe(1);
    });
  });

  describe('delete', () => {
    it('should DELETE the preset and replace the cache with the remaining presets', () => {
      const emissions = recordPresets();
      flushLoad([preset(1, 'East'), preset(2, 'West')]);
      let remaining: FilterPreset[] | undefined;

      service.delete(1).subscribe(value => remaining = value);

      const req = httpMock.expectOne(`${url}/1`);
      expect(req.request.method).toBe('DELETE');
      req.flush({ success: true, data: [preset(2, 'West')], message: 'Filter preset deleted' });

      expect(remaining).toEqual([preset(2, 'West')]);
      expect(emissions.at(-1)).toEqual([preset(2, 'West')]);
    });

    it('should leave the cache unchanged when the preset is not found', () => {
      const emissions = recordPresets();
      flushLoad([preset(1, 'East')]);

      service.delete(42).subscribe({ error: () => undefined });
      httpMock.expectOne(`${url}/42`).flush(
        { success: false, message: 'Filter preset 42 not found' },
        { status: 404, statusText: 'Not Found' }
      );

      expect(emissions.length).toBe(1);
    });
  });

  describe('setDefault', () => {
    it('should PUT to the default route and leave exactly one default in the cache', () => {
      const emissions = recordPresets();
      flushLoad([preset(1, 'East', true), preset(2, 'West')]);

      service.setDefault(2).subscribe();

      const req = httpMock.expectOne(`${url}/2/default`);
      expect(req.request.method).toBe('PUT');
      req.flush({ success: true, data: preset(2, 'West', true), message: 'Default filter preset set' });

      expect(emissions.at(-1)).toEqual([preset(1, 'East', false), preset(2, 'West', true)]);
    });

    it('should update defaultPreset$', () => {
      let result: FilterPreset | null | undefined;
      service.defaultPreset$.subscribe(value => result = value);
      flushLoad([preset(1, 'East'), preset(2, 'West')]);

      service.setDefault(1).subscribe();
      httpMock.expectOne(`${url}/1/default`).flush({ success: true, data: preset(1, 'East', true), message: 'ok' });

      expect(result).toEqual(preset(1, 'East', true));
    });
  });

  describe('clearDefault', () => {
    it('should DELETE the default and replace the cache with the returned presets', () => {
      let result: FilterPreset | null | undefined;
      service.defaultPreset$.subscribe(value => result = value);
      flushLoad([preset(1, 'East', true)]);

      service.clearDefault().subscribe();

      const req = httpMock.expectOne(`${url}/default`);
      expect(req.request.method).toBe('DELETE');
      req.flush({ success: true, data: [preset(1, 'East')], message: 'Default filter preset cleared' });

      expect(result).toBeNull();
    });
  });

  describe('writes before the first load', () => {
    it('should not invent a cache from a single created preset', () => {
      service.create({ name: 'East', districts: [] }).subscribe();
      httpMock.expectOne(url).flush({ success: true, data: preset(1, 'East'), message: 'ok' });

      const emissions = recordPresets();
      flushLoad([preset(1, 'East'), preset(2, 'West')]);

      expect(emissions).toEqual([[preset(1, 'East'), preset(2, 'West')]]);
    });
  });
});
