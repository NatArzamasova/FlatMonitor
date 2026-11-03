import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoadingSkeletonComponent } from './loading-skeleton.component';

describe('LoadingSkeletonComponent', () => {
  let fixture: ComponentFixture<LoadingSkeletonComponent>;

  const element = (): HTMLElement => fixture.nativeElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [LoadingSkeletonComponent] }).compileComponents();
    fixture = TestBed.createComponent(LoadingSkeletonComponent);
  });

  it('announces loading to screen readers and hides the placeholders from them', () => {
    fixture.detectChanges();

    const status = element().querySelector('[role="status"]')!;
    expect(status.querySelector('.visually-hidden')!.textContent).toBe('Loading…');
    expect(Array.from(status.querySelectorAll('.card, .table-card')).every(box => box.getAttribute('aria-hidden') === 'true'))
      .toBe(true);
  });

  it('draws a table with the given rows and columns', () => {
    fixture.componentRef.setInput('rows', 3);
    fixture.componentRef.setInput('columns', 6);
    fixture.detectChanges();

    expect(element().querySelectorAll('thead th').length).toBe(6);
    expect(element().querySelectorAll('tbody tr').length).toBe(3);
    expect(element().querySelector('.table-card-fill')).not.toBeNull();
  });

  it('draws a chart box instead of the table for the chart variant', () => {
    fixture.componentRef.setInput('variant', 'chart');
    fixture.detectChanges();

    expect(element().querySelector('.skeleton-chart')).not.toBeNull();
    expect(element().querySelector('table')).toBeNull();
  });

  it('adds card placeholders below md and hides the table there with cardsOnSmallScreens', () => {
    fixture.componentRef.setInput('rows', 8);
    fixture.componentRef.setInput('cardsOnSmallScreens', true);
    fixture.detectChanges();

    const cards = fixture.nativeElement.querySelector('.skeleton-cards') as HTMLElement;
    const table = fixture.nativeElement.querySelector('.table-card') as HTMLElement;
    expect(cards.classList).toContain('d-md-none');
    expect(cards.getAttribute('aria-hidden')).toBe('true');
    expect(cards.querySelectorAll('.card').length).toBe(4);
    expect(table.classList).toContain('d-none');
    expect(table.classList).toContain('d-md-block');
  });

  it('shows only the table without cardsOnSmallScreens', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.skeleton-cards')).toBeNull();
    expect(fixture.nativeElement.querySelector('.table-card').classList).not.toContain('d-none');
  });
});
