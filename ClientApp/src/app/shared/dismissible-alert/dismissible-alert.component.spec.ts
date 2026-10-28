import { Component } from '@angular/core';
import { NgIf } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DismissibleAlertComponent } from './dismissible-alert.component';

@Component({
  standalone: true,
  imports: [NgIf, DismissibleAlertComponent],
  template: `
    <app-dismissible-alert *ngIf="message" [type]="type" (dismissed)="message = null">✓ {{ message }}</app-dismissible-alert>
  `
})
class HostComponent {
  message: string | null = 'Scrape job queued';
  type: 'danger' | 'info' = 'info';
}

describe('DismissibleAlertComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  const alert = (): HTMLElement | null => fixture.nativeElement.querySelector('[role="alert"]');

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders the projected message in a dismissible Bootstrap alert of the given type', () => {
    expect(alert()!.textContent!.trim()).toBe('✓ Scrape job queued');
    expect(Array.from(alert()!.classList).sort()).toEqual(['alert', 'alert-dismissible', 'alert-info', 'fade', 'show']);
  });

  it('switches the alert type', () => {
    host.type = 'danger';
    fixture.detectChanges();

    expect(alert()!.classList).toContain('alert-danger');
    expect(alert()!.classList).not.toContain('alert-info');
  });

  it('emits dismissed when the close button is clicked', () => {
    const close = alert()!.querySelector('button.btn-close') as HTMLButtonElement;
    expect(close.getAttribute('aria-label')).toBe('Close');

    close.click();
    fixture.detectChanges();

    expect(host.message).toBeNull();
    expect(alert()).toBeNull();
  });

  it('defaults to a danger alert', () => {
    const standalone = TestBed.createComponent(DismissibleAlertComponent);
    standalone.detectChanges();

    expect((standalone.nativeElement.querySelector('[role="alert"]') as HTMLElement).classList).toContain('alert-danger');
  });
});
