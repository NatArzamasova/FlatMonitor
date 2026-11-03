import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppHeaderComponent } from './app-header.component';

@Component({
  standalone: true,
  imports: [AppHeaderComponent],
  template: `
    <app-header>
      <nav headerNav aria-label="Test tabs">tabs</nav>
      <button headerActions type="button">Action</button>
    </app-header>
  `
})
class HostComponent {}

describe('AppHeaderComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  const header = (): HTMLElement => fixture.nativeElement.querySelector('app-header');

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideRouter([])]
    }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('is a sticky navbar landmark', () => {
    expect(header().getAttribute('role')).toBe('banner');
    expect(header().classList).toContain('navbar');
    expect(header().classList).toContain('sticky-top');
  });

  it('renders the brand as a link to the overview', () => {
    const brand = header().querySelector('a.navbar-brand') as HTMLAnchorElement;

    expect(brand.getAttribute('href')).toBe('/overview');
    expect(brand.textContent).toContain('FlatMonitor');
    expect(brand.querySelector('img')!.getAttribute('alt')).toBe('');
  });

  it('projects the navigation after the brand and the actions into the right-aligned group', () => {
    const nav = header().querySelector('nav[aria-label="Test tabs"]')!;
    const actions = header().querySelector('.ms-auto')!;

    expect(nav.previousElementSibling!.classList).toContain('navbar-brand');
    expect(actions.querySelector('button')!.textContent).toBe('Action');
  });
});
