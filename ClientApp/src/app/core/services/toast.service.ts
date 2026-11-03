import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export type ToastTone = 'success' | 'info' | 'warning' | 'danger';

export interface Toast {
  id: number;
  message: string;
  tone: ToastTone;
}

export const TOAST_DURATION_MS = 4000;
/** More than this and the oldest toast is dropped, so a burst of messages does not fill the screen. */
export const MAX_TOASTS = 4;

/**
 * Short confirmations shown in the corner of the screen (`ToastContainerComponent`), hidden after 4 s.
 * For results of actions that would otherwise succeed silently; errors stay inline next to what failed.
 */
@Injectable({
  providedIn: 'root'
})
export class ToastService {
  private nextId = 1;
  private readonly toastsSubject = new BehaviorSubject<Toast[]>([]);

  readonly toasts$: Observable<Toast[]> = this.toastsSubject.asObservable();

  /** Shows a toast; `durationMs <= 0` keeps it until it is dismissed. Returns its id. */
  show(message: string, tone: ToastTone = 'success', durationMs = TOAST_DURATION_MS): number {
    const toast: Toast = { id: this.nextId++, message, tone };
    this.toastsSubject.next([...this.toastsSubject.value, toast].slice(-MAX_TOASTS));
    if (durationMs > 0) {
      setTimeout(() => this.dismiss(toast.id), durationMs);
    }
    return toast.id;
  }

  dismiss(id: number): void {
    const remaining = this.toastsSubject.value.filter(toast => toast.id !== id);
    if (remaining.length !== this.toastsSubject.value.length) {
      this.toastsSubject.next(remaining);
    }
  }
}
