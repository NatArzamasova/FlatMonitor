import { Component, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { nextThemeMode, ThemeMode, ThemeService } from '../../core/services/theme.service';

const MODE_NAMES: Record<ThemeMode, string> = { system: 'System', light: 'Light', dark: 'Dark' };

/** Header button that cycles the theme System → Light → Dark; its icon shows the current choice. */
@Component({
  selector: 'app-theme-toggle',
  standalone: true,
  imports: [AsyncPipe],
  template: `
    @if (themeService.mode$ | async; as mode) {
      <button type="button" class="btn btn-sm btn-outline-secondary theme-toggle" (click)="themeService.cycleMode()"
        [attr.aria-label]="label(mode)" [title]="label(mode)">
        <svg width="16" height="16" viewBox="0 0 16 16" aria-hidden="true" focusable="false">
          @switch (mode) {
            @case ('light') {
              <circle cx="8" cy="8" r="3" fill="currentColor" />
              <path stroke="currentColor" stroke-width="1.5" stroke-linecap="round"
                d="M8 1v1.5M8 13.5V15M1 8h1.5M13.5 8H15M3.05 3.05l1.06 1.06M11.89 11.89l1.06 1.06M3.05 12.95l1.06-1.06M11.89 4.11l1.06-1.06" />
            }
            @case ('dark') {
              <path fill="currentColor" d="M6.2 1.2a6.8 6.8 0 1 0 8.6 8.6A5.6 5.6 0 0 1 6.2 1.2z" />
            }
            @default {
              <circle cx="8" cy="8" r="6.25" fill="none" stroke="currentColor" stroke-width="1.5" />
              <path fill="currentColor" d="M8 1.75a6.25 6.25 0 0 1 0 12.5z" />
            }
          }
        </svg>
      </button>
    }
  `,
  styles: [`
    .theme-toggle {
      display: inline-flex;
      align-items: center;
      line-height: 1;
      padding-block: 0.3rem;
    }
  `]
})
export class ThemeToggleComponent {
  readonly themeService = inject(ThemeService);

  label(mode: ThemeMode): string {
    return `Theme: ${MODE_NAMES[mode]} (switch to ${MODE_NAMES[nextThemeMode(mode)]})`;
  }
}
