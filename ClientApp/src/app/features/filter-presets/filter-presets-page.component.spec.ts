import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { FilterPresetsPageComponent } from './filter-presets-page.component';
import { FilterPresetsService } from '../../core/services/filter-presets.service';
import { DistrictsService } from '../../core/services/districts.service';
import { FilterPreset } from '../../core/models/filter-preset.model';
import { createFilterPresetsServiceStub, filterPreset, FilterPresetsServiceStub } from '../../core/testing/filter-presets.testing';

describe('FilterPresetsPageComponent', () => {
  const east = filterPreset(1, 'East', ['Durlach'], true);
  const west = filterPreset(2, 'West', []);

  let fixture: ComponentFixture<FilterPresetsPageComponent>;
  let component: FilterPresetsPageComponent;
  let presets: FilterPresetsServiceStub;
  let districts$: BehaviorSubject<string[]>;

  const element = (): HTMLElement => fixture.nativeElement;
  const buttons = (): HTMLButtonElement[] => Array.from(element().querySelectorAll('button'));
  const button = (text: string): HTMLButtonElement => buttons().find(b => b.textContent!.trim() === text)!;
  const listItems = (): string[] =>
    Array.from(element().querySelectorAll('[data-test="preset-list"] button'))
      .map(b => Array.from(b.children).map(child => child.textContent!.trim()).join(' | '));
  const nameInput = (): HTMLInputElement => element().querySelector('#presetName')!;
  const checkbox = (district: string): HTMLInputElement =>
    Array.from(element().querySelectorAll<HTMLInputElement>('input[type="checkbox"]'))
      .find(input => input.nextElementSibling!.textContent!.trim() === district)!;

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

  const tick = async (district: string) => {
    checkbox(district).click();
    await render();
  };

  const open = async (preset: FilterPreset) => {
    buttons().find(b => b.textContent!.includes(preset.name))!.click();
    await render();
  };

  beforeEach(async () => {
    presets = createFilterPresetsServiceStub([east, west]);
    districts$ = new BehaviorSubject<string[]>(['Durlach', 'Hagsfeld', 'Weststadt']);

    await TestBed.configureTestingModule({
      imports: [FilterPresetsPageComponent],
      providers: [
        provideRouter([]),
        { provide: FilterPresetsService, useValue: presets.service },
        { provide: DistrictsService, useValue: { districts$ } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FilterPresetsPageComponent);
    component = fixture.componentInstance;
    await render();
  });

  describe('list', () => {
    it('lists the presets with the default starred and their district count', () => {
      expect(listItems()).toEqual(['★ East | 1 districts', 'West | all districts']);
    });

    it('says when there are no presets', async () => {
      presets.presets$.next([]);
      await render();

      expect(element().textContent).toContain('No presets yet.');
    });

    it('does not touch the dashboard\'s active preset', async () => {
      await open(west);

      expect(presets.service.selectPreset).not.toHaveBeenCalled();
    });
  });

  describe('editing', () => {
    it('shows the chosen preset in the editor', async () => {
      await open(east);

      expect(nameInput().value).toBe('East');
      expect(checkbox('Durlach').checked).toBeTrue();
      expect(checkbox('Hagsfeld').checked).toBeFalse();
    });

    it('enables Save only after a change', async () => {
      await open(east);
      expect(button('Save').disabled).toBeTrue();

      await tick('Hagsfeld');

      expect(button('Save').disabled).toBeFalse();
    });

    it('saves the name and the ticked districts', async () => {
      presets.service.update.and.returnValue(of(filterPreset(1, 'Far east', ['Durlach', 'Hagsfeld'], true)));
      await open(east);
      await typeName(' Far east ');
      await tick('Hagsfeld');

      button('Save').click();
      await render();

      expect(presets.service.update).toHaveBeenCalledWith(1, { name: 'Far east', districts: ['Durlach', 'Hagsfeld'] });
    });

    it('saves an empty selection as "all districts"', async () => {
      presets.service.update.and.returnValue(of(filterPreset(1, 'East', [], true)));
      await open(east);

      button('Clear').click();
      await render();
      expect(element().textContent).toContain('none ticked = all districts');

      button('Save').click();
      await render();

      expect(presets.service.update).toHaveBeenCalledWith(1, { name: 'East', districts: [] });
    });

    it('Cancel restores the stored preset', async () => {
      await open(east);
      await tick('Hagsfeld');

      button('Cancel').click();
      await render();

      expect(checkbox('Hagsfeld').checked).toBeFalse();
      expect(button('Save').disabled).toBeTrue();
    });

    it('keeps unsaved edits when the list changes, but refreshes an unchanged editor', async () => {
      await open(east);
      presets.presets$.next([filterPreset(1, 'East', ['Durlach', 'Weststadt'], true), west]);
      await render();
      expect(checkbox('Weststadt').checked).toBeTrue();

      await tick('Hagsfeld');
      presets.presets$.next([filterPreset(1, 'East', [], true), west]);
      await render();

      expect(checkbox('Hagsfeld').checked).toBeTrue();
      expect(checkbox('Weststadt').checked).toBeTrue();
    });

    it('closes the editor when its preset was deleted elsewhere', async () => {
      await open(east);

      presets.presets$.next([west]);
      await render();

      expect(nameInput()).toBeNull();
    });

    it('offers a stored district the API does not know, so it can be unticked', async () => {
      const old = filterPreset(3, 'Old', ['Altstadt']);
      presets.presets$.next([east, west, old]);
      await render();
      await open(old);
      await render();

      expect(checkbox('Altstadt').checked).toBeTrue();
    });

    it('warns when the districts could not be loaded', async () => {
      districts$.next([]);
      await open(west);

      expect(element().textContent).toContain('Could not load the districts');
    });
  });

  describe('new preset', () => {
    it('creates a preset and keeps it open in the editor', async () => {
      const north = filterPreset(3, 'North', ['Hagsfeld']);
      presets.service.create.and.returnValue(of(north));

      button('+ New preset').click();
      await render();
      expect(button('Create').disabled).toBeTrue();

      await typeName('North');
      await tick('Hagsfeld');
      button('Create').click();
      await render();

      expect(presets.service.create).toHaveBeenCalledWith({ name: 'North', districts: ['Hagsfeld'] });
      expect(component.draft).toEqual({ id: 3, name: 'North', districts: ['Hagsfeld'] });
    });

    it('shows the API message on a conflict and keeps the draft', async () => {
      presets.service.create.and.returnValue(throwError(() => new HttpErrorResponse({
        status: 409,
        error: { success: false, message: "A filter preset named 'East' already exists" }
      })));
      button('+ New preset').click();
      await render();
      await typeName('East');

      button('Create').click();
      await render();

      expect(element().querySelector('[role="alert"]')!.textContent).toContain("A filter preset named 'East' already exists");
      expect(nameInput().value).toBe('East');
    });

    it('has no default or delete actions before it is created', async () => {
      button('+ New preset').click();
      await render();

      expect(button('Delete')).toBeUndefined();
      expect(button('Set as default')).toBeUndefined();
    });
  });

  describe('default', () => {
    it('clears the default for the default preset', async () => {
      presets.service.clearDefault.and.returnValue(of([filterPreset(1, 'East', ['Durlach']), west]));
      await open(east);

      button('Unset default').click();
      await render();

      expect(presets.service.clearDefault).toHaveBeenCalled();
    });

    it('sets another preset as default', async () => {
      presets.service.setDefault.and.returnValue(of(filterPreset(2, 'West', [], true)));
      await open(west);

      button('Set as default').click();
      await render();

      expect(presets.service.setDefault).toHaveBeenCalledWith(2);
    });
  });

  describe('Delete', () => {
    it('deletes after confirmation and closes the editor', async () => {
      spyOn(window, 'confirm').and.returnValue(true);
      presets.service.delete.and.returnValue(of([west]));
      await open(east);

      button('Delete').click();
      await render();

      expect(window.confirm).toHaveBeenCalledWith('Delete preset "East"?');
      expect(presets.service.delete).toHaveBeenCalledWith(1);
      expect(nameInput()).toBeNull();
    });

    it('does nothing when the confirmation is declined', async () => {
      spyOn(window, 'confirm').and.returnValue(false);
      await open(east);

      button('Delete').click();
      await render();

      expect(presets.service.delete).not.toHaveBeenCalled();
    });
  });
});
