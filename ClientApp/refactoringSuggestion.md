# Frontend Refactoring Suggestions

Review of `ClientApp/src` after the DashboardComponent decomposition (overview / offers / history tabs,
`FilterService`, `aggregation.utils`). Items already done in the previous round are not repeated.

Ordered by priority: **A** = bugs found during the review (fix first, ideally test-first),
**B** = structural refactorings, **C** = cleanup / modernisation.

---

## A. Bugs and risks uncovered by the review

### A1. XSS via scraped offer URLs (security) ✅ COMPLETED
`offers-tab.component.ts:173-185`

> **Done:** `isHttpUrl` added in `core/utils/url.utils.ts` (+ spec). `OffersTabComponent` no longer
> injects `DomSanitizer`; `isValidUrl` / `getSafeUrl` removed, the template binds `[href]="offer.url"`
> only when `isHttpUrl(offer.url)`. Component spec checks that `javascript:` / `data:` / missing URLs
> render `-` instead of a link.

`isValidUrl` only checks that `new URL(url)` parses, and `getSafeUrl` then calls
`bypassSecurityTrustUrl`. `new URL('javascript:alert(1)')` parses fine, so a scraped `javascript:` URL
is rendered as a clickable, unsanitised link.

**Fix:** allow only `http:` / `https:` and drop the sanitizer bypass entirely — Angular's default
`[href]` sanitisation is correct for http(s) links.

```ts
export function isHttpUrl(url: string | undefined): url is string {
  if (!url) return false;
  try {
    const { protocol } = new URL(url);
    return protocol === 'http:' || protocol === 'https:';
  } catch {
    return false;
  }
}
```
```html
<a *ngIf="isHttpUrl(offer.url)" [href]="offer.url" ...>
```

### A2. Status polling dies permanently after one failed request ✅ COMPLETED
`overview-tab.component.ts:60-73`

> **Done:** each status request inside the `switchMap` now has its own `catchError` that logs and
> returns `EMPTY`, so a failed poll is skipped and the next tick polls again. Covered by the
> `keeps polling after a failed status request` spec (fakeAsync).

`interval(2000).pipe(switchMap(() => getScrapeStatus()))` has no `catchError`. A single 5xx / network
blip errors the outer stream and polling stops for the rest of the session — the scraping status and
the "Manual Scrape" / "Cancel Scraping" toggle silently stop updating.

**Fix:** catch inside the `switchMap` (`catchError(() => EMPTY)`), or use `retry({ delay: 2000 })`.
See also B3.

### A3. Price filter hides offers without a price even when no range is set ✅ COMPLETED
`filter.service.ts:32-51`

> **Done:** `filterByPriceRange` returns the input unchanged when neither bound is active
> (`null` or `<= 0`); items without a price are only dropped once a min or max is set. The two
> `FilterService` specs that asserted the old behaviour were rewritten, plus new service and
> `OffersTabComponent` specs for priced/unpriced offers with and without a range.

`filterByPriceRange` returns `false` for `value === undefined` before looking at `min`/`max`, so with
both bounds empty every offer lacking `priceEur` disappears from the Current Offers table.

**Fix:** return `data` unchanged when neither bound is active; only drop missing values when a bound
is set. Add a spec case for it.

### A4. Room counts are rounded differently in each tab ✅ COMPLETED

> **Done:** decision — keep fractional counts, rounded to one decimal. `normalizeRooms` added in
> `core/utils/rooms.utils.ts` (+ spec) and used for filter options, filtering and the URL in all
> places: overview (options, filter, drill-down `rooms=` param), offers (options, filter, `rooms=`
> URL param), history (chart labels). Both tables render rooms with `number: '1.0-1'`.
> Specs cover 2.5 options/filter/URL/rendering in offers and 2.54 → 2.5 in overview.
>
> Remaining edge: the overview's "group by district + rooms" view shows backend rows as-is, so if the
> API ever returned 2.54 and 2.5 as separate rows they would both display as "2.5". Grouping keys
> are not normalized; revisit with B5 if that happens.
- Overview: raw `roomsCount` (`overview-tab.component.ts:334-336` puts e.g. `rooms=2.5` in the URL)
- Offers: `Math.round(roomsCount)` for both the options and the filter (`offers-tab.component.ts:104,118`)
- History: rounded to one decimal (`history-tab.component.ts:103`)

If fractional room counts exist, clicking a "2.5 rooms" row in the overview opens Current Offers with
a filter that can never match (offers are compared as `3`), and the select shows no selected option.

**Fix:** one shared `normalizeRooms(value)` helper in `core/utils`, used by all three tabs and by the
drill-down URL builder.

### A5. Weighted average can produce `NaN` ✅ COMPLETED
`aggregation.utils.ts:10-20`

> **Done:** `mergeWeightedAverage` returns early when the combined count is 0, leaving the
> aggregation untouched (`count: 0`, average unchanged) instead of dividing `0 / 0`. Specs added for
> the util (both counts zero, zero-count item into existing data) and for the overview's
> group-by-district view with zero-count rows. History's own `count <= 0` skip is kept.

When both counts are 0 the division is `0 / 0`. History guards against it (`count <= 0` skip), the
overview grouping does not. Guard in the util itself (`if (newTotalCount === 0) return`) so callers
don't need to know.

### A6. `DashboardComponent` SCSS has no effect ✅ COMPLETED
`dashboard.component.scss`

> **Done:** deleted `dashboard.component.scss` and the `styleUrls` entry. Chose delete over moving to
> `styles.scss`: the rules never applied, so deleting keeps the current look, whereas making them
> global would e.g. shrink the large loading spinners in every tab to 1rem.

`.table-responsive`, `.btn-group`, `.spinner-border` are styled in the shell, but the elements live in
the child tab components. With default emulated view encapsulation these rules never match. Either move
them to `styles.scss` (if they are meant to be global) or delete them. (`.btn-group` is not used at all.)

### A7. Duplicate / misleading scrape feedback ✅ COMPLETED
`overview-tab.component.ts:289-308`, template lines 57-70

> **Done:** removed `scrapedCount` and the "Scraped N flats successfully" alert; only the queued
> message remains. Correction to the analysis below: the backend's `POST /api/scrape` only queues the
> job and always returns `FlatsCount = 0` (`ScrapeController.cs`), so in practice that alert was dead
> code rather than a duplicate — only the spec mock (`flatsCount: 150`) made it appear. The final
> count is already shown by the "Last scrape … N flats" badge from the status poll, so no new
> reporting was added. Spec checks that exactly one alert (the queued message) is rendered.

`triggerScrape` shows a "Scrape job queued" alert *and* sets `scrapedCount = response.flatsCount`, which
drives a second "Scraped N flats successfully" alert — at queue time, before anything was scraped.
Keep only the queued message; report the final count from the status poll when the run finishes.

### A8. Data is never auto-refreshed when a scrape finishes ✅ COMPLETED
`overview-tab.component.ts:71-79` (found while writing the A2 specs)

> **Done:** the poll handler captures `wasScraping` before `updateScrapingStatus` and reloads the
> aggregated data on the running → not-running transition. Specs: reload exactly once when a scrape
> finishes, reload after a failed poll in between, no reload while nothing is running.
>
> Remaining gap (backend): `IsScrapingInProgress()` only counts `Running` jobs, not `Queued` ones.
> Right after "Manual Scrape", the immediate status reload can still report "not in progress" and
> flip the button back until the job starts. If a job finished between two polls (< 2 s) its
> completion would also be missed. Counting `Queued` jobs as in progress in `ScrapeJobService`
> would close both.

The poll handler calls `updateScrapingStatus(status)` first, which already sets
`this.scraping = status.isScrapingInProgress`. The following check
`else if (this.scraping) { this.scraping = false; this.loadAggregatedData(); }` therefore always sees
`false`, so the "scrape just finished → reload" branch is unreachable and the table keeps showing
pre-scrape data until the user clicks "Refresh Data".

**Fix:** remember the previous state before updating:

```ts
const wasScraping = this.scraping;
this.updateScrapingStatus(status);
if (wasScraping && !status.isScrapingInProgress) {
  this.loadAggregatedData();
}
```

Spec: status sequence running → not running must trigger one extra `getAggregated` call.
B3's `scrapeFinished$` stream (`pairwise` on `isScrapingInProgress`) is the structural version of this fix.

---

## B. Structural refactorings

### B1. Replace Bootstrap tabs + DOM hacks with child routes ✅ COMPLETED

> **Done:**
> - Routes `/overview`, `/offers`, `/history` as children of the `DashboardComponent` shell; `''` and
>   unknown URLs (incl. old `/dashboard`) redirect to `/overview`. Tabs are `routerLink` +
>   `routerLinkActive` (+ `aria-current`); the `bootstrap.bundle.min.js` import is gone
>   (initial bundle 722 kB → 647 kB).
> - Old drill-down links `/?tab=offers&district=…&rooms=…` are redirected to
>   `/offers?district=…&rooms=…`, so existing bookmarks keep working.
> - Offers reads filters from `ActivatedRoute.snapshot.queryParamMap` instead of `window.location`.
> - Overview drill-down still opens a new window (unchanged UX) but builds the URL with
>   `router.createUrlTree` + `Location.prepareExternalUrl` (respects `<base href>`).
> - History: `shown.bs.tab` listener, `setTimeout(…, 100)` and the 0×0-canvas guard removed; a
>   `@ViewChild` setter renders the chart when the canvas appears and destroys it when it goes.
>   This also fixes the long-failing "Chart scaling" spec, which was skipped by that guard.
> - Not done (kept out of scope): Offers does not write filter changes back to the URL; tabs
>   re-fetch their data on every navigation (no caching).
> - Deployment note: works with `ng serve` (history fallback). If the ASP.NET app ever serves the
>   built SPA, it needs `UseStaticFiles()` + `MapFallbackToFile("index.html")` for deep links.

Several workarounds exist only because the tabs are Bootstrap JS tabs rather than Angular routes:

| Workaround | Location |
|---|---|
| `document.getElementById('offers-tab')?.click()` to pre-select a tab | `dashboard.component.ts:15-22` |
| `window.location.search` parsing for filters | `offers-tab.component.ts:66-72` |
| `shown.bs.tab` listener + 0×0 canvas guard + `setTimeout(..., 100)` | `history-tab.component.ts:47-71,154-160` |
| `window.open(... '_blank')` for drill-down | `overview-tab.component.ts:325-339` |
| Two routes (`''`, `'dashboard'`) both mapped to the shell | `app.routes.ts` |

**Proposal:**

```ts
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'overview' },
  {
    path: '',
    component: DashboardComponent,          // header + <nav> with routerLink tabs + <router-outlet>
    children: [
      { path: 'overview', component: OverviewTabComponent },
      { path: 'offers',   component: OffersTabComponent },   // ?district=..&rooms=..
      { path: 'history',  component: HistoryTabComponent },
    ],
  },
];
```

- Tabs become `<a routerLink="offers" routerLinkActive="active">`; `bootstrap.bundle.min.js` import
  in `main.ts` can go.
- Offers reads filters from `ActivatedRoute.queryParamMap` and can *write* them back
  (`router.navigate([], { queryParams, queryParamsHandling: 'merge' })`), so filtered views become
  shareable/bookmarkable.
- Drill-down becomes `router.navigate(['/offers'], { queryParams })` (or keep a new window, but build
  the URL with `router.createUrlTree`).
- History is only instantiated when visible → the zero-size canvas problem and the timers disappear.
- Trade-off: tabs are re-created on navigation, so data is re-fetched. If that matters, cache in the
  service (`shareReplay`) — see B2.

### B2. Unwrap `ApiResponse` once, in the service ✅ COMPLETED

> **Done:** `core/utils/api-response.utils.ts` adds `unwrapApiResponse()` (throws `ApiResponseError`
> on `success: false` or missing data) and `describeLoadError()` (keeps the exact messages shown
> before: API message or fallback, `"Error loading …: <error>"` for HTTP errors). `FlatsService`
> `getAggregated` / `getCurrentOffers` / `getHistory` now return plain data; the three tabs have one
> `next` and one `error` path with `finalize` resetting the loading flag. Component specs mock the
> service via `core/testing/api-response.testing.ts` → `apiResult(response)`, which runs the real
> unwrap, so the existing failure specs still cover the API-failure path. Service spec now covers
> all three endpoints.
Every tab repeats the same block (`overview:81-101`, `offers:74-93`, `history:63-82`):

```ts
this.loading = true; this.error = null;
subscribe({ next: r => { if (r.success && r.data) {...} else { this.error = r.message || '...' } this.loading = false },
            error: e => { this.error = `...: ${e.message}`; this.loading = false } })
```

**Proposal:** a small operator in `FlatsService` that maps `ApiResponse<T>` → `T` and throws on
`success === false`. Components then deal with one error path:

```ts
function unwrap<T>(): OperatorFunction<ApiResponse<T>, T> {
  return map(r => {
    if (!r.success || r.data === undefined) throw new Error(r.message || 'Request failed');
    return r.data;
  });
}

getCurrentOffers(): Observable<FlatOffer[]> {
  return this.http.get<ApiResponse<FlatOffer[]>>(`${this.apiUrl}/flats/current-offers`).pipe(unwrap());
}
```

Combined with `finalize(() => this.loading = false)` this removes ~15 lines per tab and the
"forgot to reset `loading`" class of bugs.

### B3. Extract scraping status/control out of the overview tab ✅ COMPLETED

> **Done:**
> - `core/services/scrape-status.service.ts`: `status$` polls via `timer(0, 2000)` merged with a
>   `refresh()` trigger, catches errors per request, and is shared with
>   `shareReplay({ bufferSize: 1, refCount: true })`. Polling stops when nobody subscribes.
>   `scrapeFinished$` (`pairwise` running → not running) replaces the A8 `wasScraping` logic;
>   `triggerScrape()` refreshes the status on success; `cancelScrape()`.
> - `features/dashboard/scraping-status/ScrapingStatusComponent`: last / last-successful scrape,
>   badges, Manual Scrape / Cancel buttons and the scrape alerts. Kept in the Overview tab at the
>   same place; Overview projects "Data last updated" (`[scrapingStatusInfo]`) and "Refresh Data"
>   (`[scrapingStatusActions]`) into the same row, so the layout is unchanged.
> - Overview only subscribes to `scrapeFinished$` to reload; ~110 lines of scrape code removed.
> - Polling/A2/A8 specs moved to `scrape-status.service.spec.ts`; trigger/cancel/display specs to
>   `scraping-status.component.spec.ts`; Overview spec uses a stub service.
> - Small differences: the first poll happens one macrotask later (`timer(0)`) instead of a separate
>   synchronous request; scrape errors and load errors are now two separate alerts; the always-false
>   `[disabled]="scraping"` on the Manual Scrape button (already hidden by `*ngIf`) was dropped.
> - Not done (optional follow-ups): moving `<app-scraping-status>` to the dashboard header (visible
>   on every tab, would also keep polling there); slower polling while idle.
`OverviewTabComponent` (340 lines) mixes two unrelated responsibilities: the aggregation table and the
scrape lifecycle (polling, trigger, cancel, 6 status fields, badges).

**Proposal:**
- `ScrapeStatusService` exposing a single shared `status$` (polling with `timer(0, 2000)`,
  `switchMap` + `catchError`, `shareReplay({ bufferSize: 1, refCount: true })`) and a
  `scrapeFinished$` stream (emit on `inProgress: true → false`) that the overview subscribes to for
  reloading.
- `ScrapingStatusComponent` (presentational) rendering last/last-successful run, badges and the
  trigger/cancel buttons. Could sit in the dashboard header so it's visible on every tab.
- This also removes the separate initial `loadScrapingStatus()` call, which duplicates the first tick.
- Optional: only poll quickly while a scrape is running, slowly (e.g. 30 s) otherwise.

### B4. Shared table sorting ✅ COMPLETED

> **Done:** `core/utils/table-sort.ts` → `TableSort<T, K>` (`toggle`, `indicator`, `ariaSort`,
> `apply` returning a sorted copy; nulls last in both directions, numeric vs. `localeCompare`).
> `shared/sortable-header/SortableHeaderComponent` (`th[appSortableHeader]`, inputs `sort`,
> `column`, `sortLabel`, output `sortRequested`) replaces the 11 hand-written `<th><button>` blocks;
> it only displays the sort state, the tab decides what a click does. Both tabs keep `onSort(column)`
> (now `sort.toggle` + re-apply), so the existing tab sorting specs pass unchanged. New specs for
> `TableSort` and the header component. Only difference: the overview now always assigns a copy of
> the rows, even before a column is selected.
`onSort`, `sortIndicator`, `sortAriaValue` and the comparator are copy-pasted between
`overview-tab.component.ts:145-182` and `offers-tab.component.ts:134-171`.

**Proposal:** a generic helper in `core/utils/table-sort.ts`:

```ts
export class TableSort<T, K extends keyof T> {
  column: K | null = null;
  direction: 'asc' | 'desc' = 'asc';
  toggle(column: K): void { ... }
  indicator(column: K): string { ... }
  ariaSort(column: K): 'ascending' | 'descending' | null { ... }
  apply(rows: readonly T[]): T[] { ... }   // null-last, numeric vs localeCompare
}
```

Plus a `SortableHeaderComponent` (`<th app-sortable-header [sort]="sort" column="priceEur">Price (€)</th>`)
to replace the 11 identical `<th><button ...>` blocks in the two templates. The comparator gets its
own spec instead of being tested indirectly through two components.

### B5. Collapse the overview grouping into one function ✅ COMPLETED

> **Done:** `aggregation.utils.ts` gets `groupWeighted(rows, keyOf, seed)` (one count-weighted row
> per key, first-appearance order), and `mergeWeightedAverage` now returns a new object instead of
> mutating its argument. Overview's `aggregateByDistrict` / `aggregateByRooms` / `aggregateAll` and
> the 4-way `if/else` became one `aggregate()` that picks key, seed and ordering from the two
> checkboxes. History uses the returned merge value. Before refactoring, six characterization specs
> pinned the exact grouped rows (order, kept postal code / availability, empty case, input not
> modified); they pass unchanged afterwards.
>
> Not done: History's nested label → date grouping still uses its own loop (B6 reworks that map
> anyway); room grouping keys are still not normalized (A4 edge) — that would be a behaviour change.
`aggregateByDistrict`, `aggregateByRooms`, `aggregateAll` (`overview-tab.component.ts:184-257`) and the
4-way `if/else` in `applyFiltersAndGrouping` (`:129-137`) are the same algorithm with a different key.
History has its own variant (`aggregateHistoryByLabel`). Move to `aggregation.utils.ts`:

```ts
export function groupWeighted<T extends Aggregatable, K>(
  rows: readonly T[], keyOf: (row: T) => K, seed: (row: T) => T
): Map<K, T>
```

Overview then picks a key function from `(groupByDistrict, groupByRooms)` — the "both off" case is just
a constant key. Also make `mergeWeightedAverage` return a new object rather than mutating its argument
(previous round's item 12 is still open here).

### B6. Extract `HistoryChartComponent` and type Chart.js properly ✅ COMPLETED

> **Done:**
> - `chart.js@4.4.0` (same version as the old CDN tag) installed from npm; the CDN `<script>` in
>   `index.html` and all `(window as any).Chart` / `any` typings are gone. Only the line-chart
>   pieces are registered (`LineController`, `LineElement`, `PointElement`, `LinearScale`,
>   `CategoryScale`, `Legend`, `Title`, `Tooltip`).
> - `history-tab/history-chart/history-chart.config.ts`: pure, typed `buildHistoryChartConfig(points,
>   title)` with a `Map<label, Map<date, price>>` lookup instead of `find` in nested loops; palette
>   and date formatter are module-level.
> - `HistoryChartComponent` (`[points]`, `[title]`): renders after view init and on input changes,
>   destroys on destroy. The History tab keeps loading, grouping and the latest-averages table and
>   exposes `chartTitle`; it no longer touches the canvas or Chart.js.
> - `/history` route is lazy-loaded so Chart.js is only downloaded when the tab is opened: initial
>   bundle 650 kB → 666 kB, History chunk 154 kB (48 kB gzipped) instead of the ~205 kB CDN file
>   loaded on every page.
> - Specs: config builder (labels, null gaps, latest point, colours, y-axis min, tick and tooltip
>   formatting) and chart component against real Chart.js (draws, replaces on input change, no
>   chart without points, destroys); History tab spec checks what it passes to the chart.
`history-tab.component.ts:154-280`

- Chart.js is loaded from a CDN `<script>` in `index.html` and accessed as `(window as any).Chart`;
  `chart: any`, `context: any`, `value: any`. Install `chart.js` from npm and
  `import { Chart, ChartConfiguration } from 'chart.js/auto'` → typed config, versioned with
  `package-lock.json`, works offline/behind CSP, tree-shakable.
- Move rendering into a presentational `HistoryChartComponent` with
  `@Input() series` / `@Input() title`, `ngOnChanges` → update, `ngOnDestroy` → destroy.
  The tab keeps data loading and grouping only.
- Building datasets does `chartData.find(...)` inside `labels × dates` loops (O(labels·dates·n)).
  Build a `Map<label, Map<date, price>>` once (the grouping step already has exactly that map —
  return it instead of flattening to `chartData` and searching it again).
- The colour palette array is re-created on every `getColorForIndex` call → module-level `const`.

### B7. Shared filter controls ✅ COMPLETED

> **Done:** three presentational components in `shared/`, markup moved unchanged:
> - `app-multi-select-filter` (`label`, `inputId`, `options`, `[(selected)]`, `minWidth`, and
>   `(changed)` emitted after `selectedChange`) replaces the 4 district/rooms selects.
> - `app-loading-spinner` replaces the 3 spinners; `app-dismissible-alert` (`type`, projected
>   message, `(dismissed)`) replaces the 5 dismissible alerts (3 tabs + scraping status). The close
>   button gained `aria-label="Close"`.
> - Inline label/input styles moved to `.filter-label` / `.filter-price-input` in `styles.scss`;
>   no `style="…"` attributes left in the templates.
> - Overview `availableRooms` is now `number[]` (null was never added; dead null-sort branch and
>   `*ngIf="rooms !== null"` removed — part of C3).
> - 13 DOM specs (dropdown/price filtering, spinners, alert dismissal) were written against the old
>   markup first and pass unchanged; plus specs for the three shared components.
>
> Not done: the "dropdown with checkboxes" UX (behaviour change, separate decision).
The district/rooms multi-select markup is duplicated in overview (`html:78-115`) and offers
(`html:8-43`), including repeated inline styles (`font-size: 0.875rem; white-space: nowrap;`,
`min-width`). Extract a `MultiSelectFilterComponent` (`label`, `options`, `[(selected)]`) and move the
inline styles to a class. Same for the loading spinner and dismissible error alert, which appear in all
three templates.

The `<option disabled selected hidden>Select...</option>` placeholder inside a `multiple` select
doesn't behave like a placeholder; a proper dropdown-with-checkboxes component would be clearer UX.

### B8. Compute view state once instead of calling methods from templates ✅ COMPLETED

> **Done:**
> - Offers: `applyOfferFilters()` also builds `offerRows: { offer, discountPercentage, priceChanged,
>   hasLink }[]`; the table iterates `offerRows`, so the discount, price-change and URL checks run
>   once per filter/sort change instead of 2× each per row on every change detection.
>   `filteredOffers` is unchanged (still the filtered + sorted offers).
> - History: `latestAverageDate` / `latestAverageData` are fields computed in
>   `updateHistoryChart()` instead of getters that re-filtered and re-sorted on every change detection.
> - Specs: two tests that failed before (no recomputation on repeated `detectChanges()`, stable
>   `latestAverageData` reference) plus DOM checks for the latest-averages table and row order;
>   existing price-display and link specs pass unchanged.
Template-bound methods run on every change-detection cycle:
- Offers: `getDiscountPercentage(offer)` ×2, `hasChangedPrice(offer)` ×2, `isValidUrl` ×2,
  `getSafeUrl` per row (`offers-tab.component.html:119-137`).
- History: getters `latestAverageDate` / `latestAverageData` re-filter **and re-sort** `chartData`
  every cycle (`history-tab.component.ts:22-35`).

Map offers to a row view model once after loading (`{ ...offer, discountPct, priceChanged, safeUrl }`)
and compute the latest-averages table when `chartData` changes (or use `computed()` signals, see C1).

---

## C. Cleanup and modernisation

### C1. Adopt Angular 18 idioms (incrementally, per component) 🟡 PARTLY DONE

> **Done (step 1, behaviour-neutral, all components and services):**
> - Built-in control flow via `ng generate @angular/core:control-flow`, then templates re-indented
>   by hand (the migration's formatter drifted after self-closing `<input />` tags) and opposite
>   `@if` pairs merged into `@if … @else`.
> - Real `track` keys instead of the migration's `track item`: offers `track row.offer` (offer
>   objects survive filter/sort), overview `track item.district + '|' + item.roomsCount` (backend
>   groups by exactly that pair, see `FlatAggregationCalculator`), history `track item.label`.
> - `inject()` instead of constructor injection; `takeUntilDestroyed(destroyRef)` instead of the
>   `destroy$` subjects (overview spec's `destroy$` test removed — unsubscribe-on-destroy is
>   covered by the existing `.observed` specs).
> - `CommonModule` replaced by the pipes/directives actually used (`DatePipe`, `DecimalPipe`,
>   `CurrencyPipe`, `NgClass`).
>
> **Still open (step 2, per component):** signals (`signal` / `computed`) for component state and
> `ChangeDetectionStrategy.OnPush`. This changes each component's public state API (specs read and
> assign fields directly), so it should be done and reviewed one component at a time.
- `inject()` instead of constructor injection.
- `takeUntilDestroyed()` instead of the `destroy$` Subject.
- Signals (`signal`, `computed`) for component state → derived values such as `filteredOffers`,
  `aggregatedData`, `latestAverageData` become `computed()` and the `on…Change()` →
  `apply…()` plumbing disappears.
- `ChangeDetectionStrategy.OnPush` once state is signal-based.
- Built-in control flow (`@if`, `@for (...; track ...)`) instead of `*ngIf` / `*ngFor`
  (`ng generate @angular/core:control-flow` migrates automatically); `@for` requires `track`, which
  also fixes the current re-rendering of whole tables on each sort.
- Drop `CommonModule` imports in favour of the specific pipes/directives once on control flow.

### C2. Leftover names from the monolith ✅ COMPLETED

> **Done:**
> - Offers: `loadingOffers` → `loading`, `selectedOfferDistricts/Rooms` → `selectedDistricts/Rooms`,
>   `availableOfferDistricts/Rooms` → `availableDistricts/Rooms`, `applyOfferFilters` → `applyFilters`;
>   the `onOfferFiltersChange()` wrapper was removed (template calls `applyFilters()`).
> - Overview: `onFiltersChange()` and `onGroupingToggle()` wrappers removed, the template calls
>   `applyFiltersAndGrouping()`; `selectedRoomCounts` → `selectedRooms` (same name as in Offers).
> - History: `loadingHistory` → `loading`, `historyGroupBy` → `groupBy`, `onHistoryGroupByChange` →
>   `onGroupByChange`, `aggregateByDistrictHistory/RoomsHistory/HistoryByLabel` →
>   `aggregateByDistrict/Rooms/Label`, `updateHistoryChart` → `updateChartData`.
> - Specs updated with the same renames; no assertions changed.
Now that each tab is its own component, the prefixes are noise:
`loadingOffers`, `selectedOfferDistricts`, `availableOfferRooms`, `onOfferFiltersChange`,
`applyOfferFilters`, `loadingHistory`, `historyGroupBy`, `onHistoryGroupByChange`,
`aggregateByDistrictHistory`… → `loading`, `selectedDistricts`, `groupBy`, etc.
Also `onFiltersChange()` and `onGroupingToggle()` in the overview are both just
`applyFiltersAndGrouping()` — keep one.

### C3. Dead or redundant code ✅ COMPLETED

> **Done:**
> - `availableRooms` null handling and the History `ngAfterViewInit` render were already removed
>   in B7 / B1+B6.
> - Overview no longer runs `roundPriceToEuro` before `currency: …: '1.0-0'` (the pipe rounds the
>   same way); the component property is gone.
> - History `parsePrice()` replaced by `item.avgPricePerMeter ?? 0`.
> - `core/utils/collection.utils.ts` → `distinctSorted(values, compare?)` builds the district / room
>   filter options in both tabs; Offers' `updateAvailableOfferFilters` (missed in C2) is now
>   `updateAvailableFilters`.
> - Characterization specs written first and passing before and after: price rounding in the
>   overview table (12.49 → €12, 12.5 → €13), missing History prices count as 0, filter options
>   drop empty values but keep 0 rooms.
>
> Not done on purpose: `rawData` / `currentOffers` / `historyData` stay public — the specs set and
> read them directly, and Angular templates can use them either way.
- `availableRooms: (number | null)[]` in overview — `null` is never added, so the null-sort branch
  (`:117-121`) and the `*ngIf="rooms !== null"` wrapper in the template are dead.
- `rawData` / `currentOffers` / `historyData` are public only for templates' `length > 0` checks.
- `roundPriceToEuro` is exposed to the overview template and then piped through
  `currency: ...: '1.0-0'`, which already rounds.
- `HistoryTabComponent.parsePrice` converts a value typed as `number` with `Number()`.
- `ngAfterViewInit` in history calls `renderChart()` while `historyData` is still empty (the
  `@ViewChild` canvas is inside an `*ngIf` and doesn't exist yet).
- Shared distinct-and-sort logic for filter options (`updateAvailableFilters` /
  `updateAvailableOfferFilters`) → `distinctSorted(rows, selector)` util.

### C4. Make models match the wire format ✅ COMPLETED

> **Done (models checked against the C# contracts):**
> - Date fields (`ScrapeResponse.scrapedAtUtc`, `ScrapingHistoryDto.startTimeUtc/endTimeUtc`) are
>   `string`; the scraping status parses them with the new `core/utils/date.utils.ts` →
>   `parseUtcDate`. **Bug fixed:** EF Core reads the SQLite timestamps without a DateTimeKind, so
>   the API sends them without an offset and `new Date()` read them as local time — "Last scrape"
>   was shown 1–2 h too early in Germany. `parseUtcDate` appends `Z` when no offset is present.
>   (Backend alternative: a UTC `ValueConverter` in `FlatsDbContext`; the helper keeps working
>   either way.)
> - `ScrapingStatus` union includes **`PartialFailed`**, which the backend writes
>   (`ScrapeBackgroundService`) but the frontend did not know — its badge had no colour. Badge
>   classes come from a typed `Record<ScrapingStatus, string>`; `PartialFailed` → `bg-warning`.
> - `cancelScrape()` returns `Observable<CancelScrapeResponse>` (`{ message }`) instead of `any`.
> - `FlatHistory.avgPricePerMeter` / `count` are required (non-nullable in C#), the `count ?? 1` /
>   `?? 0` defaults are gone; nullable fields are typed `| null`. The C3 spec for missing prices was
>   replaced by one for zero counts / missing dates / missing districts (inputs the API can send).
> - The two new component specs (UTC timestamp, coloured `PartialFailed`) fail against the old
>   component and pass now; spec mocks use ISO strings like the real API.
- `ScrapeResponse.scrapedAtUtc` and `ScrapingHistoryDto.startTimeUtc/endTimeUtc` are typed `Date` but
  arrive as ISO strings from JSON; type them as `string` and convert explicitly.
- `ScrapingHistoryDto.status: string` with a comment listing the values →
  `type ScrapingStatus = 'Running' | 'Successful' | 'Failed' | 'Canceled'`; the badge class mapping in
  the overview template becomes a typed `Record<ScrapingStatus, string>`.
- `cancelScrape(): Observable<any>` → concrete type (or `Observable<void>`).
- `FlatHistory.scrapingDate` / `count` / `avgPricePerMeter` are all optional; if the API always sends
  them, make them required and remove the defensive defaults (`count ?? 1`).

### C5. `FilterService` → plain functions
`FilterService` is stateless; like `aggregation.utils.ts` it can be exported functions in
`core/utils/filter.utils.ts`. Removes a DI dependency from two components and their spec setup.

### C6. Build configuration
- `angular.json` uses the legacy `browser` builder and `browserTarget`; Angular 18 defaults to the
  esbuild-based `application` builder with `buildTarget` (`ng update` migration
  `use-application-builder`). Faster builds, and `browserTarget` is deprecated.
- `test.options.styles` lacks the Bootstrap CSS that the build includes — harmless now, but
  layout-dependent tests (e.g. canvas size) would behave differently from the app.

---

## Suggested order

1. **A1–A5** with failing specs first (each is small and isolated).
2. **B2** (service unwrap) and **B4** (TableSort) — low-risk, shrink the components noticeably.
3. **B3** (scrape status service/component) — fixes A2/A7 structurally.
4. **B1** (routing) — removes the DOM/timer workarounds; do **B6** together with or right after it.
5. **B5, B7, B8**, then **C** items opportunistically per component (C1 is easiest one component at a time).
