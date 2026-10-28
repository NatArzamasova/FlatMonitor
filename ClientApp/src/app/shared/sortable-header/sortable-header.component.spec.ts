import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SortableHeaderComponent } from './sortable-header.component';
import { TableSort } from '../../core/utils/table-sort';

interface Row {
  name: string;
  price: number;
}

@Component({
  standalone: true,
  imports: [SortableHeaderComponent],
  template: `
    <table>
      <thead>
        <tr>
          <th appSortableHeader class="text-end" [sort]="sort" column="name" sortLabel="Name" (sortRequested)="requested.push($event)">Name</th>
          <th appSortableHeader [sort]="sort" column="price" sortLabel="Price per Square Meter" (sortRequested)="requested.push($event)">Price/m²</th>
        </tr>
      </thead>
    </table>
  `
})
class HostComponent {
  sort = new TableSort<Row, 'name' | 'price'>();
  requested: string[] = [];
}

describe('SortableHeaderComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  const headers = (): HTMLTableCellElement[] => Array.from(fixture.nativeElement.querySelectorAll('th'));
  const button = (index: number): HTMLButtonElement => headers()[index].querySelector('button')!;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders the projected label inside a sort button with an accessible name', () => {
    expect(button(0).textContent!.trim()).toBe('Name');
    expect(button(0).getAttribute('aria-label')).toBe('Sort by Name');
    expect(button(1).getAttribute('aria-label')).toBe('Sort by Price per Square Meter');
  });

  it('keeps classes set on the header cell', () => {
    expect(headers()[0].classList.contains('text-end')).toBe(true);
  });

  it('emits the column when clicked, without changing the sort itself', () => {
    button(1).click();

    expect(host.requested).toEqual(['price']);
    expect(host.sort.column).toBeNull();
  });

  it('shows the sort state of its own column only', () => {
    host.sort.toggle('price');
    fixture.detectChanges();

    expect(headers()[1].getAttribute('aria-sort')).toBe('ascending');
    expect(button(1).textContent).toContain('↑');
    expect(headers()[0].hasAttribute('aria-sort')).toBe(false);
    expect(button(0).textContent).not.toContain('↑');

    host.sort.toggle('price');
    fixture.detectChanges();

    expect(headers()[1].getAttribute('aria-sort')).toBe('descending');
    expect(button(1).textContent).toContain('↓');
  });
});
