import { HttpErrorResponse } from '@angular/common/http';
import { describeScrapeStartError } from './scrape-error.utils';

describe('describeScrapeStartError', () => {
  it('reports a 409 as an already running scrape with the API message', () => {
    const conflict = new HttpErrorResponse({ status: 409, error: { success: false, message: 'Already running, wait' } });

    expect(describeScrapeStartError(conflict)).toEqual({ message: 'Already running, wait', alreadyRunning: true });
  });

  it('has a default message for a 409 without one', () => {
    expect(describeScrapeStartError(new HttpErrorResponse({ status: 409 }))).toEqual({
      message: 'A scrape is already queued or running',
      alreadyRunning: true
    });
  });

  it('prefixes other errors', () => {
    expect(describeScrapeStartError(new Error('Network error'))).toEqual({
      message: 'Scrape error: Network error',
      alreadyRunning: false
    });
  });
});
