import { TestBed } from '@angular/core/testing';
import { LoadingSpinnerComponent } from './loading-spinner.component';

describe('LoadingSpinnerComponent', () => {
  it('renders a centred Bootstrap spinner with an accessible loading text', async () => {
    await TestBed.configureTestingModule({ imports: [LoadingSpinnerComponent] }).compileComponents();
    const fixture = TestBed.createComponent(LoadingSpinnerComponent);
    fixture.detectChanges();
    const host: HTMLElement = fixture.nativeElement;

    expect(Array.from(host.classList)).toEqual(['d-flex', 'justify-content-center', 'py-5']);
    const spinner = host.querySelector('.spinner-border.text-primary') as HTMLElement;
    expect(spinner.getAttribute('role')).toBe('status');
    expect(spinner.querySelector('.visually-hidden')!.textContent).toBe('Loading...');
  });
});
