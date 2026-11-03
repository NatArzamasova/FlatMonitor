import { TestBed } from '@angular/core/testing';
import { DARK_SCHEME_QUERY, nextThemeMode, Theme, THEME_STORAGE_KEY, ThemeService } from './theme.service';

describe('ThemeService', () => {
  let systemDark: boolean;
  let changeListeners: (() => void)[];

  const fakeMediaQuery = () => ({
    get matches() { return systemDark; },
    media: DARK_SCHEME_QUERY,
    addEventListener: (_type: string, listener: () => void) => changeListeners.push(listener)
  }) as unknown as MediaQueryList;

  const createService = (): ThemeService => TestBed.inject(ThemeService);
  const shownTheme = () => document.documentElement.getAttribute('data-bs-theme');
  const switchSystemTo = (dark: boolean) => {
    systemDark = dark;
    changeListeners.forEach(listener => listener());
  };

  beforeEach(() => {
    systemDark = false;
    changeListeners = [];
    localStorage.removeItem(THEME_STORAGE_KEY);
    spyOn(window, 'matchMedia').and.callFake(fakeMediaQuery);
    TestBed.configureTestingModule({});
  });

  afterEach(() => {
    localStorage.removeItem(THEME_STORAGE_KEY);
    document.documentElement.removeAttribute('data-bs-theme');
  });

  it('follows the system setting by default', () => {
    systemDark = true;

    const service = createService();

    expect(service.mode).toBe('system');
    expect(service.theme).toBe('dark');
    expect(shownTheme()).toBe('dark');
    expect(window.matchMedia).toHaveBeenCalledWith(DARK_SCHEME_QUERY);
  });

  it('switches live when the system setting changes in system mode', () => {
    const service = createService();
    const themes: Theme[] = [];
    service.theme$.subscribe(theme => themes.push(theme));

    switchSystemTo(true);
    switchSystemTo(true);
    switchSystemTo(false);

    expect(themes).toEqual(['light', 'dark', 'light']);
    expect(shownTheme()).toBe('light');
  });

  it('ignores the system setting when a fixed theme is chosen', () => {
    const service = createService();
    service.setMode('light');

    switchSystemTo(true);

    expect(service.theme).toBe('light');
    expect(shownTheme()).toBe('light');
  });

  it('sets data-bs-theme and stores the choice', () => {
    const service = createService();

    service.setMode('dark');

    expect(shownTheme()).toBe('dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
  });

  it('restores the stored choice', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');

    const service = createService();

    expect(service.mode).toBe('dark');
    expect(shownTheme()).toBe('dark');
  });

  it('falls back to system for an unknown stored value', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'purple');

    expect(createService().mode).toBe('system');
  });

  it('works when localStorage throws', () => {
    spyOn(Storage.prototype, 'getItem').and.throwError('SecurityError');
    spyOn(Storage.prototype, 'setItem').and.throwError('SecurityError');

    const service = createService();
    expect(service.mode).toBe('system');

    expect(() => service.setMode('dark')).not.toThrow();
    expect(service.theme).toBe('dark');
    expect(shownTheme()).toBe('dark');
  });

  it('cycles System → Light → Dark → System', () => {
    const service = createService();
    const modes: string[] = [];
    service.mode$.subscribe(mode => modes.push(mode));

    service.cycleMode();
    service.cycleMode();
    service.cycleMode();

    expect(modes).toEqual(['system', 'light', 'dark', 'system']);
    expect(nextThemeMode('dark')).toBe('system');
  });
});
