import { Component, Input } from '@angular/core';

/**
 * Grey placeholders in the shape of a tab while its data loads (a filter toolbar, then a table or a chart),
 * so the layout does not jump when the data arrives: `<app-loading-skeleton variant="table" [columns]="6">`.
 * The table variant fills the height left in a `.fill-column`, like the real table.
 */
@Component({
  selector: 'app-loading-skeleton',
  standalone: true,
  host: { class: 'fill-column' },
  template: `
    <div class="fill-column placeholder-glow" role="status" aria-live="polite">
      <span class="visually-hidden">Loading…</span>

      <div class="card mb-3" aria-hidden="true">
        <div class="card-body py-2 px-3 d-flex flex-wrap gap-3">
          <span class="placeholder col-3 skeleton-control"></span>
          <span class="placeholder col-2 skeleton-control"></span>
          <span class="placeholder col-1 skeleton-control"></span>
        </div>
      </div>

      @if (variant === 'chart') {
        <div class="card skeleton-chart" aria-hidden="true">
          <span class="placeholder w-100 h-100"></span>
        </div>
      } @else {
        <div class="table-card table-card-fill" aria-hidden="true">
          <table class="table data-table skeleton-table">
            <thead>
              <tr>
                @for (column of columnList; track column) {
                  <th><span class="placeholder col-6"></span></th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of rowList; track row) {
                <tr>
                  @for (column of columnList; track column) {
                    <!-- Varying widths look less like a grid of bars. -->
                    <td><span class="placeholder" [style.width.%]="cellWidth(row, column)"></span></td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .skeleton-control {
      height: 1.75rem;
      border-radius: var(--bs-border-radius);
    }
    .skeleton-chart {
      height: 24rem;
      padding: 1rem;
    }
    .skeleton-table .placeholder {
      border-radius: var(--bs-border-radius-sm);
    }
  `]
})
export class LoadingSkeletonComponent {
  @Input() variant: 'table' | 'chart' = 'table';
  @Input() rows = 8;
  @Input() columns = 4;

  get rowList(): number[] {
    return Array.from({ length: this.rows }, (_, index) => index);
  }

  get columnList(): number[] {
    return Array.from({ length: this.columns }, (_, index) => index);
  }

  cellWidth(row: number, column: number): number {
    return 40 + ((row * 7 + column * 13) % 5) * 12;
  }
}
