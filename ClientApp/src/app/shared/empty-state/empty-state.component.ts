import { Component, Input } from '@angular/core';

export type EmptyStateIcon = 'inbox' | 'search' | 'chart';

/**
 * Centred empty state: a muted icon, a heading, an optional line of text and the projected actions:
 * `<app-empty-state icon="search" heading="No offers match the filters"><button …>Reset filters</button></app-empty-state>`.
 */
@Component({
  selector: 'app-empty-state',
  standalone: true,
  host: { class: 'd-block' },
  template: `
    <div class="empty-state text-center" role="status">
      <svg class="empty-state-icon" viewBox="0 0 24 24" width="48" height="48" fill="none" stroke="currentColor"
        stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        @switch (icon) {
          @case ('search') {
            <circle cx="11" cy="11" r="6.5" />
            <path d="m20 20-4.4-4.4" />
          }
          @case ('chart') {
            <path d="M4 4v16h16" />
            <path d="m7 15 4-4 3 3 5-6" />
          }
          @default {
            <path d="M3 13h5l1.5 3h5L16 13h5" />
            <path d="M5.5 5h13L21 13v6H3v-6z" />
          }
        }
      </svg>
      <h2 class="h5 mt-3 mb-1">{{ heading }}</h2>
      @if (message) {
        <p class="text-body-secondary mb-0">{{ message }}</p>
      }
      <div class="d-flex justify-content-center gap-2 mt-3 empty-state-actions">
        <ng-content></ng-content>
      </div>
    </div>
  `,
  styles: [`
    .empty-state {
      padding: 3rem 1rem;
      background-color: var(--bs-body-bg);
      border: var(--bs-border-width) dashed var(--bs-border-color);
      border-radius: var(--bs-border-radius-lg);
    }
    .empty-state-icon {
      color: var(--bs-tertiary-color);
    }
    .empty-state-actions:empty {
      display: none !important;
    }
  `]
})
export class EmptyStateComponent {
  @Input() icon: EmptyStateIcon = 'inbox';
  @Input({ required: true }) heading!: string;
  @Input() message: string | null = null;
}
