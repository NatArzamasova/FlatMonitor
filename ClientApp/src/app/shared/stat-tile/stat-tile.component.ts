import { Component, Input } from '@angular/core';

/**
 * A key figure in a small two-line card (label; value with an optional hint): `<app-stat-tile label="Avg. price / m²" value="€4,250" hint="in 12 districts">`.
 * The host fills its grid cell, so tiles in one row are equally high.
 */
@Component({
  selector: 'app-stat-tile',
  standalone: true,
  host: { class: 'd-block h-100' },
  template: `
    <div class="card h-100">
      <div class="card-body stat-tile-body">
        <div class="stat-tile-label">{{ label }}</div>
        <!-- Value and hint share one line, so a tile is only two lines high. -->
        <div class="d-flex align-items-baseline gap-2 text-nowrap overflow-hidden">
          <span class="stat-tile-value text-truncate" [title]="value">{{ value }}</span>
          @if (hint) {
            <span class="stat-tile-hint text-truncate">{{ hint }}</span>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .stat-tile-body {
      padding: 0.375rem 0.75rem;
    }
    .stat-tile-label {
      color: var(--bs-secondary-color);
      font-size: 0.75rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }
    .stat-tile-value {
      color: var(--bs-emphasis-color);
      font-size: 1.25rem;
      font-weight: 600;
      line-height: 1.25;
      font-variant-numeric: tabular-nums;
    }
    .stat-tile-hint {
      color: var(--bs-secondary-color);
      font-size: 0.8125rem;
    }
  `]
})
export class StatTileComponent {
  @Input({ required: true }) label!: string;
  @Input({ required: true }) value!: string;
  @Input() hint: string | null = null;
}
