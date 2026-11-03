import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EmptyStateComponent, EmptyStateIcon } from './empty-state.component';

@Component({
  standalone: true,
  imports: [EmptyStateComponent],
  template: `
    <app-empty-state [icon]="icon" heading="No offers match the filters" [message]="message">
      <button type="button" class="projected">Reset filters</button>
    </app-empty-state>
  `
})
class HostComponent {
  icon: EmptyStateIcon = 'search';
  message: string | null = 'Try fewer districts.';
}

describe('EmptyStateComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  const element = (): HTMLElement => fixture.nativeElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('shows the heading, the message and the projected actions as a status', () => {
    expect(element().querySelector('[role="status"]')).not.toBeNull();
    expect(element().querySelector('h2')!.textContent!.trim()).toBe('No offers match the filters');
    expect(element().querySelector('p')!.textContent!.trim()).toBe('Try fewer districts.');
    expect(element().querySelector('.empty-state-actions .projected')!.textContent).toBe('Reset filters');
  });

  it('hides the icon from screen readers and draws the chosen one', () => {
    const icon = (): SVGElement => element().querySelector('svg')!;
    expect(icon().getAttribute('aria-hidden')).toBe('true');
    expect(icon().querySelector('circle')).not.toBeNull();

    fixture.componentInstance.icon = 'inbox';
    fixture.detectChanges();
    expect(icon().querySelector('circle')).toBeNull();
  });

  it('leaves out the message when there is none', () => {
    fixture.componentInstance.message = null;
    fixture.detectChanges();

    expect(element().querySelector('p')).toBeNull();
  });
});
