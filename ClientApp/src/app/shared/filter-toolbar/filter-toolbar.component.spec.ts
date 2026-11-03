import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FilterToolbarComponent } from './filter-toolbar.component';
import { FilterChip } from '../../core/utils/filter-chips.utils';

@Component({
  standalone: true,
  imports: [FilterToolbarComponent],
  template: `
    <app-filter-toolbar [chips]="chips" (chipRemoved)="removed.push($event)" (reset)="resets = resets + 1">
      <div data-filter-row="test">filter controls</div>
    </app-filter-toolbar>
  `
})
class HostComponent {
  chips: FilterChip[] = [];
  removed: FilterChip[] = [];
  resets = 0;
}

describe('FilterToolbarComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  const element = (): HTMLElement => fixture.nativeElement;
  const card = (): HTMLElement => element().querySelector('.filter-toolbar')!;
  const toggle = (): HTMLButtonElement => element().querySelector('.filter-toolbar-toggle')!;
  const chipTexts = (): string[] =>
    Array.from(element().querySelectorAll('.filter-chip')).map(chip => chip.firstChild!.textContent!.trim());

  const setChips = (chips: FilterChip[]) => {
    host.chips = chips;
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('projects the filter rows into a card', () => {
    expect(card().classList).toContain('card');
    expect(card().querySelector('[data-filter-row="test"]')).not.toBeNull();
  });

  it('shows no chips and no reset link without active filters', () => {
    expect(element().querySelector('.filter-chip')).toBeNull();
    expect(element().querySelector('.filter-reset')).toBeNull();
  });

  it('lists the active filters as chips that can be removed one by one', () => {
    const mitte: FilterChip = { kind: 'district', value: 'Mitte', label: 'Mitte' };
    setChips([mitte, { kind: 'rooms', value: 2, label: '2 rooms' }]);

    expect(chipTexts()).toEqual(['Mitte', '2 rooms']);
    expect(element().querySelector('ul')!.getAttribute('aria-label')).toBe('Active filters');

    const remove = element().querySelector('.filter-chip-remove') as HTMLButtonElement;
    expect(remove.getAttribute('aria-label')).toBe('Remove filter Mitte');
    remove.click();

    expect(host.removed).toEqual([mitte]);
  });

  it('emits reset from the reset link', () => {
    setChips([{ kind: 'rooms', value: 2, label: '2 rooms' }]);

    (element().querySelector('.filter-reset') as HTMLButtonElement).click();

    expect(host.resets).toBe(1);
  });

  it('collapses the card on small screens behind a toggle with the filter count', () => {
    setChips([{ kind: 'rooms', value: 2, label: '2 rooms' }, { kind: 'rooms', value: 3, label: '3 rooms' }]);

    expect(card().classList).toContain('d-none');
    expect(card().classList).toContain('d-md-block');
    expect(toggle().classList).toContain('d-md-none');
    expect(toggle().getAttribute('aria-controls')).toBe(card().id);
    expect(toggle().getAttribute('aria-expanded')).toBe('false');
    expect(toggle().textContent).toContain('2');

    toggle().click();
    fixture.detectChanges();

    expect(card().classList).not.toContain('d-none');
    expect(toggle().getAttribute('aria-expanded')).toBe('true');
  });

  it('gives each toolbar its own card id', () => {
    const other = TestBed.createComponent(FilterToolbarComponent);
    other.detectChanges();

    expect(other.nativeElement.querySelector('.filter-toolbar').id).not.toBe(card().id);
  });
});
