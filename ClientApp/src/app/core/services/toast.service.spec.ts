import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { MAX_TOASTS, TOAST_DURATION_MS, Toast, ToastService } from './toast.service';

describe('ToastService', () => {
  let service: ToastService;
  let toasts: Toast[];

  beforeEach(() => {
    service = TestBed.inject(ToastService);
    service.toasts$.subscribe(current => toasts = current);
  });

  it('shows a success toast by default and hides it after 4 s', fakeAsync(() => {
    service.show('Preset saved');
    expect(toasts.map(toast => [toast.message, toast.tone])).toEqual([['Preset saved', 'success']]);

    tick(TOAST_DURATION_MS - 1);
    expect(toasts.length).toBe(1);

    tick(1);
    expect(toasts).toEqual([]);
  }));

  it('keeps a toast without a duration until it is dismissed', fakeAsync(() => {
    const id = service.show('Scrape failed', 'danger', 0);
    tick(60_000);
    expect(toasts.length).toBe(1);

    service.dismiss(id);
    expect(toasts).toEqual([]);
  }));

  it('dismisses one toast and keeps the others', fakeAsync(() => {
    const first = service.show('First', 'info');
    service.show('Second', 'warning');

    service.dismiss(first);

    expect(toasts.map(toast => toast.message)).toEqual(['Second']);
    tick(TOAST_DURATION_MS);
  }));

  it('drops the oldest toasts beyond the maximum', fakeAsync(() => {
    for (let index = 1; index <= MAX_TOASTS + 2; index++) {
      service.show(`Toast ${index}`);
    }

    expect(toasts.length).toBe(MAX_TOASTS);
    expect(toasts[0].message).toBe('Toast 3');
    tick(TOAST_DURATION_MS);
  }));
});
