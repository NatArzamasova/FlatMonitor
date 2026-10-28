# Default District Filter: Implementation Plan

## Goal

When the dashboard opens, the district filter in the tabs starts with a saved **default selection**
instead of "all districts". The user can change the default from the frontend and save it. There
are no users, so exactly **one** default is stored. It is persisted in SQLite (the canonical store)
and exposed through a small REST endpoint.

## Behaviour

| Situation | District filter starts with |
|---|---|
| URL has `?district=...` (e.g. a link from the overview table into the offers tab) | The URL districts. **URL wins over the default** |
| No `district` param, a default is saved | The saved default |
| No default saved yet / saved default is empty | Nothing selected = all districts (today's behaviour) |
| Loading the default fails (backend down, 500) | Nothing selected = all districts; the tab still loads its data |

- A saved **empty list means "all districts"**. That matches `filterByDistricts`, where an empty selection keeps everything.
- Changing the multi-select only changes the current view. Only **"Save as default"** persists it.
- **"Reset to default"** puts the saved default back into the current filter.
- A saved default is used by every tab with a district filter. After saving in one tab, the other
  tab uses the new default the next time it starts (the frontend cache is updated on save).

## Backend

### 1. Extract the district list into a shared place ✅ DONE
`AllowedDistricts` is currently a `private static` set inside `ScraperCoordinator`
(`FlatMonitor/Scraping/ScraperCoordinator.cs:9`). The new endpoint needs it for validation.

- Move it to a new public static class, e.g. `FlatMonitor/Models/KarlsruheDistricts.cs`:
  - `public static IReadOnlySet<string> All` (same 27 names, `StringComparer.OrdinalIgnoreCase`)
  - `public static string? ToCanonical(string name)`: exact case-insensitive match → canonical spelling, else `null`
    (exact match only, **no** substring matching. That fuzzy logic stays in `ScraperCoordinator.NormalizeDistrict` for scraped data).
- `ScraperCoordinator` uses `KarlsruheDistricts.All`. No behaviour change; existing `ScraperCoordinatorTests` must stay green.
- Note: FILTERING.md / CLAUDE.md say "26 districts"; the list actually has 27. Fix the docs while touching this.

**Status:** implemented. `KarlsruheDistricts` lives in `FlatMonitor/Models/KarlsruheDistricts.cs` (`ToCanonical` also trims).
Tests: `FlatMonitor.Tests/Models/KarlsruheDistrictsTests.cs`. Docs updated (FILTERING.md, CLAUDE.md, AGENTS.md).
All 209 .NET tests pass.

### 2. Model + DbContext
`FlatMonitor/Models/FilterDefaults.cs`:
```csharp
public class FilterDefaults
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public List<string> Districts { get; set; } = [];
    public DateTime UpdatedAtUtc { get; set; }
}
```
- `FlatsDbContext`: `DbSet<FilterDefaults> FilterDefaults`, table `FilterDefaults`, key `Id`
  (`ValueGeneratedNever()`, since it is always 1).
- `Districts` is an EF Core primitive collection, stored as a JSON text column in SQLite. No
  join table is needed for a single list of strings. The InMemory provider used in tests supports it as well.
- Single-row table: no seed row. "No row" means "no default saved yet" and returns an empty list.

### 3. Migration
```bash
dotnet ef migrations add AddFilterDefaults --project FlatMonitor
```
(Requires the `dotnet-ef` tool. It is not in `.config/dotnet-tools.json`; use the global tool or add it as a local tool.)
Migrations already run on startup (`MigrateAsync` in `Program.cs`), so nothing else is needed for deployment.

### 4. Repository
`FlatMonitor/Storage/IFilterDefaultsRepository.cs` + `FilterDefaultsRepository.cs`, styled like
`ScrapingHistoryRepository` (takes `FlatsDbContext` directly):
```csharp
Task<IReadOnlyList<string>> GetDistrictsAsync(CancellationToken ct = default);   // [] when no row
Task<IReadOnlyList<string>> SaveDistrictsAsync(IReadOnlyList<string> districts, CancellationToken ct = default); // upsert row Id=1, returns stored list
```
Register in `Program.cs` (web server only; the CLI `scrape` mode doesn't need it):
```csharp
builder.Services.AddScoped<IFilterDefaultsRepository, FilterDefaultsRepository>();
```

### 5. Contract + controller
`FlatMonitor/Api/Contracts/FilterDefaultsDto.cs`: `public sealed class FilterDefaultsDto { public List<string> Districts { get; set; } = []; }`

New `FlatMonitor/Api/Controllers/SettingsController.cs`, `[Route("api/settings")]`. It uses the same
`ApiResponse<T>` + try/catch shape as `FlatsController`:

| Method | Route | Body | Responses |
|---|---|---|---|
| GET | `/api/settings/filter-defaults` | — | 200 `ApiResponse<FilterDefaultsDto>` (`districts: []` if none saved); 500 on error |
| PUT | `/api/settings/filter-defaults` | `FilterDefaultsDto` | 200 `ApiResponse<FilterDefaultsDto>` with the stored (normalised) value; 400 on invalid input; 500 on error |

PUT validation / normalisation (in the controller or a small static helper, not in the repository):
- `null` body or `null` districts → 400.
- Trim entries, drop blanks.
- Map each entry with `KarlsruheDistricts.ToCanonical`. If any entry is unknown → 400, message lists the unknown names.
- Remove duplicates (case-insensitive) and sort, so the stored value is stable.

## Frontend

### 6. Model + service
- `core/models/filter-defaults.model.ts`: `export interface FilterDefaults { districts: string[]; }`
- New `core/services/filter-defaults.service.ts` (`providedIn: 'root'`). Settings are not flats, so this
  does not go into `FlatsService`:
  - `getDefaultDistricts(): Observable<string[]>`: GET once, `unwrapApiResponse()`, cached with
    `shareReplay(1)`. On error it returns `[]` (`catchError`), so a failing settings call never breaks a tab.
  - `saveDefaultDistricts(districts: string[]): Observable<string[]>`: PUT, unwrap, and on success
    replace the cache with the returned (normalised) list so other tabs see it.
  - Implementation hint: keep a private `BehaviorSubject<string[] | null>` plus a "loaded" flag, or
    reset the `shareReplay` source on save. Either works; keep it small.

### 7. Shared UI piece
New `shared/default-filter-actions/default-filter-actions.component.ts` (small, standalone), placed
right after the district `app-multi-select-filter` in each tab:
- Buttons: **Save as default** and **Reset to default** (`btn btn-sm btn-outline-secondary`).
- Inputs: `selected: string[]`, `defaults: string[]`; outputs: `save`, `reset`.
- "Save as default" is disabled while saving and when `selected` equals `defaults` (same set).
- Shows a short success/error message (reuse `DismissibleAlertComponent` or inline text).
- The component only emits. The tab calls the service. This keeps the component free of HTTP and easy to test.

(Alternative if this seems over-engineered: put the two buttons directly in both tab templates. Two copies are acceptable.)

### 8. Tabs
**Offers tab** (`offers-tab.component.ts`)
- In `loadInitialFiltersFromUrl()`: if `params.getAll('district')` is non-empty, use it (as today).
  Otherwise subscribe to `getDefaultDistricts()` (`take(1)`), set `selectedDistricts`, and call `applyFilters()`.
- The defaults request and `getCurrentOffers()` run in parallel; `applyFilters()` works with whatever
  has arrived, so the order doesn't matter. Defaults don't block the offers load.
- Add `onSaveDefaultDistricts()` / `onResetDistricts()` handlers.

**Overview tab** (`overview-tab.component.ts`)
- Same, without the URL part: in `ngOnInit`, load defaults → `selectedDistricts` → `applyFiltersAndGrouping()`.
- A reload after a scrape (`scrapeFinished$`) must **not** reset the user's current selection to the default.

**Options vs. selected values**
`availableDistricts` is built from the loaded data. A default district with no current flats would
be selected but would not appear in the `<select>`. Fix: `availableDistricts = distinctSorted([...fromData, ...selectedDistricts])`,
so saved districts are always visible and can be deselected.

**History tab**: open question, see below.

## Tests (NUnit + Moq + FluentAssertions / Jasmine)

Backend:
- `KarlsruheDistrictsTests`: `ToCanonical` exact match, case-insensitive (`"durlach"` → `"Durlach"`), unknown → `null`, no substring match.
- `FilterDefaultsRepositoryTests` (InMemory, like `ScrapingHistoryRepositoryTests`): empty when no row; save then get round-trips;
  a second save overwrites (still one row); `UpdatedAtUtc` set.
- `SettingsControllerTests` (Moq repo, like `FlatsControllerTests`): GET success / empty / 500;
  PUT success returns normalised list; PUT dedupes + canonicalises case + sorts; PUT with unknown district → 400 and repo not called;
  PUT null → 400; repo throws → 500.
- `ScraperCoordinatorTests` unchanged and green after the extraction.

Frontend:
- `filter-defaults.service.spec.ts`: GET hits the URL once even with two subscribers; error → `[]`;
  save sends PUT body and updates the cache; failed save does not change the cache.
- `default-filter-actions.component.spec.ts`: emits `save`/`reset`; save disabled when selection equals defaults.
- `offers-tab.component.spec.ts`: URL districts win over defaults; no URL param → defaults applied; defaults error → all offers shown;
  saved district without data still appears in options.
- `overview-tab.component.spec.ts`: defaults applied on init; reload after `scrapeFinished$` keeps the current selection.

## Docs
- `WohnungMonitor.md`: add the `FilterDefaults` table and the two endpoints.
- `FILTERING.md` / `CLAUDE.md`: district count 26 → 27; one line about the frontend default filter.

## Suggested commit sequence
1. `refactor(scrape): extract allowed districts into KarlsruheDistricts` ✅ ready to commit
2. `feat(db): add FilterDefaults table and repository`
3. `feat(api): add filter-defaults settings endpoint`
4. `feat(frontend): add filter defaults service and default-filter actions`
5. `feat(frontend): apply and save default district filter in overview and offers tabs`
6. `docs(filtering): document default district filter`

## Open questions
1. **History tab:** it currently has no district filter, only a "group by district / rooms" radio.
   Should it get a district multi-select (filtering `historyData` before aggregation, only when grouping by district) that also uses the default?
   This plan leaves it out until decided.
2. **Rooms / price:** should the default later also cover rooms and price range? If so, the
   `FilterDefaults` row gets more columns (`Rooms`, `PriceMin`, `PriceMax`), and the endpoint stays the same. The current design allows this. Not in scope now.
3. **Shared component vs. inline buttons** (step 7): which do you prefer?
