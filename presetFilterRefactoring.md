# Preset Filter Refactoring: Implementation Plan

**Status:** implemented (steps 1–7). .NET: 280 tests passed; Angular: 372 specs passed, `ng build` clean.

## Goal

Preset **configuration** (create, rename, edit districts, set/clear default, delete) moves out of the
tab filter rows into its own page, `/presets`. A **"Configure filters"** link in the top right corner of the
dashboard opens that page in a **new browser tab**. The tabs keep only what is needed to *apply* a preset.

## Today

- `FilterPresetBarComponent` (`shared/filter-preset-bar/`) is embedded in the district row of each tab
  (Overview, Offers, History). It holds the preset dropdown **and** all management actions
  (Save, Save as new…, Rename…, Set as default, Delete).
- Save / Save as new take the districts from the host tab (`[selected]="selectedDistricts"`).
- `FilterPresetsService` caches the preset list and the chosen preset **in memory**. A second browser tab
  has its own Angular instance, so it shares nothing with the first one except the backend.
- The frontend has no list of all districts. The tabs derive district options from their data.
  The canonical list is `KarlsruheDistricts.All` on the backend.

## Target

```
┌──────────────────────────────────────────────────────────────┐
│ FlatMonitor                            [⚙ Configure filters] │  ← <a target="_blank">
│ ┌Dashboard┐ Current Offers  History                          │
│ Districts: [Mitte, …▾]  Preset: [★ South ▾]  modified        │  ← tabs: apply only
└──────────────────────────────────────────────────────────────┘

/presets  (new browser tab)
┌──────────────────────────────────────────────────────────────┐
│ Filter presets                                               │
│ ┌───────────────────┐  ┌─────────────────────────────────┐ │
│ │ ★ South  2 distr. │  │ Name [South_____________]       │ │
│ │   East   all      │  │ Districts (2 ticked)  Clear     │ │
│ └───────────────────┘  │ ☑ Durlach  ☐ Hagsfeld  ☑ Weststadt│ │
│ [+ New preset]         │ [Save] [Cancel] [Unset default] [Delete]│
│                        └─────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

## Steps

### 1. Backend: district list endpoint ✅ DONE
The config page edits districts without any flat data, so it needs the full list.

- New `DistrictsController`: `GET /api/districts` → `ApiResponse<string[]>` with `KarlsruheDistricts.All`,
  sorted (culture-aware, like the frontend).
- Tests: `FlatMonitor.Tests/Api/Controllers/DistrictsControllerTests.cs` (returns all 27, sorted, success flag).

### 2. Frontend: `DistrictsService` ✅ DONE
- `core/services/districts.service.ts`: `districts$` fetched once, `shareReplay(1)`, `[]` on error
  (same failure style as `FilterPresetsService.load`).
- Spec with `HttpTestingController`.

### 3. Frontend: cross-browser-tab sync for presets ✅ DONE
Without this, a preset saved in the `/presets` tab would not appear in the dashboard tab until reload.

- `FilterPresetsService` gets a `BroadcastChannel('flatmonitor-filter-presets')`, wrapped in a small
  injectable (`PresetsChannel`) so specs can fake it.
- After every successful write (`create`, `update`, `delete`, `setDefault`, `clearDefault`) the service posts
  `'changed'`. A `BroadcastChannel` does not deliver to its own sender, so the writing tab is not affected.
- On `'changed'` the other tabs re-fetch `GET /api/filter-presets` and push the result into `presetsState`:
  - call `pinActivePreset()` first, so a remote default change does not swap the districts the user is looking at
    (same rule as a local `setDefault`);
  - if the chosen preset no longer exists (deleted remotely), set `chosenPresetId` to `null`.
    The tabs then keep their current selection (existing behaviour for a `null` active preset).
  - if the active preset's districts were edited remotely, `activePreset$` re-emits and the tabs apply the new
    districts (same as a local Save today).
- Specs in `filter-presets.service.spec.ts`: posts after each write, not after a failed write;
  reloads on message; pins the default; drops a deleted chosen preset.

### 4. Frontend: `/presets` page ✅ DONE
- `features/filter-presets/filter-presets-page.component.{ts,html,spec.ts}`.
- Route in `app.routes.ts` **next to** the dashboard route (not a child), so it has no tab navigation:
  `{ path: 'presets', component: FilterPresetsPageComponent }` before the `**` fallback.
  Lazy-loading is not needed; the page is small.
- Content (Bootstrap): **list + editor** instead of the table drafted above, because a compact
  multi-select is awkward for 27 districts:
  - left: preset list (★ = default, district count or "all districts"), "+ New preset";
  - right: editor with name, district **checkboxes** (none ticked = all districts, "Clear"),
    Create/Save (enabled when dirty), Cancel, Set/Unset default, Delete (with `confirm`).
  - When the list changes (e.g. a save in another browser tab): unsaved edits are kept, an unchanged editor is
    refreshed, and the editor closes if its preset was deleted.
  - Errors: `describeWriteError` moved from the bar to `core/utils/api-response.utils.ts`.
- The page uses only `presets$` and the write methods; it does **not** touch `selectPreset`/`activePreset$`.
- Spec: renders rows, create/save/delete/default call the service with the right payloads, dirty tracking,
  error message, empty state.

### 5. Frontend: header link ✅ DONE
- `dashboard.component.html`: the title row becomes `d-flex justify-content-between align-items-center` with
  `<a class="btn btn-outline-secondary" routerLink="/presets" target="_blank" rel="noopener">⚙ Configure filters</a>`
  on the right. An `<a>` (not a `<button>` with `window.open`) so ctrl/middle click and "copy link" work.
  No icon library is installed; use the `⚙` character.
- `dashboard.component.spec.ts`: link exists, `href` ends with `/presets`, `target="_blank"`, `rel="noopener"`.

### 6. Frontend: slim down `FilterPresetBarComponent` ✅ DONE
Keeps: preset dropdown, "modified" hint, **Save** and **Save as new…** (see open question 1).
Removes: Rename…, Set/Unset default, Delete (now on the config page).

- Update `filter-preset-bar.component.spec.ts` (drop tests of removed actions, keep the rest).
- Tab specs and `filter-presets-across-tabs.spec.ts`: should need no change beyond removed-button assertions.

### 7. Docs ✅ DONE
- `defaultdistrictImpl.md`: note that management moved, link here.
- `WohnungMonitor.md`: add `GET /api/districts` and the `/presets` page.

## Hosting note
The SPA is served by `ng serve` with `proxy.conf.json`; its history fallback serves `index.html` for
`/presets`, so a fresh browser tab on that URL works. No backend change is needed for routing.

## Decisions (were open questions)

1. **What stays in the tabs?** Decided: dropdown + Save + Save as new…, because those two need the tab's
   current selection. Alternative: dropdown only, and all editing on `/presets`.
2. **Should the dropdown also move to the header**? Decided: keep it in
   the tabs for now (smaller change, tests and layout stay as they are).

Sync works both ways: a Save in a dashboard tab also updates an open `/presets` page, since both use
`FilterPresetsService` (step 3).

## Out of scope
- Authentication / per-user presets.
