import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ThemeToggleComponent } from '../theme-toggle/theme-toggle.component';

/**
 * Sticky app bar shared by all pages: the brand on the left, then the projected navigation and actions:
 * `<app-header><nav headerNav>…</nav><a headerActions>…</a></app-header>`.
 * The actions are pushed to the right, followed by the theme toggle; on narrow screens the bar wraps.
 */
@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink, ThemeToggleComponent],
  host: {
    class: 'navbar navbar-expand bg-body border-bottom sticky-top px-3 py-2 gap-3 flex-wrap',
    role: 'banner'
  },
  template: `
    <a class="navbar-brand d-flex align-items-center gap-2 fw-semibold me-0" routerLink="/overview">
      <img src="favicon.svg" alt="" width="28" height="28" />
      FlatMonitor
      <span class="text-body-secondary fw-normal fs-6 d-none d-sm-inline">Karlsruhe</span>
    </a>
    <ng-content select="[headerNav]"></ng-content>
    <div class="ms-auto d-flex align-items-center gap-2">
      <ng-content select="[headerActions]"></ng-content>
      <app-theme-toggle></app-theme-toggle>
    </div>
  `
})
export class AppHeaderComponent {}
