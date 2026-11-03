import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StatTileComponent } from './stat-tile.component';

describe('StatTileComponent', () => {
  let fixture: ComponentFixture<StatTileComponent>;

  const text = (selector: string): string | undefined =>
    (fixture.nativeElement.querySelector(selector) as HTMLElement | null)?.textContent!.trim();

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StatTileComponent] }).compileComponents();
    fixture = TestBed.createComponent(StatTileComponent);
    fixture.componentRef.setInput('label', 'Avg. price / m²');
    fixture.componentRef.setInput('value', '€4,250');
  });

  it('shows the label and the value in a card', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.card')).not.toBeNull();
    expect(text('.stat-tile-label')).toBe('Avg. price / m²');
    expect(text('.stat-tile-value')).toBe('€4,250');
    expect(fixture.nativeElement.querySelector('.stat-tile-value').title).toBe('€4,250');
  });

  it('shows the hint only when there is one', () => {
    fixture.detectChanges();
    expect(text('.stat-tile-hint')).toBeUndefined();

    fixture.componentRef.setInput('hint', 'in 12 districts');
    fixture.detectChanges();
    expect(text('.stat-tile-hint')).toBe('in 12 districts');
  });
});
