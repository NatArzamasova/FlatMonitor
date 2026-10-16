# Story 4: Application & Frontend — ASP.NET Core API + Angular Dashboard

## Goal
Transform FlatMonitor from a console-only application into a full-stack web application that serves an Angular dashboard while retaining the ability to trigger scraping on-demand. Expose aggregated flat statistics via a REST API endpoint that groups by postal code, district, room count, and availability.

## Architecture Overview

### Design: Hybrid Application Model
The application transitions from a pure console app (`dotnet run -- scrape`) to an ASP.NET Core web application with two execution paths:

1. **Web Server (default)** — launches on `http://localhost:5000` (or configured port)
   - Serves REST API endpoints for querying aggregated flat data
   - Serves Angular SPA (Single Page Application)
   - Hosts manual scrape trigger endpoint (`POST /api/scrape`)

2. **Scrape Mode (legacy)** — optional CLI argument `dotnet run -- scrape`
   - Maintains backward compatibility for scheduled CI/CD pipelines
   - Executes scrape immediately and exits
   - (Optional: refactor into a background job within the web app instead)

### Technology Stack
- **Backend:** ASP.NET Core 10 (C#), Entity Framework Core, SQLite
- **Frontend:** Angular 18+, TypeScript, Bootstrap/Material Design
- **API:** RESTful, JSON request/response
- **Database:** SQLite (existing `data/flats.db`)

---

## Acceptance Criteria

### Backend
- [ ] ASP.NET Core Minimal APIs or MVC structure (with dependency injection)
- [ ] `Program.cs` configures the web host and DI container
- [ ] Console app entrypoint: if CLI arg `scrape` is present, execute scraper and exit; otherwise, start web server
- [ ] API endpoint `GET /api/flats/aggregated` returns aggregated statistics
- [ ] API endpoint `POST /api/scrape` triggers an on-demand scrape (background or synchronous)
- [ ] All responses use a consistent JSON structure with proper HTTP status codes
- [ ] Unit tests cover API endpoints and aggregation logic

### Frontend
- [ ] Angular app bootstraps and serves at `/` (SPA)
- [ ] Dashboard displays aggregated data in a table or card grid
- [ ] Filters/UI controls for postal code, district, rooms, availability (optional but recommended)
- [ ] "Manual Scrape" button that triggers `POST /api/scrape` and shows feedback
- [ ] Data auto-refreshes on page load and after scrape
- [ ] Responsive design (mobile-friendly)
- [ ] TypeScript with proper type safety

### Integration
- [ ] `dotnet build` compiles both backend and frontend
- [ ] `dotnet run` launches the web server on a known port
- [ ] SQLite database is auto-migrated on app startup (if needed)
- [ ] CI/CD pipeline (`gitlab-ci.yml`) updated to trigger scrape via API endpoint or CLI

---

## Implementation Tasks

### Phase 1: Transition to ASP.NET Core Web App

#### 1.1 Update Project Structure
- [ ] Rename or create a new ASP.NET Core project (if starting fresh):
  - File: `FlatMonitor/FlatMonitor.csproj` updated to include `<OutputType>WinExe</OutputType>` → removed (console no longer primary)
  - Or: keep as-is and add a `.Web` suffix project if splitting concerns
- [ ] Add NuGet packages:
  - `Microsoft.AspNetCore.App` (implicit with ASP.NET Core SDK)
  - `Microsoft.EntityFrameworkCore.Sqlite` (if not present)
  - `Swashbuckle.AspNetCore` (optional: for Swagger/OpenAPI docs)

#### 1.2 Create or Update `Program.cs`
- [ ] Refactor `Program.cs` to:
  - Check for CLI argument: `if (args.Contains("scrape")) { ... scrape and exit ... }`
  - Otherwise, build and run the web host via `WebApplication.CreateBuilder(args).Build().RunAsync()`
- [ ] Configure dependency injection:
  - Register `ISnapshotRepository` as singleton
  - Register `ScraperCoordinator` as scoped/transient
  - Register Entity Framework `FlatDbContext`
- [ ] Configure Kestrel to listen on `http://localhost:5000` (configurable via `appsettings.json`)
- [ ] Middleware: Add `.UseRouting()`, `.MapControllers()` or `.MapGet/MapPost()` for minimal APIs

#### 1.3 Set Up Entity Framework DbContext
- [ ] Create or update `FlatDbContext` in `Data/FlatDbContext.cs`:
  - Models: `Flat`, `AggregatedStatistic` (if not already defined)
  - DbSet for flats: `DbSet<Flat> Flats { get; set; }`
  - Configure mappings to SQLite table schema
- [ ] Add migration for schema (if needed):
  - `dotnet ef migrations add InitialCreate` → creates migration file
  - `dotnet ef database update` → applies to SQLite (or called at app startup)

---

### Phase 2: Implement REST API Endpoints

#### 2.1 Aggregation Endpoint: `GET /api/flats/aggregated`

**Purpose:** Return aggregated statistics grouped by:
- PostalCode
- District
- RoomsCount
- Availability

**Response Schema:**
```json
{
  "success": true,
  "data": [
    {
      "postalCode": "76137",
      "district": "Karlsruhe-Innenstadt",
      "roomsCount": 3,
      "availability": "sofort",
      "count": 15,
      "avgPricePerMeter": 4250.50
    },
    {
      "postalCode": "76137",
      "district": "Karlsruhe-Innenstadt",
      "roomsCount": 3,
      "availability": "vereinbart",
      "count": 8,
      "avgPricePerMeter": 4100.00
    }
  ],
  "message": "Aggregated successfully"
}
```

**Implementation:**
- [ ] Create controller: `Controllers/FlatsController.cs`
- [ ] Method: `[HttpGet("aggregated")] public async Task<ActionResult<ApiResponse<List<FlatAggregation>>>> GetAggregatedAsync()`
- [ ] LINQ query:
  ```csharp
  var aggregated = await _context.Flats
    .GroupBy(f => new { f.PostalCode, f.District, f.RoomsCount, f.Availability })
    .Select(g => new FlatAggregation
    {
      PostalCode = g.Key.PostalCode,
      District = g.Key.District,
      RoomsCount = g.Key.RoomsCount,
      Availability = g.Key.Availability,
      Count = g.Count(),
      AvgPricePerMeter = g.Average(f => f.PricePerMeter)
    })
    .ToListAsync();
  ```
- [ ] Return with consistent API response wrapper

#### 2.2 Manual Scrape Trigger: `POST /api/scrape`

**Purpose:** Trigger an on-demand scrape from the web app (not via CLI).

**Request:** Empty body or optional JSON payload
```json
{
  "sources": ["OhneMakler", "ImmobilienScout24"]  // optional; default all
}
```

**Response:**
```json
{
  "success": true,
  "message": "Scrape started",
  "scrapedAt": "2026-10-16T14:30:00Z",
  "flatsCount": 42
}
```

**Implementation Options:**
- **Option A (Synchronous):** Block the HTTP request while scraping runs; return only when complete.
  - Pro: Simple; status immediately known.
  - Con: Long-running HTTP request; potential timeout if scrape takes >30s.
  - Code: `var flats = await coordinator.ScrapeAllSourcesAsync(); return Ok(new { ... });`
  
- **Option B (Background Job):** Queue the scrape task and return immediately with a job ID.
  - Pro: Non-blocking; scales better.
  - Con: Requires background job framework (BackgroundTaskQueue, Hangfire, etc.).
  - Code: `_jobQueue.Enqueue(async () => await coordinator.ScrapeAllSourcesAsync());`
  - Add endpoint `GET /api/scrape/{jobId}` to poll status.

- **Recommendation:** Start with Option A (synchronous) for Story 4; can evolve to Option B later if needed.

- [ ] Create controller method: `[HttpPost] public async Task<ActionResult<ScrapeResponse>> ScrapeAsync()`
- [ ] Call `ScraperCoordinator.ScrapeAllSourcesAsync()`
- [ ] Capture count of flats scraped
- [ ] Return response with timestamp and count

#### 2.3 Swagger/OpenAPI Documentation (Optional)
- [ ] Add `.AddSwaggerGen()` and `.UseSwagger()` / `.UseSwaggerUI()` middleware
- [ ] Endpoints are auto-documented at `http://localhost:5000/swagger`
- [ ] Useful for testing API during development

---

### Phase 3: Angular Frontend Setup

#### 3.1 Project Structure
- [ ] Create Angular app in `src/app/` (or `FlatMonitor.Web/ClientApp/src/` if using ASP.NET SPA template)
- [ ] Directory structure:
  ```
  src/
    app/
      core/
        models/
          flat-aggregation.model.ts
          api-response.model.ts
        services/
          flats.service.ts
      shared/
        components/
          (shared UI components)
      features/
        dashboard/
          dashboard.component.ts
          dashboard.component.html
          dashboard.component.scss
      app.component.ts
      app.routing.ts
    assets/
    styles/
    index.html
    main.ts
  angular.json
  tsconfig.json
  package.json
  ```

#### 3.2 Models & Services
- [ ] Create TypeScript models:
  - `FlatAggregation` — matches API response structure
  - `ApiResponse<T>` — wraps all API responses
  - `ScrapeRequest`, `ScrapeResponse`

- [ ] Create `FlatsService`:
  ```typescript
  @Injectable({ providedIn: 'root' })
  export class FlatsService {
    constructor(private http: HttpClient) {}
    
    getAggregated(): Observable<ApiResponse<FlatAggregation[]>> {
      return this.http.get<ApiResponse<FlatAggregation[]>>('/api/flats/aggregated');
    }
    
    triggerScrape(): Observable<ScrapeResponse> {
      return this.http.post<ScrapeResponse>('/api/scrape', {});
    }
  }
  ```

#### 3.3 Dashboard Component
- [ ] Create `DashboardComponent`:
  - `ngOnInit()`: Load aggregated data on component init
  - `refresh()`: Manual refresh button
  - `scrape()`: Call `triggerScrape()`, show spinner/toast feedback
  - Template: Display data in table or card grid

- [ ] Table columns:
  - Postal Code
  - District
  - Rooms Count
  - Availability
  - Count (of flats)
  - Avg Price/m² (formatted as currency: €)

- [ ] Optional features:
  - Sorting by column
  - Filtering by postal code/district/rooms
  - Pagination (if large dataset)
  - Chart visualization (bars/heatmap)

#### 3.4 UI/UX Features
- [ ] Loading spinner while fetching data
- [ ] Error message display (if API fails)
- [ ] "Manual Scrape" button:
  - Disabled during scrape
  - Shows progress spinner
  - Toast notification on success ("Scraped 42 flats at 14:30")
  - Toast notification on error
- [ ] Last-updated timestamp
- [ ] Responsive layout (mobile-first)

#### 3.5 Styling
- [ ] Use Bootstrap 5 or Angular Material for consistency
- [ ] Dark mode support (optional)
- [ ] Color scheme: professional, minimal

---

### Phase 4: Integration & Build

#### 4.1 ASP.NET Core SPA Configuration
- [ ] In `Program.cs`, add static file middleware to serve Angular build:
  ```csharp
  app.UseDefaultFiles();  // Serves index.html by default
  app.UseStaticFiles();   // Serves from wwwroot/
  ```

- [ ] Configure `angular.json` to output built app to `wwwroot/`:
  ```json
  {
    "projects": {
      "FlatMonitor": {
        "architect": {
          "build": {
            "options": {
              "outputPath": "../../../wwwroot"
            }
          }
        }
      }
    }
  }
  ```

#### 4.2 Build Process
- [ ] Update `.csproj` to build Angular as part of `dotnet build`:
  ```xml
  <Target Name="BuildAngular" BeforeTargets="Build">
    <Exec Command="npm install" WorkingDirectory="ClientApp" />
    <Exec Command="npm run build" WorkingDirectory="ClientApp" />
  </Target>
  ```

- [ ] Or: Document separate build steps:
  1. `cd ClientApp && npm install && npm run build`
  2. `dotnet build`

#### 4.3 Development Experience
- [ ] For local development:
  - Terminal 1: `cd ClientApp && npm start` (Angular dev server on port 4200)
  - Terminal 2: `dotnet run` (ASP.NET Core on port 5000)
  - Angular proxy config routes `/api/*` to `http://localhost:5000`

- [ ] For production:
  - Build Angular → outputs to `wwwroot/`
  - Build & run ASP.NET Core → serves both API and static files

---

### Phase 5: Testing & Validation

#### 5.1 Backend Tests
- [ ] Unit tests for `FlatsController.GetAggregatedAsync()`:
  - Mock `IDbContext`
  - Verify aggregation logic
  - Check response format
  
- [ ] Unit tests for `FlatsController.ScrapeAsync()`:
  - Mock `ScraperCoordinator`
  - Verify response includes flats count and timestamp
  - Test error handling (scraper fails)

#### 5.2 Frontend Tests
- [ ] Unit tests for `FlatsService`:
  - Mock `HttpClient`
  - Test `getAggregated()` returns observable
  - Test `triggerScrape()` POSTs to correct endpoint

- [ ] Component tests for `DashboardComponent`:
  - Mock `FlatsService`
  - Verify table renders aggregated data
  - Verify "Scrape" button calls service
  - Verify loading spinner shows/hides

#### 5.3 Integration Testing
- [ ] Build and run full stack locally
- [ ] Manually test:
  - Open `http://localhost:5000/` → Angular app loads
  - Click "Refresh" → table updates with data
  - Click "Manual Scrape" → scrape runs, spinner shows, table updates
  - Check browser DevTools → API calls to `/api/flats/aggregated` and `/api/scrape` work

#### 5.4 Backward Compatibility
- [ ] Test `dotnet run -- scrape` still works (CLI mode)
- [ ] Verify snapshot files are created in `data/snapshots/`

---

## Database Schema Considerations

### Current Schema (from prior stories)
- Table `Flats` with columns: `FlatId`, `Source`, `Price`, `PricePerMeter`, `PostalCode`, `District`, `RoomsCount`, `Availability`, `Url`, `ScrapedAtUtc`, etc.

### Required for This Story
- Ensure `PostalCode`, `District`, `RoomsCount`, `Availability`, `PricePerMeter` are all populated and indexed
- No new tables needed; existing `Flats` table is sufficient

### Optional Improvements
- Add index on `(PostalCode, District, RoomsCount, Availability)` for faster aggregation queries
- Add index on `Source` if filtering by source later

---

## CI/CD Updates

### Update `.gitlab-ci.yml`
- [ ] Build stage now includes Angular build:
  ```yaml
  build:
    script:
      - cd FlatMonitor/ClientApp && npm install && npm run build && cd ../..
      - dotnet build
  ```

- [ ] Scheduled scrape job now uses API instead of CLI (optional):
  ```yaml
  scheduled-scrape:
    script:
      - dotnet run  # Start server
      - curl -X POST http://localhost:5000/api/scrape  # Trigger scrape via API
  ```
  - Or: Keep CLI mode `dotnet run -- scrape` for simplicity

---

## Success Metrics

- ✅ User can open `http://localhost:5000` and see an Angular dashboard
- ✅ Dashboard displays aggregated flat data (count & avg price per m²)
- ✅ "Manual Scrape" button triggers a scrape and updates the table
- ✅ API returns consistent JSON responses with proper error handling
- ✅ Unit tests cover backend and frontend logic
- ✅ `dotnet run -- scrape` still works for CI/CD backward compatibility
- ✅ Full build (backend + frontend) completes with `dotnet build`

---

## Out of Scope (Future Stories)
- Advanced filtering/search UI
- Charting and data visualization beyond basic tables
- Real-time WebSocket updates (push data instead of pull)
- Multi-user authentication & authorization
- Docker containerization
- Deployment to cloud (Azure, AWS, etc.)
