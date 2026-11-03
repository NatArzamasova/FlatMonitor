import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OfferPreviewComponent } from './offer-preview.component';

describe('OfferPreviewComponent', () => {
  const url = '/api/flats/OhneMakler/42/preview';
  let fixture: ComponentFixture<OfferPreviewComponent>;

  const element = (): HTMLElement => fixture.nativeElement;
  const trigger = (): HTMLButtonElement => element().querySelector('.offer-thumb-button')!;
  const thumbnail = (): HTMLImageElement | null => element().querySelector('img.offer-thumb');
  const placeholder = (): HTMLElement | null => element().querySelector('.offer-thumb-empty');
  const popover = (): HTMLElement | null => element().querySelector('.offer-preview-popover');
  const largeImage = (): HTMLImageElement | null => element().querySelector('.offer-preview-popover img');
  const fire = (target: EventTarget, type: string) => {
    target.dispatchEvent(new Event(type));
    fixture.detectChanges();
  };
  const render = (src: string | null) => {
    fixture.componentRef.setInput('src', src);
    fixture.detectChanges();
  };
  /** Puts the thumbnail at `left`/`top` in the window (the popover is placed from its rectangle). */
  const placeTrigger = (left: number, top: number) =>
    spyOn(trigger(), 'getBoundingClientRect').and.returnValue(new DOMRect(left, top, 40, 30));

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [OfferPreviewComponent] }).compileComponents();
    fixture = TestBed.createComponent(OfferPreviewComponent);
  });

  afterEach(() => fixture.destroy());

  describe('with a photo', () => {
    beforeEach(() => render(url));

    it('shows a lazily loaded thumbnail in a button with an accessible name', () => {
      expect(trigger().getAttribute('aria-label')).toBe('Show the photo of the offer larger');
      expect(thumbnail()!.getAttribute('src')).toBe(url);
      expect(thumbnail()!.getAttribute('loading')).toBe('lazy');
      expect(thumbnail()!.alt).toBe('');
      expect(thumbnail()!.width).toBe(40);
      expect(thumbnail()!.height).toBe(30);
      expect(placeholder()).toBeNull();
      expect(popover()).toBeNull();
    });

    it('shows the photo larger while the thumbnail is hovered', () => {
      fire(trigger(), 'mouseenter');

      expect(popover()!.getAttribute('role')).toBe('tooltip');
      expect(trigger().getAttribute('aria-describedby')).toBe(popover()!.id);
      expect(largeImage()!.getAttribute('src')).toBe(url);
      expect(largeImage()!.alt).toBe('Photo of the offer');
      expect(largeImage()!.width).toBe(320);

      fire(trigger(), 'mouseleave');

      expect(popover()).toBeNull();
      expect(trigger().hasAttribute('aria-describedby')).toBeFalse();
    });

    it('shows the photo larger while the thumbnail has the focus (keyboard, a tap) and closes it on Escape', () => {
      fire(trigger(), 'focus');
      expect(popover()).not.toBeNull();

      trigger().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
      fixture.detectChanges();
      expect(popover()).toBeNull();

      fire(trigger(), 'focus');
      fire(trigger(), 'blur');
      expect(popover()).toBeNull();
    });

    it('keeps the large photo open when the hovered thumbnail is also clicked', () => {
      fire(trigger(), 'mouseenter');
      fire(trigger(), 'focus');
      fire(trigger(), 'click');

      expect(popover()).not.toBeNull();
    });

    it('pulses the large photo\'s placeholder until it has loaded', () => {
      fire(trigger(), 'mouseenter');
      expect(largeImage()!.classList).toContain('offer-preview-loading');

      fire(largeImage()!, 'load');

      expect(largeImage()!.classList).not.toContain('offer-preview-loading');
    });

    it('says so when the large photo cannot be loaded', () => {
      fire(trigger(), 'mouseenter');

      fire(largeImage()!, 'error');

      expect(largeImage()).toBeNull();
      expect(popover()!.textContent!.trim()).toBe('No photo available');
    });

    it('shows the placeholder when the thumbnail cannot be loaded', () => {
      fire(thumbnail()!, 'error');

      expect(thumbnail()).toBeNull();
      expect(placeholder()).not.toBeNull();
    });

    it('closes the large photo when anything scrolls, e.g. the table', () => {
      fire(trigger(), 'mouseenter');

      fire(document.body, 'scroll');

      expect(popover()).toBeNull();
    });

    it('opens right of the thumbnail, vertically centred on it', () => {
      // The thumbnail's centre in the middle of the window (the headless test browser's is small)
      const middle = Math.round(window.innerHeight / 2);
      placeTrigger(100, middle - 15);

      fire(trigger(), 'mouseenter');

      // 334 × 254: the 320 × 240 photo with padding and border
      expect(popover()!.style.left).toBe(`${100 + 40 + 8}px`);
      expect(popover()!.style.top).toBe(`${middle - 127}px`);
    });

    it('opens left of the thumbnail without room on the right, and stays inside the window', () => {
      placeTrigger(window.innerWidth - 50, 0);

      fire(trigger(), 'mouseenter');

      expect(popover()!.style.left).toBe(`${window.innerWidth - 50 - 8 - 334}px`);
      expect(popover()!.style.top).toBe('8px');
    });
  });

  describe('without a photo', () => {
    beforeEach(() => render(null));

    it('shows a placeholder of the same size, named for screen readers, and nothing to hover', () => {
      expect(placeholder()!.textContent!.trim()).toBe('No photo');
      expect(placeholder()!.title).toBe('No photo');
      expect(thumbnail()).toBeNull();
      expect(element().querySelector('button')).toBeNull();
    });
  });
});
