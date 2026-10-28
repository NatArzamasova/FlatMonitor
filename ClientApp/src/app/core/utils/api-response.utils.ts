import { OperatorFunction } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiResponse } from '../models/api-response.model';

/** The API answered, but reported `success: false` or sent no data. */
export class ApiResponseError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'ApiResponseError';
  }
}

export function unwrapApiResponse<T>(): OperatorFunction<ApiResponse<T>, T> {
  return map(response => {
    if (!response.success || response.data === undefined || response.data === null) {
      throw new ApiResponseError(response.message);
    }
    return response.data;
  });
}

/**
 * Message for a failed load: the API's own message (or `fallback` if it sent none) when the API
 * reported the failure, `"<httpPrefix>: <error>"` for transport/HTTP errors.
 */
export function describeLoadError(err: unknown, fallback: string, httpPrefix: string): string {
  if (err instanceof ApiResponseError) {
    return err.message || fallback;
  }
  // HttpErrorResponse carries a message but does not extend Error.
  const message = typeof (err as { message?: unknown })?.message === 'string'
    ? (err as { message: string }).message
    : String(err);
  return `${httpPrefix}: ${message}`;
}
