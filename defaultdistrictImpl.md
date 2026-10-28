# Default District Filter (Named Presets): Implementation Plan

## Goal

The dashboard's district filter can be saved as **named filter presets** (e.g. "South", "Near work").
Any number of presets can exist. **At most one preset is marked as the default**. That one is applied when
the dashboard opens, instead of "all districts". From the frontend the user can:

- apply any preset to the current view,
- create a new preset from the current selection,
- update a preset (selection and/or name),
- delete a preset,
- mark a preset as the default, or clear the default.

There are no users, so presets are global. They are persisted in SQLite (the canonical store) and
exposed through a small REST API.

## Behaviour

| Situation | District filter starts with |
|---|---|
| URL has `?district=...` (e.g. a link from the overview table into the offers tab) | The URL districts. **URL wins over the default preset** |
| No `district` param, a default preset exists | The default preset's districts |
| No default preset (none saved, or default cleared) | Nothing selected = all districts (today's behaviour) |
| Loading presets fails (backend down, 500) | Nothing selected = all districts; the tab still loads its data |

- A preset with an **empty district list means "all districts"**. That matches `filterByDistricts`, where an empty selection keeps everything.
- Changing the multi-select only changes the current view. Presets change only through explicit actions (Save / Save as new / Delete / Set as default).
- Applying a different preset does **not** change which preset is the default. Only "Set as default" does that.
- Presets are shared by every tab with a district filter. After a change in one tab, the other tab sees it
  (the frontend cache is updated after every successful write).

## Backend

### 1. Extract the district list into a shared place ✅ DONE
`AllowedDistricts` was a `private static` set inside `ScraperCoordinator`. The new API needs it for validation.

- Moved to `FlatMonitor/Models/KarlsruheDistricts.cs`:
  - `public static IReadOnlySet<string> All` (27 names, `StringComparer.OrdinalIgnoreCase`)
  - `public static string? ToCanonical(string? name)`: exact case-insensitive match (after trimming) → canonical spelling, else `null`.
    No substring matching. That fuzzy logic stays in `ScraperCoordinator.NormalizeDistrict` for scraped data.
- `ScraperCoordinator` uses `KarlsruheDistricts.All`. No behaviour change.
- Docs fixed: 26 → 27 districts (FILTERING.md, CLAUDE.md, AGENTS.md).

**Status:** implemented. Tests: `FlatMonitor.Tests/Models/KarlsruheDistrictsTests.cs`. All 209 .NET tests passed at that point.

### 2. Model + DbContext ✅ DONE
`FlatMonitor/Models/FilterPreset.cs`:
```csharp
public sealed class FilterPreset
{
    public const int MaxNameLength = 100;

    public int Id { get; set; }
    public required string Name { get; set; }      // unique, case-insensitive
    public List<string> Districts { get; set; } = []; // empty = all districts
    public bool IsDefault { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
```
`FlatsDbContext`: `DbSet<FilterPreset> FilterPresets`, table `FilterPresets`:
- `Id`: auto-increment key.
- `Name`: required, max 100, `COLLATE NOCASE`, unique index `idx_FilterPresets_Name`.
  "East" and "east" count as the same name, and lookups by name ignore case.
- `Districts`: EF Core primitive collection, stored as a JSON text column (`["Durlach","Südstadt"]`).
- `IsDefault`: required, DB default `false`. **Partial unique index** `idx_FilterPresets_SingleDefault`
  on `IsDefault` with filter `"IsDefault" = 1`, so the database itself guarantees at most one default.
- `UpdatedAtUtc`: required.
- No seed data. An empty table means no presets and no default (all districts).

**Status:** implemented. Mapping tests run against a real SQLite in-memory database, so the JSON column, the
collation and the partial index are actually tested: `FlatMonitor.Tests/Storage/FilterPresetMappingTests.cs` (9 tests, all pass).
They cover round-trip, empty list, JSON storage, several presets, duplicate name rejected (also with different case),
case-insensitive lookup, many non-default presets, and a second default rejected.
(The pending-migration warning that broke startup and 4 backup/restore tests is resolved by step 3.)

### 3. Migration ✅ DONE
```bash
dotnet ef migrations add AddFilterPresets --project FlatMonitor
```
(Requires the global `dotnet-ef` tool, version 10.0.1 to match EF Core. Add `--configuration Release` while the API server is
running, because the server locks the Debug output.)
Check the generated migration for the `NOCASE` collation on `Name` and the `filter: "\"IsDefault\" = 1"` on the index.
Migrations already run on startup (`MigrateAsync` in `Program.cs`), so nothing else is needed for deployment.

**Status:** implemented. `FlatMonitor/Migrations/20261028104827_AddFilterPresets.cs` (+ Designer, updated model snapshot).
Collation and partial index are present. `dotnet ef migrations has-pending-model-changes` reports none. All 218 .NET tests pass.
The snapshot diff also renames entity types `workInProgress.Models.*` → `FlatMonitor.Models.*`, left over from an earlier
namespace rename. This changes no schema.

### 4. Repository ✅ DONE
`FlatMonitor/Storage/IFilterPresetRepository.cs` + `FilterPresetRepository.cs`, styled like
`ScrapingHistoryRepository` (takes `FlatsDbContext` directly). All methods take a `CancellationToken ct = default`.
```csharp
Task<IReadOnlyList<FilterPreset>> GetAllAsync(ct);                 // ordered by Name
Task<FilterPreset?> GetByIdAsync(int id, ct);
Task<bool> NameExistsAsync(string name, int? excludingId, ct);     // case-insensitive (NOCASE)
Task<FilterPreset> CreateAsync(string name, IReadOnlyList<string> districts, ct);
Task<FilterPreset?> UpdateAsync(int id, string name, IReadOnlyList<string> districts, ct); // null if not found
Task<bool> DeleteAsync(int id, ct);                                 // false if not found
Task<FilterPreset?> SetDefaultAsync(int id, ct);                    // null if not found
Task ClearDefaultAsync(ct);
```
- `SetDefaultAsync` runs in **one transaction with two `SaveChanges`**: first clear the current default, then set
  the new one. With a single `SaveChanges`, EF may issue the two UPDATEs in either order and break the partial unique index.
  Setting the preset that is already the default is a no-op.
- Deleting the default preset leaves no default (all districts). It does not promote another preset.
- Every write sets `UpdatedAtUtc = DateTime.UtcNow`.
- Validation (names, districts) is **not** done here. That's the controller's job. The unique indexes are the last line of defence.

Register in `Program.cs` (web server only; the CLI `scrape` mode doesn't need it):
```csharp
builder.Services.AddScoped<IFilterPresetRepository, FilterPresetRepository>();
```

**Status:** implemented as planned and registered in `Program.cs`. `GetAllAsync`/`GetByIdAsync` are no-tracking;
`NameExistsAsync` relies on the `NOCASE` collation. `ClearDefaultAsync` clears all defaults in one `SaveChanges`, and
`SetDefaultAsync` reuses it inside its transaction. Updating a preset keeps its default flag. Tests (SQLite in-memory, 24):
`FlatMonitor.Tests/Storage/FilterPresetRepositoryTests.cs`. All 242 .NET tests pass.

### 5. Contracts + controller ✅ DONE
`FlatMonitor/Api/Contracts/`:
- `FilterPresetDto`: `Id`, `Name`, `Districts`, `IsDefault`, `UpdatedAtUtc` (response).
- `SaveFilterPresetRequest`: `Name`, `Districts` (body for create and update).

New `FlatMonitor/Api/Controllers/FilterPresetsController.cs`, `[Route("api/filter-presets")]`. It uses the same
`ApiResponse<T>` + try/catch shape as `FlatsController`:

| Method | Route | Body | Success | Errors |
|---|---|---|---|---|
| GET | `/api/filter-presets` | — | 200 `ApiResponse<List<FilterPresetDto>>` (ordered by name, `[]` if none) | 500 |
| POST | `/api/filter-presets` | `SaveFilterPresetRequest` | 201 `ApiResponse<FilterPresetDto>` | 400 invalid, 409 name taken, 500 |
| PUT | `/api/filter-presets/{id}` | `SaveFilterPresetRequest` | 200 `ApiResponse<FilterPresetDto>` | 400, 404, 409, 500 |
| DELETE | `/api/filter-presets/{id}` | — | 200 `ApiResponse<List<FilterPresetDto>>` (remaining presets) | 404, 500 |
| PUT | `/api/filter-presets/{id}/default` | — | 200 `ApiResponse<FilterPresetDto>` (now default) | 404, 500 |
| DELETE | `/api/filter-presets/default` | — | 200 `ApiResponse<List<FilterPresetDto>>` (all presets, none default) | 500 |

The two DELETEs return the preset list instead of an empty `ApiResponse<object>`. The frontend's `unwrapApiResponse()`
treats `data: null` as an error, and the list lets the service simply replace its cache.
`{id:int}` route constraints keep `DELETE /default` from being matched as an id.

There is no separate "get default" endpoint. The list already carries `isDefault`, and the frontend needs the list anyway.

Request validation / normalisation (small static helper, e.g. `FilterPresetRequestNormalizer`, so it can be unit-tested on its own):
- `null` body → 400.
- `Name`: trim; blank → 400; longer than 100 → 400.
- `Districts`: `null` → treated as `[]`. Trim entries, drop blanks, map each with `KarlsruheDistricts.ToCanonical`;
  any unknown entry → 400 with the unknown names in the message; remove duplicates and sort, so the stored value is stable.
- Name uniqueness: `NameExistsAsync(name, excludingId: id)` → 409 before writing. A `DbUpdateException` from the unique
  index (race) is also mapped to 409.
- Renaming a preset to its own name with different case (e.g. "east" → "East") is allowed.

**Status:** implemented. Files: `Api/Contracts/FilterPresetDto.cs`, `SaveFilterPresetRequest.cs`,
`FilterPresetRequestNormalizer.cs` (`TryNormalize`, districts sorted with `InvariantCulture`, so "Grötzingen" sorts next to "Grünwinkel"),
`Api/Controllers/FilterPresetsController.cs`. A `DbUpdateException` on create/update is mapped to 409 only if the name is
now taken; otherwise it is a 500. Tests: `FlatMonitor.Tests/Api/Contracts/FilterPresetRequestNormalizerTests.cs`,
`FlatMonitor.Tests/Api/Controllers/FilterPresetsControllerTests.cs`. All 279 .NET tests pass.
Not covered: malformed JSON in the body gets ASP.NET's automatic 400 `ProblemDetails`, not an `ApiResponse`.
No end-to-end HTTP check yet; do one after restarting the server (see step 3).

## Frontend

### 6. Model + service ✅ DONE
- `core/models/filter-preset.model.ts`:
  `export interface FilterPreset { id: number; name: string; districts: string[]; isDefault: boolean; updatedAtUtc: string; }`
  and `export interface SaveFilterPresetRequest { name: string; districts: string[]; }`
- New `core/services/filter-presets.service.ts` (`providedIn: 'root'`), separate from `FlatsService`:
  - `presets$: Observable<FilterPreset[]>`: loaded once on first subscribe, cached (`BehaviorSubject` or `shareReplay(1)`).
    On load error it emits `[]`, so a failing presets call never breaks a tab.
  - `defaultPreset$: Observable<FilterPreset | null>`: derived from `presets$`.
  - `create(request)`, `update(id, request)`, `delete(id)`, `setDefault(id)`, `clearDefault()`:
    each call the API, `unwrapApiResponse()`, and **on success update the cached list**. Setting a default clears `isDefault` on the others in the cache.
    Errors propagate to the caller, which shows them (e.g. 409 "name already exists").

**Status:** implemented. `presets$` uses a private `BehaviorSubject<FilterPreset[] | null>` plus `defer`; the GET happens on
the first subscribe and only once. On a failed load it emits `[]`, logs the error, and lets the **next** subscriber retry.
`create`/`update`/`setDefault` patch the cache and keep it sorted by name (case-insensitive); `delete`/`clearDefault` replace
the cache with the list the backend returns. A write before the first load finished doesn't touch the cache; the load brings it.
Tests: `core/services/filter-presets.service.spec.ts` (19 specs). All 274 frontend tests pass.

### 7. Shared UI: preset bar ✅ DONE

> **Changed later:** Rename, Set/Unset default and Delete moved to the `/presets` page; the bar keeps the dropdown,
> Save and Save as new. See [presetFilterRefactoring.md](presetFilterRefactoring.md).

New `shared/filter-preset-bar/filter-preset-bar.component.ts` (standalone), placed next to the district
`app-multi-select-filter` in each tab. Since it behaves the same in every tab, it **talks to `FilterPresetsService` itself**.
Because the chosen preset is shared across tabs (Q3), the bar has **no output**. It calls `selectPreset(id)`, and the tab
applies districts by subscribing to `activePreset$` (step 8):
- Input: `selected: string[]` (the tab's current district selection), optional `inputId`.
- UI (Bootstrap, `form-select-sm` / `btn-sm`):
  - **Preset dropdown**: all presets by name, the default marked with ★, plus "— none —". Picking one calls `selectPreset`.
    "— none —" detaches from the preset and keeps the current districts.
  - **Save**: overwrite the chosen preset's districts with `selected`. Disabled when no preset is chosen or nothing changed (same set).
  - **Save as new…**: small inline name input + confirm; creates a preset from `selected` and selects it.
  - **Rename**: inline name input for the chosen preset.
  - **Set as default** / **Unset default** (toggle for the chosen preset).
  - **Delete**: with `confirm()`.
  - Errors (409 name taken, 400, network) are shown inline next to the bar.
- On start the dropdown shows the active preset: the one chosen in any tab, else the default.
- Shows a small "modified" hint when the current selection differs from the active preset (that is also when Save is enabled).

**Service additions for Q3** (in `filter-presets.service.ts`):
- `activePreset$`: the chosen preset (`selectPreset(id)`), or the default until something is chosen. It re-emits only when
  the active preset's id or districts change, not on a rename or on changes to other presets, so tabs can apply it on
  every emission without losing manual edits.
- Deleting the active preset makes it `null` (tabs keep their districts).
- `setDefault`/`clearDefault` first pin the active preset if nothing was chosen yet, so the dropdown and the tab's districts don't jump.
- `sameElements(a, b)` added to `core/utils/collection.utils.ts` (set comparison).

**Status:** implemented. The bar finds `activePreset` in `presets` by id, so a rename or a default change shows immediately.
"Rename" keeps the preset's stored districts; "Save" writes the current selection. Error text is the API's `message` when there
is one, else "Could not save the preset. Is the backend running?". Tests: `filter-preset-bar.component.spec.ts` (21),
10 new `activePreset$` specs in the service spec, 5 `sameElements` specs. All 310 frontend tests pass; `ng build` succeeds
(its "1 rules skipped due to selector errors" CSS warning was there before).
Not yet used by any tab; that is step 8.

### 8. Tabs ✅ DONE
**Offers tab** (`offers-tab.component.ts`)
- Subscribe to `activePreset$` (`takeUntilDestroyed`). On each non-null emission: `selectedDistricts = preset.districts; applyFilters()`.
  A `null` emission keeps the current selection.
- URL wins: if `params.getAll('district')` is non-empty, use it and **skip the first** `activePreset$` emission
  (later emissions, e.g. picking a preset in the bar, apply normally).
- The presets request and `getCurrentOffers()` run in parallel. `applyFilters()` works with whatever has arrived,
  so the order doesn't matter, and presets don't block the offers load.
- Add `<app-filter-preset-bar [selected]="selectedDistricts">` next to the district filter.

**Overview tab** (`overview-tab.component.ts`)
- Same, without the URL part (and calling `applyFiltersAndGrouping()`).
- A reload after a scrape (`scrapeFinished$`) must **not** reset the user's current selection to the default.

**Options vs. selected values**
`availableDistricts` is built from the loaded data. A preset district with no current flats would be selected but
would not appear in the `<select>`. Fix: `availableDistricts = distinctSorted([...fromData, ...selectedDistricts])`,
so preset districts are always visible and can be deselected.

**History tab** ✅ DONE (Q1 decided: yes)
- New district multi-select (`historyDistrictSelect`) and preset bar (`historyPresetSelect`) before the "Group By" radios.
- The district filter is applied to `historyData` **before grouping, in both groupings**. With "Room Number" it shows
  the room trends within the selected districts (same as the overview tab, which filters by district even when not grouping by it).
- Follows `activePreset$` like the other tabs; preset districts without history stay in the options.
- If the filter leaves no data: "No history data for the selected districts." instead of an empty chart.
- Tests: new `District filter and presets` block in `history-tab.component.spec.ts` (9). All 336 frontend tests pass; `ng build` succeeds.

**Filter layout (all three tabs)** ✅ DONE
The filter area is a column of rows (`d-flex flex-column gap-2`), each row `d-flex flex-wrap` and marked with `data-filter-row`:
1. `districts`: district multi-select + preset bar, **nothing else**.
2. `other`: every other filter (offers: rooms, price min/max; overview: rooms). The history tab has none.
3. `grouping`: grouping controls, always on their own row (overview checkboxes, history radios). The offers tab has none.

New filters go into `other`. Tests: a `Filter layout` block in each tab spec checks which rows exist and what's in them.

**Status (offers + overview):** implemented. Both tabs have `followActivePreset()` (subscribed in `ngOnInit`, `takeUntilDestroyed`) and
`updateDistrictOptions()`; the offers tab skips the first emission when the URL had `district` params.
The bar sits right after the district multi-select (`inputId="offerPresetSelect"` in offers, default `presetSelect` in overview).
Tests: new `Filter presets` blocks in `offers-tab.component.spec.ts` (10) and `overview-tab.component.spec.ts` (7).
The shared stub `core/testing/filter-presets.testing.ts` (`createFilterPresetsServiceStub`, `filterPreset`) is also provided in
`dashboard.component.spec.ts`, whose routing tests render the real tabs. All 327 frontend tests pass; `ng build` succeeds.
Not yet checked in a browser against the real backend (needs a server restart for the migration).

## Tests (NUnit + Moq + FluentAssertions / Jasmine)

Backend:
- `KarlsruheDistrictsTests` ✅.
- `FilterPresetMappingTests` ✅ (SQLite).
- `FilterPresetRepositoryTests` (SQLite in-memory, because the transaction and the partial index matter): GetAll ordered by name;
  create/update/delete round-trip; update/delete/setDefault on unknown id → null/false; `SetDefaultAsync` moves the default
  from A to B (exactly one default afterwards); setting the current default again is a no-op; `ClearDefaultAsync` leaves no default;
  deleting the default leaves no default; `NameExistsAsync` ignores case and honours `excludingId`; `UpdatedAtUtc` set on writes.
- `FilterPresetRequestNormalizerTests`: trims the name; blank or too-long name → error; null districts → `[]`;
  canonicalises case, dedupes, sorts; unknown districts → error listing them.
- `FilterPresetsControllerTests` (Moq repo, like `FlatsControllerTests`): each endpoint's success response;
  400 on invalid input (repo not called); 409 on a taken name (create, and update to another preset's name); rename to own name in
  different case allowed; 404 on unknown id; 500 when the repo throws; `DbUpdateException` → 409.
- `ScraperCoordinatorTests` unchanged and green.

Frontend:
- `filter-presets.service.spec.ts`: GET only once with several subscribers; error → `[]`; `defaultPreset$` picks the
  `isDefault` one or `null`; create/update/delete/setDefault/clearDefault send the right request and update the cache;
  setDefault leaves exactly one default in the cache; a failed write leaves the cache unchanged.
- `filter-preset-bar.component.spec.ts` ✅: lists presets with the default marked; picking a preset calls `selectPreset`; Save disabled
  when unchanged; Save as new / Rename / Delete / Set as default call the service; a 409 shows the error.
- `offers-tab.component.spec.ts` ✅: URL districts win over the active preset; no URL param → active preset applied; none → all offers;
  presets error → all offers; a later `activePreset$` emission re-filters; a preset district without data still appears in the options.
- `overview-tab.component.spec.ts` ✅: active preset applied on init; a later emission re-filters; reload after `scrapeFinished$` keeps the current selection.

## Docs
- `WohnungMonitor.md`: add the `FilterPresets` table and the API endpoints.
- `FILTERING.md`: one short section on frontend filter presets (separate from scrape-time filtering).

## Suggested commit sequence
1. `refactor(scrape): extract allowed districts into KarlsruheDistricts` ✅ ready to commit
2. `feat(db): add FilterPresets table and repository` (steps 2–4 together; steps 2+3 alone are also safe to commit now)
3. `feat(api): add filter presets endpoints`
4. `feat(frontend): add filter presets service and preset bar`
5. `feat(frontend): apply default filter preset in overview and offers tabs`
6. `docs(filtering): document filter presets`

## Open questions
1. ~~**History tab**~~ **Decided:** yes. District multi-select + preset bar; the filter applies in both groupings (see step 8).
2. ~~**Rooms / price**~~ **Decided:** presets store districts only. Rooms/price can be added later with a new migration.
3. ~~**Chosen preset across tabs**~~ **Decided:** yes. The preset chosen in one tab is active in the other tabs too
   (held in `FilterPresetsService` while the app is open; a page reload starts with the default again).
   Manual district edits that aren't saved to the preset are **not** carried over to the other tab.
