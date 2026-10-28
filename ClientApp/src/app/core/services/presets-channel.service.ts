import { Injectable, OnDestroy } from '@angular/core';
import { EMPTY, fromEvent, Observable } from 'rxjs';
import { map } from 'rxjs/operators';

const CHANNEL_NAME = 'flatmonitor-filter-presets';

/**
 * Tells other browser tabs of this app that the filter presets changed, e.g. when the `/presets` page saves
 * while a dashboard tab is open. A `BroadcastChannel` does not deliver to its own sender, so `changed$` only
 * emits for changes made in another tab. Without `BroadcastChannel` support nothing is sent or received.
 */
@Injectable({
  providedIn: 'root'
})
export class PresetsChannel implements OnDestroy {
  private readonly channel = typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel(CHANNEL_NAME);

  readonly changed$: Observable<void> = this.channel
    ? fromEvent<MessageEvent>(this.channel, 'message').pipe(map(() => undefined))
    : EMPTY;

  notifyChanged(): void {
    this.channel?.postMessage('changed');
  }

  ngOnDestroy(): void {
    this.channel?.close();
  }
}
