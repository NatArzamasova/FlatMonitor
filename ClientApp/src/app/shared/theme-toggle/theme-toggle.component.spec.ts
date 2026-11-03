import { ComponentFixture, TestBed } from '@angular/core/testing';
import { THEME_STORAGE_KEY, ThemeService } from '../../core/services/theme.service';
import { ThemeToggleComponent } from './theme-toggle.component';

describe('ThemeToggleComponent', () => {
  let fixture: ComponentFixture<ThemeToggleComponent>;
  let themeService: ThemeService;

  const button = (): HTMLButtonElement => fixture.nativeElement.querySelector('button');
  const click = () => {
    button().click();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    localStorage.removeItem(THEME_STORAGE_KEY);
    spyOn(window, 'matchMedia').and.returnValue(
      { matches: false, addEventListener: () => undefined } as unknown as MediaQueryList
    );
    await TestBed.configureTestingModule({ imports: [ThemeToggleComponent] }).compileComponents();
    themeService = TestBed.inject(ThemeService);
    fixture = TestBed.createComponent(ThemeToggleComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    localStorage.removeItem(THEME_STORAGE_KEY);
    document.documentElement.removeAttribute('data-bs-theme');
  });

  it('names the current choice and the next one in its aria-label and title', () => {
    expect(button().getAttribute('aria-label')).toBe('Theme: System (switch to Light)');
    expect(button().title).toBe('Theme: System (switch to Light)');
  });

  it('cycles System → Light → Dark → System and updates its aria-label', () => {
    click();
    expect(themeService.mode).toBe('light');
    expect(button().getAttribute('aria-label')).toBe('Theme: Light (switch to Dark)');

    click();
    expect(themeService.mode).toBe('dark');
    expect(button().getAttribute('aria-label')).toBe('Theme: Dark (switch to System)');

    click();
    expect(themeService.mode).toBe('system');
    expect(button().getAttribute('aria-label')).toBe('Theme: System (switch to Light)');
  });

  it('shows a decorative icon only', () => {
    expect(button().querySelector('svg')!.getAttribute('aria-hidden')).toBe('true');
    expect(button().textContent!.trim()).toBe('');
  });
});
