# Frontend Refactoring Suggestions

## Overview
The Angular frontend consists of a `DashboardComponent` that orchestrates all data display, filtering, and state management. Below are actionable refactoring suggestions organized by category.

---

## 1. Component Architecture & Decomposition

### Issue: Monolithic DashboardComponent
**Current state:** DashboardComponent (~700 lines) handles:
- Aggregated data + filtering + grouping
- Current offers + filtering
- History data + chart rendering
- Scraping status polling
- Filter state management (6+ separate filter properties per data type)

**Recommendation:** Decompose into smaller, focused components:

```
dashboard/
  ├── dashboard.component.ts (container/smart component)
  ├── aggregation-tab/
  │   ├── aggregation-tab.component.ts
  │   └── aggregation-filters.component.ts
  ├── offers-tab/
  │   ├── offers-tab.component.ts
  │   └── offers-filters.component.ts
  ├── history-tab/
  │   ├── history-tab.component.ts
  │   ├── history-chart.component.ts
  │   └── history-filters.component.ts
  └── scraping-status/
      └── scraping-status.component.ts
```

**Benefit:** Easier testing, reusability, clearer responsibility.

---

## 2. Filter & Aggregation Logic Extraction

### Issue: Repeated filter patterns across data types
Three similar filter-and-aggregate workflows:
1. Aggregated data filtering (lines 161–186)
2. Offer filtering (lines 395–419)
3. History filtering (lines 514–601)

**Recommendation:** Extract to reusable filter/aggregation service:

```typescript
// filter.service.ts
export class FilterService {
  filterByDistricts<T>(data: T[], districts: string[], districtSelector: (item: T) => string | undefined): T[] { }
  filterByRooms<T>(data: T[], rooms: number[], roomsSelector: (item: T) => number | undefined | null): T[] { }
  filterByPriceRange<T>(data: T[], min: number | null, max: number | null, priceSelector: (item: T) => number | undefined): T[] { }
}
```

**Benefit:** DRY principle; easier to test filter logic in isolation; single place to update filter behavior.

---

## 3. Aggregation Duplication

### Issue: Weighted average calculation repeated 3 times
Lines 212–215, 245–248, 276–279 all compute weighted averages identically.

**Recommendation:** Extract to utility function:

```typescript
// aggregation.utils.ts
export function mergeAggregation(
  currentAggregate: FlatAggregation,
  newItem: FlatAggregation
): FlatAggregation {
  const totalCount = currentAggregate.count + newItem.count;
  return {
    ...currentAggregate,
    count: totalCount,
    avgPricePerMeter: (
      (currentAggregate.count * currentAggregate.avgPricePerMeter +
       newItem.count * newItem.avgPricePerMeter) /
      totalCount
    )
  };
}

// Then:
private aggregateByDistrict(data: FlatAggregation[]): FlatAggregation[] {
  const grouped = new Map<string | undefined, FlatAggregation>();
  data.forEach(item => {
    const key = item.district;
    if (!grouped.has(key)) {
      grouped.set(key, createEmptyAggregation(item));
    }
    grouped.set(key, mergeAggregation(grouped.get(key)!, item));
  });
  // ...
}
```

**Benefit:** Single source of truth for aggregation logic; easier to modify; testable in isolation.

---

## 4. Filter State Management

### Issue: Duplicated filter state across data types
Each data type (aggregation, offers, history) maintains parallel filter arrays:
- `selectedDistricts`, `selectedOfferDistricts`, `selectedHistoryDistricts`
- `selectedRoomCounts`, `selectedOfferRooms`, `selectedHistoryRooms`
- `availableDistricts`, `availableOfferDistricts`, `availableHistoryDistricts` (x3)

**Recommendation:** Create reusable filter state model:

```typescript
// filter-state.model.ts
export interface FilterState {
  selectedDistricts: string[];
  selectedRooms: number[];
  availableDistricts: string[];
  availableRooms: number[];
}

// In component, use:
aggregationFilters: FilterState = { /* ... */ };
offersFilters: FilterState = { /* ... */ };
historyFilters: FilterState & { priceMin?: number; priceMax?: number } = { /* ... */ };
```

**Benefit:** Reduces boilerplate; easier to extend filters later; clearer intent.

---

## 5. Duplicate Method Names & Inconsistent Naming

### Issue: Lines 188 and 285
```typescript
onFiltersChange(): void { ... }   // line 188
onFilterChange(): void { ... }    // line 285
```
Both do identical work. Same with offer and history versions.

**Recommendation:** Use consistent, unambiguous names:
- `onAggregationFiltersChange()`
- `onOffersFiltersChange()`
- `onHistoryFiltersChange()`

**Benefit:** Clarity; prevents copy-paste bugs.

---

## 6. Polling & Subscription Management

### Issue: Manual interval polling (lines 87–101) + manual status checks
```typescript
interval(2000)
  .pipe(
    switchMap(() => this.flatsService.getScrapeStatus()),
    takeUntil(this.destroy$)
  )
  .subscribe((status) => { ... });
```

Hard-coded 2000ms magic number; pattern not reusable.

**Recommendation:** Extract to reusable polling service:

```typescript
// polling.service.ts
export class PollingService {
  poll<T>(source: () => Observable<T>, intervalMs: number, destroy$: Subject<void>): Observable<T> {
    return interval(intervalMs).pipe(
      switchMap(() => source()),
      takeUntil(destroy$)
    );
  }
}

// In component:
constructor(
  private flatsService: FlatsService,
  private pollingService: PollingService,
  private destroy$: Subject<void>
) {}

ngOnInit() {
  this.pollingService.poll(
    () => this.flatsService.getScrapeStatus(),
    2000,
    this.destroy$
  ).subscribe(status => this.updateScrapingStatus(status));
}
```

**Also consider:** Using `async` pipe with `BehaviorSubject` in the service to eliminate subscription management entirely.

**Benefit:** Reusable; centralized polling logic; testable.

---

## 7. Chart Library Type Safety

### Issue: Chart.js loaded via window casting without types (lines 681–682)
```typescript
const Chart = (window as any).Chart;
this.chart = new Chart(ctx, chartConfig);
```

**Recommendation:** Use `declare global` + TypeScript types or import properly:

```typescript
// declare-chart.d.ts
declare global {
  interface Window {
    Chart: typeof import('chart.js').Chart;
  }
}

// Or better: import Chart.js as a dependency:
// npm install chart.js
import { Chart } from 'chart.js';
```

**Benefit:** Type safety; IDE autocomplete; catch errors at compile time.

---

## 8. Magic Numbers & Constants

### Issue: Hard-coded values scattered throughout:
- `2000` ms polling interval (line 87)
- `100` ms setTimeout for chart render (line 448)
- Color array (lines 686–693)
- `Math.round(x * 10) / 10` rounding logic (lines 470, 566)

**Recommendation:** Extract to constants file:

```typescript
// dashboard.constants.ts
export const DASHBOARD_CONFIG = {
  POLLING_INTERVAL_MS: 2000,
  CHART_RENDER_DELAY_MS: 100,
  ROOM_ROUNDING_PRECISION: 10, // rounds to 0.1
  CHART_COLORS: [
    '#201923', '#fcff5d', '#7dfc00', // ...
  ]
} as const;

// In component:
interval(DASHBOARD_CONFIG.POLLING_INTERVAL_MS)
setTimeout(() => this.renderChart(), DASHBOARD_CONFIG.CHART_RENDER_DELAY_MS);
```

**Benefit:** Single source of truth for tuning; easier testing with different values; readability.

---

## 9. Error Handling Consistency

### Issue: Error handling duplicated in every `.subscribe()` call
Lines 133–136, 298–300, 371–373, etc. all repeat:
```typescript
error: (err) => {
  this.error = `Error message: ${err.message}`;
  this.loading = false; // sometimes
}
```

**Recommendation:** Extract to error handling utility:

```typescript
// error-handler.service.ts
export class ErrorHandlerService {
  handle(err: any, contextLabel?: string): string {
    const message = err?.message || 'Unknown error';
    console.error(`Error ${contextLabel || ''}:`, err);
    return `${contextLabel ? contextLabel + ': ' : ''}${message}`;
  }
}

// In component:
error: (err) => {
  this.error = this.errorHandler.handle(err, 'Failed to load data');
  this.loading = false;
}
```

**Benefit:** Consistent error messages; centralized logging/tracking; DRY.

---

## 10. Chart Rendering Decoupling

### Issue: Chart logic tightly coupled to component (lines 608–683)
- Manual canvas access via `@ViewChild`
- Chart config creation inline
- `renderChart()` called with `setTimeout` (code smell)

**Recommendation:** Extract to dedicated service:

```typescript
// chart-renderer.service.ts
export class ChartRendererService {
  private chart: Chart | null = null;

  render(
    canvas: HTMLCanvasElement,
    data: ChartData[],
    groupBy: 'district' | 'rooms'
  ): void {
    if (this.chart) this.chart.destroy();
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const datasets = this.buildDatasets(data, groupBy);
    const config = this.buildChartConfig(datasets, groupBy);
    this.chart = new Chart(ctx, config);
  }

  destroy(): void {
    this.chart?.destroy();
  }

  private buildDatasets(data: ChartData[], groupBy: string): any[] { /* ... */ }
  private buildChartConfig(datasets: any[], groupBy: string): any { /* ... */ }
}
```

**Benefit:** Chart logic testable; reusable; component logic cleaner.

---

## 11. Async Pipe Instead of Manual Subscriptions

### Issue: Manual subscription management (lines 121–137, 360–375, etc.)
```typescript
this.flatsService.getAggregated().subscribe({
  next: (response) => { /* handle */ },
  error: (err) => { /* handle */ },
  complete: () => { }
});
```

**Recommendation:** Use `async` pipe where possible:

```typescript
// dashboard.component.ts
aggregatedData$: Observable<FlatAggregation[]>;

ngOnInit() {
  this.aggregatedData$ = this.flatsService.getAggregated().pipe(
    map(response => response.data || []),
    catchError(err => {
      this.error = this.errorHandler.handle(err);
      return of([]);
    })
  );
}

// dashboard.component.html
<div *ngIf="(aggregatedData$ | async) as data">
  {{ data | json }}
</div>
```

**Benefit:** Automatic unsubscribe via `OnDestroy`; cleaner component; reactive pipeline.

---

## 12. Immutability & Functional Patterns

### Issue: Array mutations in place
Lines 493–510 (toggle filters):
```typescript
if (checked) {
  if (!this.selectedHistoryDistricts.includes(district)) {
    this.selectedHistoryDistricts.push(district);  // mutation
  }
} else {
  this.selectedHistoryDistricts = this.selectedHistoryDistricts.filter(...);  // creates new
}
```

Inconsistent: sometimes mutates, sometimes creates new array.

**Recommendation:** Always create new arrays:

```typescript
toggleDistrictFilter(district: string, checked: boolean): void {
  this.selectedHistoryDistricts = checked
    ? [...this.selectedHistoryDistricts, district].filter(d => [...new Set([d])].length)
    : this.selectedHistoryDistricts.filter(d => d !== district);
  this.onHistoryFiltersChange();
}

// Or use Set for uniqueness:
toggleDistrictFilter(district: string, checked: boolean): void {
  const set = new Set(this.selectedHistoryDistricts);
  checked ? set.add(district) : set.delete(district);
  this.selectedHistoryDistricts = Array.from(set);
  this.onHistoryFiltersChange();
}
```

**Benefit:** Easier debugging; change detection; predictable behavior.

---

## 13. Type Safety: Remove `any` Types

### Issue: Multiple uses of `any`:
- Line 46: `chart: any;`
- Line 681: `(window as any).Chart`
- Line 39: `Observable<any>` for cancel

**Recommendation:** Define proper types:

```typescript
// models/chart.model.ts
import Chart from 'chart.js/auto';
export type ChartInstance = InstanceType<typeof Chart>;

// models/scrape.model.ts
export interface CancelScrapeResponse {
  success: boolean;
  message?: string;
}

// In service:
cancelScrape(): Observable<CancelScrapeResponse> {
  return this.http.post<CancelScrapeResponse>(...);
}

// In component:
chart: ChartInstance | null = null;
```

**Benefit:** Type safety; IDE support; catch errors early.

---

## 14. Responsive & Accessible UI Helpers

### Issue: URL validation logic in component (lines 429–437)

**Recommendation:** Move to service:

```typescript
// url.service.ts
export class UrlService {
  isValid(url: string | undefined): boolean {
    if (!url) return false;
    try {
      new URL(url);
      return true;
    } catch {
      return false;
    }
  }
  
  sanitize(url: string | undefined): SafeUrl {
    return url && this.isValid(url)
      ? this.sanitizer.bypassSecurityTrustUrl(url)
      : this.sanitizer.bypassSecurityTrustUrl('');
  }
}
```

**Benefit:** Testable; reusable; consistent URL handling.

---

## 15. Performance: Eliminate setTimeout Workaround

### Issue: Line 448 — `setTimeout(() => this.renderChart(), 100);`
Indicates a timing issue with Angular change detection or template rendering.

**Recommendation:** 
- Use `AfterViewInit` lifecycle hook (already done at line 104, but not called for history tab update)
- Or use explicit change detection: `ChangeDetectorRef.detectChanges()`
- Or refactor to reactive: emit chart data changes via Observable

```typescript
constructor(private cdr: ChangeDetectorRef) {}

loadHistory(): void {
  this.flatsService.getHistory().subscribe({
    next: (response) => {
      if (response.success && response.data) {
        this.historyData = response.data;
        this.updateHistoryChart();
        this.cdr.detectChanges(); // Force change detection
        this.renderChart();
      }
    }
  });
}
```

**Benefit:** Faster initial render; better performance; cleaner code.

---

## 16. Extract Price Parsing Logic

### Issue: Line 603–606 — `parsePrice()` is trivial but useful to extract:

**Recommendation:** Move to utility or model method:

```typescript
// flat-aggregation.model.ts
export class FlatAggregation {
  // ... existing fields

  getPricePerMeter(): number {
    return (this.avgPricePerMeter ?? 0) || 0;
  }
}

// Or utility:
// price.utils.ts
export function parsePrice(price: number | undefined | null): number {
  return (price && Number.isFinite(price)) ? price : 0;
}
```

**Benefit:** Centralized parsing; reusable across components; testable.

---

## 17. Service Layer Expansion

### Issue: `FlatsService` is too thin; component has too much logic

**Recommendation:** Add domain services for distinct concerns:

```
core/services/
  ├── flats.service.ts (API layer - already exists)
  ├── aggregation.service.ts (aggregation + filtering logic)
  ├── offers.service.ts (offer filtering + display logic)
  ├── history.service.ts (history aggregation + chart data prep)
  ├── scraping-status.service.ts (polling + status tracking)
  ├── filter.service.ts (reusable filter utilities)
  └── error-handler.service.ts (centralized error handling)
```

**Benefit:** Component becomes a dumb container; business logic centralized; easier testing.

---

## 18. Testing Improvements

### Current state: Limited test coverage visible
- `app.component.spec.ts` exists but likely trivial
- `dashboard.component.spec.ts` exists
- No service tests visible

### Recommendation:
1. **Mock service tests**: Test each service independently
   ```typescript
   // aggregation.service.spec.ts
   describe('AggregationService', () => {
     it('should merge aggregations with correct weighted average', () => {
       const agg1 = { count: 10, avgPricePerMeter: 100 };
       const agg2 = { count: 20, avgPricePerMeter: 120 };
       const result = service.merge(agg1, agg2);
       expect(result.avgPricePerMeter).toBe(113.33);
     });
   });
   ```

2. **Component tests**: Test with mock services
   ```typescript
   // dashboard.component.spec.ts
   beforeEach(() => {
     TestBed.configureTestingModule({
       providers: [
         { provide: FlatsService, useValue: mockFlatsService },
         // ...
       ]
     });
   });
   ```

3. **Add e2e tests**: Filter interactions, chart rendering, etc.

**Benefit:** Confidence in refactoring; catch regressions; document expected behavior.

---

## 19. Accessibility (a11y) Review

### Recommendation:
- Add `aria-label` to filter buttons and checkboxes
- Ensure chart is described with `<figure>` + `<figcaption>`
- Add `role="status"` to loading/error messages
- Test with keyboard navigation

```html
<!-- Example -->
<label>
  <input 
    type="checkbox" 
    [checked]="selectedDistricts.includes(district)"
    (change)="toggleDistrict(district, $event.target.checked)"
    [attr.aria-label]="'Filter by ' + district"
  />
  {{ district }}
</label>

<div role="status" aria-live="polite" *ngIf="loading">Loading...</div>
```

---

## 20. CSS Organization & Component Styling

### Current: `styleUrls: ['./dashboard.component.scss']`

**Recommendation:**
- Split SCSS by tab/feature (if not already done)
- Use CSS variables for colors/spacing (matching dashboard constants)
- Consider BEM or similar naming convention

```scss
// dashboard.component.scss
:root {
  --chart-color-primary: #201923;
  --polling-interval: 2000ms;
}

.dashboard {
  &__tab { /* ... */ }
  &__filter { /* ... */ }
  &__chart { /* ... */ }
}
```

---

## Priority Ranking

### High Priority (Do First)
1. **Decompose DashboardComponent** (item 1) — reduces complexity
2. **Extract filter logic** (item 2) — enables reuse + testing
3. **Remove `any` types** (item 13) — improves type safety
4. **Extract aggregation logic** (item 3) — reduce duplication

### Medium Priority (Next Phase)
5. **Polling service** (item 6) — centralize recurring pattern
6. **Error handling service** (item 9) — DRY + consistency
7. **Constants extraction** (item 8) — easier to test/tune
8. **Chart service extraction** (item 10) — decouple library

### Low Priority (Nice-to-Have)
9. **Async pipe adoption** (item 11) — nice but requires refactoring
10. **Immutability patterns** (item 12) — cleaner but not critical
11. **a11y improvements** (item 19) — important for UX but separate effort
12. **Test coverage** (item 18) — ongoing activity

---

## Estimated Effort

| Refactoring | Effort | Benefit | Priority |
|---|---|---|---|
| Decompose component | 4-6 hours | 🔴 High | 1st |
| Extract filters | 2-3 hours | 🔴 High | 2nd |
| Remove `any` types | 1-2 hours | 🔴 High | 3rd |
| Aggregation utils | 1 hour | 🟡 Medium | 4th |
| Polling service | 1-2 hours | 🟡 Medium | 5th |
| Error handling | 1 hour | 🟡 Medium | 6th |
| Constants | 30 min | 🟡 Medium | 7th |
| Chart service | 2-3 hours | 🟡 Medium | 8th |
| Async pipe | 2-3 hours | 🟢 Low | Later |
| Full test suite | 4-6 hours | 🟢 Low | Ongoing |

---

## Summary

**Key Takeaway:** The dashboard component is a classic case of "doing too much." Break it into feature-specific sub-components, extract business logic into services, and use TypeScript's type system more aggressively. Start with decomposition and filter extraction for maximum impact with moderate effort.
