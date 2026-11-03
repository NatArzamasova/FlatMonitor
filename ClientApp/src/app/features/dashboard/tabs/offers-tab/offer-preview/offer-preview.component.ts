import { Component, ElementRef, Input, OnDestroy, ViewChild } from '@angular/core';

/**
 * Default size of the thumbnail: small enough to keep a table row about as high as a row of text. The API's photos
 * are about 400×300 (4:3). A host can enlarge it with `--offer-thumb-width` / `--offer-thumb-height` (the cards do).
 */
const THUMB_WIDTH = 40;
const THUMB_HEIGHT = 30;
/** Size of the photo in the popover. */
const IMAGE_WIDTH = 320;
const IMAGE_HEIGHT = 240;
/** Popover padding plus border on each side. */
const FRAME = 7;
/** Gap between the thumbnail and the popover, and the minimum distance to the window's edges. */
const GAP = 8;

let nextId = 0;

/**
 * The offer's title photo as a thumbnail that shows it larger while hovered or focused:
 * `<app-offer-preview [src]="url">`. Without a photo (`src` `null`, or it fails to load) a grey placeholder of the
 * same size keeps the column even. A tap (touch screens) focuses the thumbnail; tapping elsewhere or Escape closes
 * the large photo. Thumbnails load lazily, so a long table loads only the rows on screen; the large photo is the same
 * image, already in the browser's cache.
 *
 * The popover is `position: fixed` next to the thumbnail (right of it, left if there is no room), so the table's
 * scroll container does not clip it; it closes when anything scrolls, as it would no longer sit next to its row.
 */
@Component({
  selector: 'app-offer-preview',
  standalone: true,
  template: `
    @if (src && !thumbFailed) {
      <button #trigger type="button" class="offer-thumb-button"
        aria-label="Show the photo of the offer larger" [attr.aria-describedby]="open ? popoverId : null"
        (mouseenter)="show()" (mouseleave)="hide()" (focus)="show()" (blur)="hide()" (click)="show()"
        (keydown.escape)="hide()">
        <img class="offer-thumb" [src]="src" alt="" loading="lazy" [width]="thumbWidth" [height]="thumbHeight"
          (error)="thumbFailed = true" />
      </button>
    } @else {
      <span class="offer-thumb offer-thumb-empty" title="No photo">
        <svg aria-hidden="true" viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor"
          stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <rect x="3" y="5" width="18" height="14" rx="2" />
          <circle cx="9" cy="10" r="1.5" />
          <path d="m21 16-5-5-8 8" />
        </svg>
        <span class="visually-hidden">No photo</span>
      </span>
    }
    @if (open) {
      <div class="offer-preview-popover shadow" role="tooltip" [id]="popoverId"
        [style.top.px]="top" [style.left.px]="left">
        @if (failed) {
          <div class="offer-preview-message small text-body-secondary">No photo available</div>
        } @else {
          <img [src]="src" alt="Photo of the offer" [width]="imageWidth" [height]="imageHeight"
            [class.offer-preview-loading]="!loaded" (load)="loaded = true" (error)="failed = true" />
        }
      </div>
    }
  `,
  styles: [`
    :host {
      display: inline-block;
      vertical-align: middle;
      line-height: 0;
    }
    .offer-thumb-button {
      padding: 0;
      border: 0;
      background: none;
      border-radius: var(--bs-border-radius-sm);
      cursor: zoom-in;
    }
    .offer-thumb-button:focus-visible {
      outline: 2px solid var(--bs-focus-ring-color, var(--bs-primary));
      outline-offset: 2px;
    }
    .offer-thumb {
      display: block;
      width: var(--offer-thumb-width, ${THUMB_WIDTH}px);
      height: var(--offer-thumb-height, ${THUMB_HEIGHT}px);
      object-fit: cover;
      border-radius: var(--bs-border-radius-sm);
      background: var(--bs-tertiary-bg);
    }
    .offer-thumb-empty {
      display: flex;
      align-items: center;
      justify-content: center;
      color: var(--bs-secondary-color);
      opacity: 0.6;
    }
    .offer-preview-popover {
      position: fixed;
      z-index: 1080;
      padding: 6px;
      border: 1px solid var(--bs-border-color);
      border-radius: var(--bs-border-radius);
      background: var(--bs-body-bg);
      pointer-events: none;
      line-height: normal;
    }
    .offer-preview-popover img,
    .offer-preview-message {
      display: block;
      width: ${IMAGE_WIDTH}px;
      height: ${IMAGE_HEIGHT}px;
      border-radius: calc(var(--bs-border-radius) - 2px);
      background: var(--bs-tertiary-bg);
    }
    .offer-preview-popover img {
      object-fit: contain;
    }
    .offer-preview-loading {
      animation: offer-preview-pulse 1.2s ease-in-out infinite;
    }
    .offer-preview-message {
      display: flex;
      align-items: center;
      justify-content: center;
    }
    @keyframes offer-preview-pulse {
      50% { opacity: 0.5; }
    }
    @media (prefers-reduced-motion: reduce) {
      .offer-preview-loading { animation: none; }
    }
  `]
})
export class OfferPreviewComponent implements OnDestroy {
  /** The photo's URL; `null` shows the placeholder. */
  @Input({ required: true }) src: string | null = null;

  @ViewChild('trigger') private trigger?: ElementRef<HTMLButtonElement>;

  readonly popoverId = `offer-preview-${nextId++}`;
  readonly thumbWidth = THUMB_WIDTH;
  readonly thumbHeight = THUMB_HEIGHT;
  readonly imageWidth = IMAGE_WIDTH;
  readonly imageHeight = IMAGE_HEIGHT;

  open = false;
  thumbFailed = false;
  loaded = false;
  failed = false;
  top = 0;
  left = 0;

  /** Captured, so it also sees the table container's own scrolling, which does not bubble. */
  private readonly closeOnScroll = () => this.hide();

  show(): void {
    if (this.open || !this.trigger) {
      return;
    }
    this.place(this.trigger.nativeElement.getBoundingClientRect());
    this.open = true;
    document.addEventListener('scroll', this.closeOnScroll, { capture: true, passive: true });
  }

  hide(): void {
    if (!this.open) {
      return;
    }
    this.open = false;
    document.removeEventListener('scroll', this.closeOnScroll, { capture: true });
  }

  ngOnDestroy(): void {
    this.hide();
  }

  /** Right of the thumbnail, vertically centred on it; left of it without room on the right; always inside the window. */
  private place(rect: DOMRect): void {
    const width = IMAGE_WIDTH + 2 * FRAME;
    const height = IMAGE_HEIGHT + 2 * FRAME;

    let left = rect.right + GAP;
    if (left + width > window.innerWidth - GAP) {
      left = rect.left - GAP - width;
    }
    this.left = clamp(left, GAP, window.innerWidth - GAP - width);
    this.top = clamp(rect.top + rect.height / 2 - height / 2, GAP, window.innerHeight - GAP - height);
  }
}

/** `value` within `[min, max]`; `min` wins when the window is too small for both. */
function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(value, max));
}
