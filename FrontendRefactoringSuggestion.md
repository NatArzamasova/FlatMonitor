# Frontend Refactoring Suggestions

## Overview
The dashboard component is doing too much and would benefit from breaking into smaller, focused components and services. Current size: 696 lines in component + 414 lines in template.

---

## Critical Issues

### 1. **Giant Component (God Component Antipattern)**
**Severity:** High  
**Impact:** Testability, maintainability, reusability  

The `DashboardComponent` combines three distinct data domains (aggregated data, current offers, history) with their own filters, aggregations, and UI logic.

**Suggested Fix:**
- Extract **AggregatedDataTabComponent** (handles dashboard tab: raw data, filters, grouping)
- Extract **CurrentOffersTabComponent** (handles offers tab: filtering, display)
- Extract **HistoryTabComponent** (handles history tab: chart rendering, district/room filtering)
- Keep only tab switching logic in DashboardComponent

**Example structure:**
```
dashboard/
  dashboard.component.ts (tab management only)
  tabs/
    aggregated-data-tab/
      aggregated-data-tab.component.ts
      aggregated-data-tab.component.html
    current-offers-tab/
      current-offers-tab.component.ts
      current-offers-tab.component.html
    history-tab/
      history-tab.component.ts
      history-tab.component.html
```

---

### 2. **Duplicate Filter Logic**
**Severity:** Medium  
**Impact:** Maintainability, DRY principle  

Three nearly identical filter update methods:
- `updateAvailableFilters()` (line 140)
- `updateAvailableOfferFilters()` (line 378)
- `updateAvailableHistoryFilters()` (line 461)

**Suggested Fix:**
- Create a generic `FilterService` with a reusable method:
```typescript
updateAvailableOptions<T>(
  data: T[],
  extractDistrict: (item: T) => string | undefined,
  extractRooms: (item: T) => number | undefined
): { districts: string[]; rooms: number[] }
```

---

### 3. **Chart Rendering Should Be Separated**
**Severity:** Medium  
**Impact:** Reusability, testability  

Chart logic is hardcoded in component:
- `renderChart()` (line 608) — 75 lines of Chart.js configuration
- `aggregateByDistrictHistory()` (line 522) — chart data transformation
- `aggregateByRoomsHistory()` (line 562) — similar transformation
- Hardcoded color array (line 686)

**Suggested Fix:**
- Create `HistoryChartComponent` (child component) that accepts `chartData` input
- Create `ChartService` to encapsulate:
  - Chart configuration
  - Color palette management
  - Data aggregation logic
  - Chart lifecycle (destroy on component destroy)

```typescript
// chart.service.ts
export class ChartService {
  aggregateByDistrict(data: FlatHistory[], selectedFilters: string[]): ChartData[]
  aggregateByRooms(data: FlatHistory[], selectedFilters: number[]): ChartData[]
  getChartConfig(data: ChartData[], groupBy: 'district' | 'rooms'): ChartConfiguration
  getColorForIndex(index: number): string
}
```

---

### 4. **Multiple Loading/Error States**
**Severity:** Low-Medium  
**Impact:** Consistency, maintainability  

Scattered state management:
- `loading`, `loadingOffers`, `loadingHistory` (separate flags)
- `error` and `success` (global, shared by all tabs)
- `scraping`, `scrapedCount`

**Suggested Fix:**
- Create a `LoadingState` utility/service:
```typescript
interface TabLoadingState {
  isLoading: boolean;
  error: string | null;
  success: string | null;
  lastUpdated: Date | null;
}

// In component:
aggregatedDataState = new LoadingState();
offersState = new LoadingState();
historyState = new LoadingState();
```

Or use RxJS state management (BehaviorSubject):
```typescript
aggregatedDataState$ = new BehaviorSubject<TabLoadingState>(initialState);
```

---

### 5. **Type Unsafety in Template**
**Severity:** Low  
**Impact:** Maintainability, bug risk  

Uses `$any()` to bypass type checking (line 368):
```html
(change)="toggleDistrictFilter(district, $any($event.target).checked)"
```

**Suggested Fix:**
- Create a helper method in component:
```typescript
onDistrictCheckboxChange(district: string, event: Event): void {
  const checked = (event.target as HTMLInputElement).checked;
  this.toggleDistrictFilter(district, checked);
}
```

---

### 6. **Polling Logic Hardcoded**
**Severity:** Low-Medium  
**Impact:** Testability, configuration  

Scraping status polling is inline in ngOnInit (line 87):
```typescript
interval(2000).pipe(
  switchMap(() => this.flatsService.getScrapeStatus()),
  // ...
)
```

**Suggested Fix:**
- Create `ScrapingStatusService` that handles polling internally:
```typescript
// scraping-status.service.ts
export class ScrapingStatusService {
  scrapeStatus$ = new BehaviorSubject<ScrapingStatusResponse>(initialState);
  
  startPolling(intervalMs: number = 2000): void {
    // Polling logic here
  }
  
  stopPolling(): void { }
}
```

---

### 7. **Duplicate Method Names**
**Severity:** Low  
**Impact:** Confusion, potential bugs  

- `onFiltersChange()` (line 188) and `onFilterChange()` (line 285) likely typo
- Methods should be renamed to be clear:
  - `onAggregatedFiltersChange()`
  - `onOfferFiltersChange()`
  - `onHistoryFiltersChange()`

---

### 8. **Aggregation Logic Could Be Reusable**
**Severity:** Low  
**Impact:** Testability, reusability  

Aggregation methods are component-specific:
- `aggregateByDistrict()` (line 192)
- `aggregateByRooms()` (line 225)
- `aggregateAll()` (line 258)

**Suggested Fix:**
- Create `AggregationService` with generic aggregation logic:
```typescript
aggregateByKey<T, K>(
  data: T[],
  keyExtractor: (item: T) => K,
  aggregator: (items: T[]) => T
): Map<K, T>
```

---

### 9. **Template Complexity**
**Severity:** Low  
**Impact:** Maintainability, readability  

414-line template with repeated patterns for filters, tables, and modals.

**Suggested Fix:**
- Extract filter controls to reusable component:
```html
<app-filter-control
  [availableDistricts]="availableDistricts"
  [selectedDistricts]="selectedDistricts"
  (districtsChange)="selectedDistricts = $event"
></app-filter-control>
```

- Extract table rendering to components
- Use `*ngTemplateOutlet` for conditional UI patterns

---

### 10. **Missing Type Definitions**
**Severity:** Low  
**Impact:** Type safety  

`FlatHistory` model (flat-history.model.ts) is minimal:
```typescript
export interface FlatHistory {
  scrapingDate?: string;
  district?: string;
  roomsCount?: number;
  avgPricePerMeter?: number;
  count?: number;
}
```

Unlike `FlatOffer`, it's missing:
- `source` field (which source did this come from?)
- Timestamp typing (should be `Date` not `string`?)
- Better null safety

**Suggested Fix:**
```typescript
export interface FlatHistory {
  scrapingDate: string; // ISO format
  district: string | null;
  roomsCount: number | null;
  avgPricePerMeter: number;
  count: number;
  source?: string; // which source
}
```

---

## Refactoring Roadmap

### Phase 1: Extract Services (Low Risk)
1. Create `FilterService` for reusable filter logic
2. Create `ChartService` to encapsulate chart configuration
3. Create `AggregationService` for aggregation logic

### Phase 2: Extract Child Components (Medium Risk)
1. Create `HistoryChartComponent`
2. Create `FilterControlComponent` (reusable filter UI)
3. Create `TableComponent` (reusable table rendering)

### Phase 3: Extract Tab Components (High Impact)
1. Create `AggregatedDataTabComponent`
2. Create `CurrentOffersTabComponent`
3. Create `HistoryTabComponent`
4. Simplify `DashboardComponent` to just manage tabs

### Phase 4: Improve State Management (Optional)
1. Consider RxJS state management (BehaviorSubject) for each tab
2. Create `TabLoadingState` utility
3. Move scraping polling to `ScrapingStatusService`

---

## Additional Improvements

### Testing Strategy
- Unit test each service independently
- Test filter/aggregation logic in isolation
- Mock API responses in component tests
- Test chart rendering without actual Chart.js

### Performance
- Consider `OnPush` change detection strategy after refactoring
- Memoize aggregation results
- Use `trackBy` in `*ngFor` loops for large lists

### Accessibility
- Template uses `$any()` which hides type info — improve type safety
- Add aria-labels to filter controls
- Ensure chart canvas has proper ARIA labels

---

## Code Smell Checklist
- [ ] Component < 300 lines
- [ ] No duplicate method names
- [ ] Filter logic is reusable and testable
- [ ] Chart logic is in a service, not component
- [ ] No `$any()` in template
- [ ] Loading states are centralized
- [ ] Each component has single responsibility
- [ ] All models are properly typed (no optional everything)

