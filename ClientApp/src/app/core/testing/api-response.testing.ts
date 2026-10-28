import { Observable, of } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { unwrapApiResponse } from '../utils/api-response.utils';

/** Mock return value for a FlatsService method: the response as the real service would unwrap it. */
export function apiResult<T>(response: ApiResponse<T>): Observable<T> {
  return of(response).pipe(unwrapApiResponse<T>());
}
