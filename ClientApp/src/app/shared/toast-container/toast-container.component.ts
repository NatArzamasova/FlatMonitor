import { Component, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { ToastService } from '../../core/services/toast.service';

/** The toasts of `ToastService`, stacked in the bottom right corner. Placed once, in the app root. */
@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [AsyncPipe],
  template: `
    <div class="toast-container position-fixed bottom-0 end-0 p-3" aria-live="polite" aria-atomic="false">
      @for (toast of toasts$ | async; track toast.id) {
        <div class="toast show app-toast" [class]="'app-toast-' + toast.tone"
          [attr.role]="toast.tone === 'danger' ? 'alert' : 'status'">
          <div class="d-flex align-items-center gap-2 px-3 py-2">
            <span class="app-toast-dot" aria-hidden="true"></span>
            <span class="flex-grow-1 app-toast-message">{{ toast.message }}</span>
            <button type="button" class="btn-close" aria-label="Close" (click)="dismiss(toast.id)"></button>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .app-toast {
      --tone: var(--bs-primary);
      border: var(--bs-border-width) solid var(--bs-border-color-translucent);
      border-left: 4px solid var(--tone);
      box-shadow: var(--bs-box-shadow);
    }
    .app-toast-success { --tone: var(--bs-success); }
    .app-toast-info { --tone: var(--bs-info); }
    .app-toast-warning { --tone: var(--bs-warning); }
    .app-toast-danger { --tone: var(--bs-danger); }
    .app-toast-dot {
      flex-shrink: 0;
      width: 0.5rem;
      height: 0.5rem;
      border-radius: 50%;
      background-color: var(--tone);
    }
    .app-toast .btn-close {
      font-size: 0.75rem;
    }
  `]
})
export class ToastContainerComponent {
  private readonly toastService = inject(ToastService);

  readonly toasts$ = this.toastService.toasts$;

  dismiss(id: number): void {
    this.toastService.dismiss(id);
  }
}
