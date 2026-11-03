import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ToastContainerComponent } from './toast-container.component';
import { ToastService } from '../../core/services/toast.service';

describe('ToastContainerComponent', () => {
  let fixture: ComponentFixture<ToastContainerComponent>;
  let toastService: ToastService;

  const toasts = (): HTMLElement[] => Array.from(fixture.nativeElement.querySelectorAll('.toast'));

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ToastContainerComponent] }).compileComponents();
    toastService = TestBed.inject(ToastService);
    fixture = TestBed.createComponent(ToastContainerComponent);
    fixture.detectChanges();
  });

  it('is a polite live region in the bottom right corner', () => {
    const container = fixture.nativeElement.querySelector('.toast-container') as HTMLElement;

    expect(container.getAttribute('aria-live')).toBe('polite');
    expect(container.classList).toContain('bottom-0');
    expect(container.classList).toContain('end-0');
  });

  it('shows each toast with its tone; errors are announced as alerts', () => {
    toastService.show('Preset saved', 'success', 0);
    toastService.show('Scrape failed', 'danger', 0);
    fixture.detectChanges();

    expect(toasts().map(toast => toast.querySelector('.app-toast-message')!.textContent)).toEqual(['Preset saved', 'Scrape failed']);
    expect(toasts()[0].classList).toContain('app-toast-success');
    expect(toasts()[0].getAttribute('role')).toBe('status');
    expect(toasts()[1].getAttribute('role')).toBe('alert');
  });

  it('closes a toast from its close button', () => {
    toastService.show('Preset saved', 'success', 0);
    fixture.detectChanges();

    (toasts()[0].querySelector('.btn-close') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(toasts()).toEqual([]);
  });
});
