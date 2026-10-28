import { Component } from '@angular/core';

@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  host: { class: 'd-flex justify-content-center py-5' },
  template: `
    <div class="spinner-border text-primary" role="status">
      <span class="visually-hidden">Loading...</span>
    </div>
  `
})
export class LoadingSpinnerComponent {}
