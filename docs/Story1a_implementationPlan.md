# Story 1a: Write Snapshots to SQLite — Implementation Plan (EF Core)

See `docs/STORY_1A_SNAPSHOT_TO_DB.md` for the original spec. This plan uses **Entity Framework Core** 
instead of raw SQL for cleaner connection pooling, transaction management, and testability.

## Implementation Phases

- **Phase 1: EF Core DbContext & Repository** — ✅ **DONE**
  - Add `Microsoft.EntityFrameworkCore.Sqlite` package (version 10.0.x to match net10.0):
    - `Directory.Packages.props`: `<PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />`
    - `workInProgress/workInProgress.csproj`: `<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />`
  - Create `Data/ListingsDbContext.cs` — inherits `DbContext`, has `DbSet<Listing> Listings` property
    - Constructor takes `DbContextOptions<ListingsDbContext>` (dependency injection friendly)
    - `OnModelCreating` configures the schema via fluent API: `Listings` table, all columns, `ListingId` PK, indexes on `ScrapedAtUtc`, `PostalCode`, `District`
    - `Details` fields flattened into `Listings` table (no separate `Details` table — `Details` is just a C# navigation/grouping, not a DB structure)
  - Create `Storage/ListingsRepository.cs` — wraps the `DbContext`:
    - Constructor `(DbContextOptions<ListingsDbContext>? options = null)` — if null, defaults to SQLite at `data/listings.db`
    - `InitializeAsync(CancellationToken ct = default)` — calls `Database.EnsureCreatedAsync()` to create schema on-demand
    - `UpsertListingsAsync(List<Listing> listings, CancellationToken ct = default)` — uses EF Core's `AddOrUpdate` or `ExecuteUpdateAsync` patterns (or explicit upsert logic with `Find` + `Update`)
    - Owns the `DbContext` lifetime (`IAsyncDisposable`)
  - Tests: `workInProgress.Tests/ListingsRepositoryTests.cs` (NUnit + FluentAssertions):
    - Use EF Core's `InMemoryDatabase` provider for fast, isolated tests (no file I/O, no lock contention)
    - Verify initialization creates the Listings table with all columns and indexes
    - Upsert tests verify insert, update, null-handling, multi-row insert
    - No teardown complexity — each test gets a fresh in-memory DB

- **Phase 2: Load Snapshots into DB** — Not started
  - `Storage/SnapshotLoader.cs` — unchanged from original plan

- **Phase 3: Integrate into Scraper** — Not started
  - `ListingScraper.RunAsync()` — unchanged from original plan

## Rationale for EF Core

1. **Connection pooling** — EF Core's `DbContext` factory pattern automatically reuses connections; no manual pooling needed
2. **Transaction safety** — `SaveChangesAsync` is atomic; no risk of partial upserts if a mid-loop error occurs
3. **Test isolation** — in-memory database provider eliminates file I/O and lock issues seen with sqlite on disk
4. **Schema evolution** — easier to add/modify columns later without rewriting SQL
5. **Type safety** — LINQ over raw SQL reduces typos and makes refactoring safer
6. **Matches .NET ecosystem norms** — most .NET projects use EF Core for persistence

## Out of Scope

- EF Core migrations (`dotnet ef migrations add`) — for now, use `EnsureCreatedAsync()` for simplicity; migrations can be added later if desired
- Lazy loading or explicit loading of `Details` — `Details` will be eagerly loaded as nested object on the `Listing` model, not as a separate table
- Complex query patterns — Phase 1 focuses on CRUD; Phase 2 (`report` command) will add aggregation queries
