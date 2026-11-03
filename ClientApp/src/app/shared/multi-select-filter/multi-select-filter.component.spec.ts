import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MultiSelectFilterComponent } from './multi-select-filter.component';

@Component({
  standalone: true,
  imports: [MultiSelectFilterComponent],
  template: `
    <app-multi-select-filter
      [label]="label"
      inputId="roomsSelect"
      [options]="options"
      [(selected)]="selected"
      (changed)="onChanged()"
      [minWidth]="110"
    ></app-multi-select-filter>
    <button type="button" id="outside">Outside</button>
  `
})
class HostComponent {
  label = 'Rooms:';
  options: (number | string)[] = [1, 2.5, 3];
  selected: (number | string)[] = [];
  selectedWhenChanged: (number | string)[] | undefined;
  changedCount = 0;

  onChanged(): void {
    this.changedCount++;
    this.selectedWhenChanged = [...this.selected];
  }
}

describe('MultiSelectFilterComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  const element = (): HTMLElement => fixture.nativeElement;
  const toggle = (): HTMLButtonElement => element().querySelector('#roomsSelect')!;
  const menu = (): HTMLElement | null => element().querySelector('#roomsSelect-menu');
  const checkboxes = (): HTMLInputElement[] => Array.from(element().querySelectorAll('.form-check-input'));
  const optionLabels = (): string[] =>
    Array.from(element().querySelectorAll('.multi-select-option-label')).map(label => label.textContent!.trim());
  const menuButton = (text: string): HTMLButtonElement =>
    Array.from(menu()!.querySelectorAll('button')).find(button => button.textContent!.trim() === text)!;
  const search = (): HTMLInputElement | null => element().querySelector('input[type="search"]');

  const render = async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const openMenu = async () => {
    toggle().click();
    await render();
  };

  const check = async (text: string) => {
    const index = optionLabels().indexOf(text);
    checkboxes()[index].click();
    await render();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    await render();
  });

  describe('closed', () => {
    it('renders a label pointing at a compact toggle button', () => {
      const label = element().querySelector('label') as HTMLLabelElement;

      expect(label.textContent!.trim()).toBe('Rooms:');
      expect(label.htmlFor).toBe('roomsSelect');
      expect(label.classList).toContain('filter-label');
      expect(toggle().tagName).toBe('BUTTON');
      expect(toggle().classList).toContain('form-select-sm');
      expect(toggle().style.minWidth).toBe('110px');
      expect(toggle().getAttribute('aria-expanded')).toBe('false');
      expect(menu()).toBeNull();
    });

    it('highlights the toggle while the filter is active', async () => {
      expect(toggle().classList).not.toContain('is-filtering');

      host.selected = [3];
      await render();

      expect(toggle().classList).toContain('is-filtering');
    });

    it('is named by its label and its summary', () => {
      expect(toggle().getAttribute('aria-labelledby')).toBe('roomsSelect-label roomsSelect');
    });

    it('summarises the selection: All, the one option, or the count', async () => {
      expect(toggle().textContent!.trim()).toBe('All');

      host.selected = [2.5];
      await render();
      expect(toggle().textContent!.trim()).toBe('2,5');

      host.selected = [1, 3];
      await render();
      expect(toggle().textContent!.trim()).toBe('2 selected');
    });

    it('defaults the minimum width to 140px', () => {
      const standalone = TestBed.createComponent(MultiSelectFilterComponent<string>);
      standalone.componentRef.setInput('label', 'District:');
      standalone.componentRef.setInput('inputId', 'districtSelect');
      standalone.componentRef.setInput('options', ['Mitte']);
      standalone.detectChanges();

      expect((standalone.nativeElement.querySelector('#districtSelect') as HTMLButtonElement).style.minWidth).toBe('140px');
    });
  });

  describe('open', () => {
    it('opens a menu with one checkbox per option, showing the current selection', async () => {
      host.selected = [1];
      await render();
      await openMenu();

      expect(toggle().getAttribute('aria-expanded')).toBe('true');
      expect(toggle().getAttribute('aria-controls')).toBe('roomsSelect-menu');
      expect(optionLabels()).toEqual(['1', '2,5', '3']);
      expect(checkboxes().map(box => box.checked)).toEqual([true, false, false]);
      expect(menu()!.querySelector('[role="group"]')!.getAttribute('aria-label')).toBe('Rooms');
    });

    it('makes the whole row clickable and highlights the ticked rows', async () => {
      await openMenu();
      const rows = (): HTMLLabelElement[] => Array.from(menu()!.querySelectorAll('.multi-select-option'));

      rows()[1].click();
      await render();

      expect(host.selected).toEqual([2.5]);
      expect(rows().map(row => row.classList.contains('is-selected'))).toEqual([false, true, false]);
      expect(menu()!.querySelector('.multi-select-count')!.textContent!.trim()).toBe('1 selected');
    });

    it('updates the bound selection with the original values in option order, then emits changed', async () => {
      await openMenu();
      await check('3');
      await check('1');

      expect(host.selected).toEqual([1, 3]);
      expect(host.selectedWhenChanged).toEqual([1, 3]);
      expect(host.changedCount).toBe(2);
      expect(menu()).not.toBeNull();
    });

    it('unticks an option', async () => {
      host.selected = [1, 3];
      await render();
      await openMenu();
      await check('1');

      expect(host.selected).toEqual([3]);
    });

    it('selects all options and clears the selection', async () => {
      await openMenu();

      menuButton('Select all').click();
      await render();
      expect(host.selected).toEqual([1, 2.5, 3]);

      menuButton('Clear').click();
      await render();
      expect(host.selected).toEqual([]);
      expect(menuButton('Clear').disabled).toBe(true);
    });

    it('closes on a second click on the toggle, on Escape and on a click outside', async () => {
      await openMenu();
      await openMenu();
      expect(menu()).toBeNull();

      await openMenu();
      menu()!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      await render();
      expect(menu()).toBeNull();
      expect(document.activeElement).toBe(toggle());

      await openMenu();
      (element().querySelector('#outside') as HTMLButtonElement).click();
      await render();
      expect(menu()).toBeNull();
    });

    it('has no search box for short lists', async () => {
      await openMenu();

      expect(search()).toBeNull();
    });
  });

  describe('long lists', () => {
    beforeEach(async () => {
      host.label = 'District:';
      host.options = ['Durlach', 'Mühlburg', 'Nordstadt', 'Nordweststadt', 'Oststadt', 'Südstadt', 'Weststadt', 'Innenstadt-Ost', 'Innenstadt-West'];
      await render();
      await openMenu();
    });

    const typeSearch = async (text: string) => {
      search()!.value = text;
      search()!.dispatchEvent(new Event('input'));
      await render();
    };

    it('get a focused search box', () => {
      expect(search()).not.toBeNull();
      expect(search()!.getAttribute('aria-label')).toBe('Search District');
      expect(document.activeElement).toBe(search());
    });

    it('filter the options by the search text, ignoring case', async () => {
      await typeSearch('NORD');

      expect(optionLabels()).toEqual(['Nordstadt', 'Nordweststadt']);
    });

    it('keep the selection of hidden options and select only the matching ones with Select all', async () => {
      await check('Durlach');
      await typeSearch('innenstadt');
      menuButton('Select all').click();
      await render();

      expect(host.selected).toEqual(['Durlach', 'Innenstadt-Ost', 'Innenstadt-West']);
    });

    it('say so when nothing matches', async () => {
      await typeSearch('xyz');

      expect(optionLabels()).toEqual([]);
      expect(menu()!.textContent).toContain('No matches');
      expect(menuButton('Select all').disabled).toBe(true);
    });

    it('start with an empty search when opened again', async () => {
      await typeSearch('nord');
      await openMenu();
      await openMenu();

      expect(search()!.value).toBe('');
      expect(optionLabels().length).toBe(9);
    });
  });
});
