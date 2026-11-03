import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { distinctUntilChanged } from 'rxjs/operators';

/** The user's choice: a fixed theme, or `system` to follow the operating system's setting. */
export type ThemeMode = 'system' | 'light' | 'dark';
/** The theme actually shown. */
export type Theme = 'light' | 'dark';

/** Also read by the inline script in `index.html`, which sets the theme before Angular starts (no white flash). */
export const THEME_STORAGE_KEY = 'flatmonitor.theme';
export const DARK_SCHEME_QUERY = '(prefers-color-scheme: dark)';
/** The order of the header's toggle. */
export const THEME_MODES: readonly ThemeMode[] = ['system', 'light', 'dark'];

/**
 * Light / dark theme. Sets Bootstrap's `data-bs-theme` on `<html>`, so everything styled through Bootstrap's CSS
 * variables switches with it. The choice is kept in `localStorage` (default `system`); in `system` mode the theme
 * follows the operating system's setting live.
 */
@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly darkQuery = this.document.defaultView?.matchMedia?.(DARK_SCHEME_QUERY);

  private readonly modeSubject = new BehaviorSubject<ThemeMode>(this.readStoredMode());
  private readonly themeSubject = new BehaviorSubject<Theme>(this.resolve(this.modeSubject.value));

  readonly mode$: Observable<ThemeMode> = this.modeSubject.asObservable();
  /** The shown theme; emits only when it changes. */
  readonly theme$: Observable<Theme> = this.themeSubject.pipe(distinctUntilChanged());

  constructor() {
    this.apply();
    this.darkQuery?.addEventListener('change', () => this.apply());
  }

  get mode(): ThemeMode {
    return this.modeSubject.value;
  }

  get theme(): Theme {
    return this.themeSubject.value;
  }

  setMode(mode: ThemeMode): void {
    this.modeSubject.next(mode);
    this.storeMode(mode);
    this.apply();
  }

  /** System → Light → Dark → System. */
  cycleMode(): void {
    this.setMode(nextThemeMode(this.mode));
  }

  private apply(): void {
    const theme = this.resolve(this.mode);
    this.document.documentElement.setAttribute('data-bs-theme', theme);
    this.themeSubject.next(theme);
  }

  private resolve(mode: ThemeMode): Theme {
    if (mode !== 'system') {
      return mode;
    }
    return this.darkQuery?.matches ? 'dark' : 'light';
  }

  // localStorage throws in some private windows and when storage is blocked; the theme then just is not remembered.
  private readStoredMode(): ThemeMode {
    try {
      const stored = this.document.defaultView?.localStorage.getItem(THEME_STORAGE_KEY);
      return THEME_MODES.includes(stored as ThemeMode) ? stored as ThemeMode : 'system';
    } catch {
      return 'system';
    }
  }

  private storeMode(mode: ThemeMode): void {
    try {
      this.document.defaultView?.localStorage.setItem(THEME_STORAGE_KEY, mode);
    } catch {
      // Not remembered; the choice still applies to this page.
    }
  }
}

export function nextThemeMode(mode: ThemeMode): ThemeMode {
  return THEME_MODES[(THEME_MODES.indexOf(mode) + 1) % THEME_MODES.length];
}
