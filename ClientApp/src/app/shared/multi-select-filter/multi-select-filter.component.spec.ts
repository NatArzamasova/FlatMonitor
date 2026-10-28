import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MultiSelectFilterComponent } from './multi-select-filter.component';

@Component({
  standalone: true,
  imports: [MultiSelectFilterComponent],
  template: `
    <app-multi-select-filter
      label="Rooms:"
      inputId="roomsSelect"
      [options]="options"
      [(selected)]="selected"
      (changed)="onChanged()"
      [minWidth]="110"
    ></app-multi-select-filter>
  `
})
class HostComponent {
  options = [1, 2.5, 3];
  selected: number[] = [];
  selectedWhenChanged: number[] | undefined;

  onChanged(): void {
    this.selectedWhenChanged = [...this.selected];
  }
}

describe('MultiSelectFilterComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  const select = (): HTMLSelectElement => fixture.nativeElement.querySelector('select');
  const visibleOptions = (): HTMLOptionElement[] => Array.from(select().options).filter(option => !option.hidden);

  const render = async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const choose = async (...texts: string[]) => {
    for (const option of visibleOptions()) {
      option.selected = texts.includes(option.textContent!.trim());
    }
    select().dispatchEvent(new Event('change'));
    await render();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    await render();
  });

  it('renders a labelled compact multi-select', () => {
    const label = fixture.nativeElement.querySelector('label') as HTMLLabelElement;

    expect(label.textContent!.trim()).toBe('Rooms:');
    expect(label.htmlFor).toBe('roomsSelect');
    expect(label.classList).toContain('filter-label');
    expect(select().id).toBe('roomsSelect');
    expect(select().multiple).toBe(true);
    expect(select().size).toBe(1);
    expect(select().style.minWidth).toBe('110px');
  });

  it('lists the options after a hidden placeholder', () => {
    expect(select().options[0].hidden).toBe(true);
    expect(select().options[0].textContent!.trim()).toBe('Select...');
    expect(visibleOptions().map(option => option.textContent!.trim())).toEqual(['1', '2.5', '3']);
  });

  it('updates the bound selection with the original option values and then emits changed', async () => {
    await choose('2.5', '3');

    expect(host.selected).toEqual([2.5, 3]);
    expect(host.selectedWhenChanged).toEqual([2.5, 3]);
  });

  it('shows a selection set by the host', async () => {
    host.selected = [1];
    await render();

    expect(visibleOptions().map(option => option.selected)).toEqual([true, false, false]);
  });

  it('defaults the minimum width to 140px', () => {
    const standalone = TestBed.createComponent(MultiSelectFilterComponent<string>);
    standalone.componentRef.setInput('label', 'District:');
    standalone.componentRef.setInput('inputId', 'districtSelect');
    standalone.componentRef.setInput('options', ['Mitte']);
    standalone.detectChanges();

    expect((standalone.nativeElement.querySelector('select') as HTMLSelectElement).style.minWidth).toBe('140px');
  });
});
