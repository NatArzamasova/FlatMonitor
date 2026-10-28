import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Observable, of, throwError } from 'rxjs';
import { FilterPresetBarComponent } from './filter-preset-bar.component';
import { FilterPresetsService } from '../../core/services/filter-presets.service';
import { FilterPreset } from '../../core/models/filter-preset.model';

@Component({
  standalone: true,
  imports: [FilterPresetBarComponent],
  template: `<app-filter-preset-bar [selected]="selected"></app-filter-preset-bar>`
})
class HostComponent {
  selected: string[] = ['Durlach'];
}

describe('FilterPresetBarComponent', () => {
  const east: FilterPreset = { id: 1, name: 'East', districts: ['Durlach'], isDefault: true, updatedAtUtc: '' };
  const west: FilterPreset = { id: 2, name: 'West', districts: ['Weststadt'], isDefault: false, updatedAtUtc: '' };

  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;
  let presets$: BehaviorSubject<FilterPreset[]>;
  let activePreset$: BehaviorSubject<FilterPreset | null>;
  let service: jasmine.SpyObj<FilterPresetsService>;

  const element = (): HTMLElement => fixture.nativeElement;
  const select = (): HTMLSelectElement => element().querySelector('select')!;
  const button = (text: string): HTMLButtonElement =>
    Array.from(element().querySelectorAll('button')).find(b => b.textContent!.trim() === text)!;
  const nameInput = (): HTMLInputElement => element().querySelector('input[type="text"]')!;

  const render = async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const typeName = async (name: string) => {
    nameInput().value = name;
    nameInput().dispatchEvent(new Event('input'));
    await render();
  };

  beforeEach(async () => {
    presets$ = new BehaviorSubject<FilterPreset[]>([east, west]);
    activePreset$ = new BehaviorSubject<FilterPreset | null>(east);
    service = jasmine.createSpyObj<FilterPresetsService>(
      'FilterPresetsService',
      ['selectPreset', 'create', 'update', 'delete', 'setDefault', 'clearDefault'],
      { presets$: presets$ as Observable<FilterPreset[]>, activePreset$: activePreset$ as Observable<FilterPreset | null> }
    );

    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [{ provide: FilterPresetsService, useValue: service }]
    }).compileComponents();

    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    await render();
  });

  describe('dropdown', () => {
    it('lists "none" and all presets, marking the default with a star', () => {
      const labels = Array.from(select().options).map(option => option.textContent!.trim());

      expect(labels).toEqual(['— none —', '★ East', 'West']);
    });

    it('shows the active preset as chosen', () => {
      expect(select().selectedOptions[0].textContent!.trim()).toBe('★ East');
    });

    it('follows activePreset$', async () => {
      activePreset$.next(west);
      await render();

      expect(select().selectedOptions[0].textContent!.trim()).toBe('West');
    });

    it('selects the chosen preset through the service', async () => {
      select().value = select().options[2].value;
      select().dispatchEvent(new Event('change'));
      await render();

      expect(service.selectPreset).toHaveBeenCalledWith(2);
    });

    it('selects null for "none"', async () => {
      select().value = select().options[0].value;
      select().dispatchEvent(new Event('change'));
      await render();

      expect(service.selectPreset).toHaveBeenCalledWith(null);
    });
  });

  describe('Save', () => {
    it('is disabled while the selection equals the active preset', () => {
      expect(button('Save').disabled).toBeTrue();
      expect(element().textContent).not.toContain('modified');
    });

    it('is enabled and marks the preset as modified when the selection differs', async () => {
      host.selected = ['Durlach', 'Hagsfeld'];
      await render();

      expect(button('Save').disabled).toBeFalse();
      expect(element().textContent).toContain('modified');
    });

    it('is disabled without an active preset', async () => {
      activePreset$.next(null);
      host.selected = ['Hagsfeld'];
      await render();

      expect(button('Save').disabled).toBeTrue();
    });

    it('overwrites the active preset with the current selection', async () => {
      service.update.and.returnValue(of({ ...east, districts: ['Durlach', 'Hagsfeld'] }));
      host.selected = ['Durlach', 'Hagsfeld'];
      await render();

      button('Save').click();
      await render();

      expect(service.update).toHaveBeenCalledWith(1, { name: 'East', districts: ['Durlach', 'Hagsfeld'] });
    });
  });

  describe('Save as new', () => {
    it('creates a preset from the current selection and selects it', async () => {
      const created: FilterPreset = { id: 3, name: 'North', districts: ['Hagsfeld'], isDefault: false, updatedAtUtc: '' };
      service.create.and.returnValue(of(created));
      host.selected = ['Hagsfeld'];
      await render();

      button('Save as new…').click();
      await render();
      await typeName('  North ');
      button('Create').click();
      await render();

      expect(service.create).toHaveBeenCalledWith({ name: 'North', districts: ['Hagsfeld'] });
      expect(service.selectPreset).toHaveBeenCalledWith(3);
      expect(nameInput()).toBeNull();
    });

    it('submits with Enter and cancels with Escape', async () => {
      service.create.and.returnValue(of(west));
      button('Save as new…').click();
      await render();
      await typeName('West');

      nameInput().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
      await render();
      expect(service.create).toHaveBeenCalledTimes(1);

      button('Save as new…').click();
      await render();
      nameInput().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
      await render();
      expect(nameInput()).toBeNull();
    });

    it('disables Create for a blank name', async () => {
      button('Save as new…').click();
      await render();
      await typeName('   ');

      expect(button('Create').disabled).toBeTrue();
    });

    it('shows the API message on a conflict and keeps the form open', async () => {
      service.create.and.returnValue(throwError(() => new HttpErrorResponse({
        status: 409,
        error: { success: false, message: "A filter preset named 'East' already exists" }
      })));
      button('Save as new…').click();
      await render();
      await typeName('East');

      button('Create').click();
      await render();

      expect(element().querySelector('[role="alert"]')!.textContent).toContain("A filter preset named 'East' already exists");
      expect(nameInput()).not.toBeNull();
      expect(service.selectPreset).not.toHaveBeenCalled();
    });

    it('shows a generic message when the backend is unreachable', async () => {
      service.create.and.returnValue(throwError(() => new HttpErrorResponse({ status: 0 })));
      button('Save as new…').click();
      await render();
      await typeName('North');

      button('Create').click();
      await render();

      expect(element().querySelector('[role="alert"]')!.textContent).toContain('Could not save the preset');
    });
  });

  it('leaves renaming, the default and deleting to the presets page', () => {
    const labels = Array.from(element().querySelectorAll('button')).map(b => b.textContent!.trim());

    expect(labels).toEqual(['Save', 'Save as new…']);
  });

  it('shows a renamed active preset without activePreset$ re-emitting', async () => {
    presets$.next([{ ...east, name: 'Far east' }, west]);
    await render();

    expect(select().selectedOptions[0].textContent!.trim()).toBe('★ Far east');
  });
});
