import { HttpErrorResponse } from '@angular/common/http';
import { of } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { ApiResponseError, describeLoadError, unwrapApiResponse } from './api-response.utils';

describe('api-response.utils', () => {
  describe('unwrapApiResponse', () => {
    const unwrap = <T>(response: ApiResponse<T>) => {
      let value: T | undefined;
      let error: unknown;
      of(response).pipe(unwrapApiResponse<T>()).subscribe({
        next: result => value = result,
        error: err => error = err
      });
      return { value, error };
    };

    it('emits the data of a successful response', () => {
      const { value, error } = unwrap({ success: true, data: [1, 2], message: 'Success' });

      expect(value).toEqual([1, 2]);
      expect(error).toBeUndefined();
    });

    it('emits empty data as a success', () => {
      expect(unwrap({ success: true, data: [], message: 'Success' }).value).toEqual([]);
    });

    it('errors with the API message when the response reports a failure', () => {
      const { value, error } = unwrap({ success: false, message: 'Database error' });

      expect(value).toBeUndefined();
      expect(error).toEqual(jasmine.any(ApiResponseError));
      expect((error as ApiResponseError).message).toBe('Database error');
    });

    it('errors when a successful response carries no data', () => {
      const { error } = unwrap({ success: true, message: 'No data available' });

      expect(error).toEqual(jasmine.any(ApiResponseError));
      expect((error as ApiResponseError).message).toBe('No data available');
    });

    it('errors when a successful response carries null data', () => {
      const { error } = unwrap({ success: true, data: null as unknown as number[], message: '' });

      expect(error).toEqual(jasmine.any(ApiResponseError));
    });
  });

  describe('describeLoadError', () => {
    it('uses the API message for API-reported failures', () => {
      expect(describeLoadError(new ApiResponseError('Database error'), 'Failed to load data', 'Error loading data'))
        .toBe('Database error');
    });

    it('falls back when the API sent no message', () => {
      expect(describeLoadError(new ApiResponseError(''), 'Failed to load data', 'Error loading data'))
        .toBe('Failed to load data');
    });

    it('prefixes transport and HTTP errors', () => {
      expect(describeLoadError(new Error('Network error'), 'Failed to load data', 'Error loading data'))
        .toBe('Error loading data: Network error');

      const httpError = new HttpErrorResponse({ status: 500, statusText: 'Server Error', url: '/api/flats/history' });
      expect(describeLoadError(httpError, 'Failed', 'Error loading history'))
        .toBe(`Error loading history: ${httpError.message}`);
    });
  });
});
