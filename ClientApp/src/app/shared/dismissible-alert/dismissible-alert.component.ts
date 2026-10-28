import { Component, EventEmitter, Input, Output } from '@angular/core';

/**
 * Bootstrap alert with a close button; the message is projected:
 * `<app-dismissible-alert *ngIf="error" type="danger" (dismissed)="error = null">{{ error }}</app-dismissible-alert>`.
 */
@Component({
  selector: 'app-dismissible-alert',
  standalone: true,
  template: `
    <div [class]="'alert alert-' + type + ' alert-dismissible fade show'" role="alert">
      <ng-content></ng-content>
      <button type="button" class="btn-close" aria-label="Close" (click)="dismissed.emit()"></button>
    </div>
  `
})
export class DismissibleAlertComponent {
  @Input() type: 'danger' | 'info' | 'success' | 'warning' = 'danger';
  @Output() dismissed = new EventEmitter<void>();
}
