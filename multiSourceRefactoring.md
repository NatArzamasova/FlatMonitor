# Multi-Source Refactoring Plan: Phase 1

## Overview

This document captures the implementation plan for Phase 1 of evolving the Wohnung Monitor from a single-source scraper (ohne-makler.net only) into a multi-source apartment monitor supporting multiple German real estate websites.

See `WohnungMonitor.md` for the full 9-step roadmap. This plan focuses on **Steps 1-5 only**:
1. Extract `IScraper` interface
2. Rename and refactor `ListingScraper` → `OhneMaklerScraper`
3. Update `OhneMaklerSnapshot` model with `Source` field
4. Refactor `ISnapshotRepository` to be source-aware (interface prep)
5. Create `ScraperCoordinator` orchestrator

## Key Architectural Decisions

### 1. IScraper Interface (Step 1)
Created at `workInProgress/Scraping/IScraper.cs`:
```csharp
public interface IScraper
{
    string Source { get; }
    Task<IEnumerable<Listing>> ScrapeAsync();
}
```

### 2. Source Field Addition (Step 3)
- Added `public string Source { get; init; } = "OhneMakler";` to `Models/Listing.cs`
- Non-required + defaulted for backward compatibility with existing test code
- Flows into JSONL snapshots automatically via `System.Text.Json`
- **Not** added to EF entity `OhneMaklerSnapshot` (deferred to Phase 2 with DB migration)

### 3. OhneMakler Code Relocation
All OhneMakler-specific scraping logic moved to `workInProgress/Scraping/OhneMakler/`:
- `OhneMaklerScraper.cs` (was `ListingScraper.cs`)
- `OhneMaklerListingsPageFetcher.cs` (was `ListingsPageFetcher.cs`)
- `OhneMaklerListingCardParser.cs` (was `ListingCardParser.cs`)
- `OhneMaklerDetailPageParser.cs` (was `DetailPageParser.cs`)
- `OhneMaklerAddressHelper.cs` (was `AddressHelper.cs`)
- `OhneMaklerSelectors.cs` (was `Selectors.cs`)

All these classes moved to namespace `workInProgress.Scraping.OhneMakler`.

### 4. ISnapshotRepository (Step 4)
- Remains **unchanged** in Phase 1 (no signature modifications)
- Added XML-doc comment clarifying it's the SQLite layer, to be made source-aware in Phase 2 alongside DB migration
- This avoids confusion with the conceptual "repository" mentioned in `WohnungMonitor.md` (which refers to the JSONL file layer)

### 5. ScraperCoordinator (Step 5)
New class at `workInProgress/Scraping/ScraperCoordinator.cs`:
```csharp
public sealed class ScraperCoordinator : IAsyncDisposable
{
    public ScraperCoordinator(
        IEnumerable<IScraper> scrapers,
        ISnapshotRepository repository,
        SnapshotWriter snapshotWriter,
        string? dataDirectory = null);

    public async Task ScrapeAllSourcesAsync(CancellationToken ct = default);
    public async ValueTask DisposeAsync();
}
```
Takes over persistence orchestration from `ListingScraper.RunAsync()`:
1. Calls `repository.InitializeAsync(ct)` once
2. For each scraper: calls `ScrapeAsync()` → if empty, logs and skips → upserts listings → writes JSONL snapshot → prints summary

## Explicitly Out of Scope (Phase 2+)

- SQLite schema migration (`Source` column on `Listings`/`Snapshots`, unique constraints)
- `ImmobilienScout24` source implementation
- `report` command and reporting logic
- DI container in `Program.cs` (manual composition only)
- Fixing duplication between `OhneMaklerScraper`'s inline extraction helpers and parser classes
- GitLab CI integration, cross-source deduplication, `Statistics` table changes

## Implementation Status

**Phase 1 Overall:** ✅ DONE (All 3 commits complete, ready to merge)

### Commit 1: Extract IScraper interface and relocate OhneMakler scraping code
**Status:** ✅ DONE (Commit 5323100)

Files created:
- ✅ `workInProgress/Scraping/IScraper.cs`

Files modified:
- ✅ `workInProgress/Models/Listing.cs` — added `Source` property
- ✅ `workInProgress/Scraping/OhneMakler/OhneMaklerScraper.cs` — implements `IScraper`
- ✅ `workInProgress/Program.cs` — references `OhneMaklerScraper` with new namespace

Files moved:
- ✅ `ListingScraper.cs` → `OhneMakler/OhneMaklerScraper.cs`
- ✅ `ListingsPageFetcher.cs` → `OhneMakler/OhneMaklerListingsPageFetcher.cs`
- ✅ `ListingCardParser.cs` → `OhneMakler/OhneMaklerListingCardParser.cs`
- ✅ `DetailPageParser.cs` → `OhneMakler/OhneMaklerDetailPageParser.cs`
- ✅ `AddressHelper.cs` → `OhneMakler/OhneMaklerAddressHelper.cs`
- ✅ `Selectors.cs` → `OhneMakler/OhneMaklerSelectors.cs`

Test files moved to `workInProgress.Tests/OhneMakler/`:
- ✅ `OhneMaklerScraperTests.cs`
- ✅ `OhneMaklerDetailPageParserTests.cs`
- ✅ `OhneMaklerListingCardParserTests.cs`
- ✅ `OhneMaklerPostcodeFilterTests.cs`

### Commit 2: Partition JSONL snapshots by source
**Status:** ✅ DONE (Ready to commit)

Files modified:
- ✅ `workInProgress/Storage/SnapshotWriter.cs` — added `string source` parameter (defaults to "default")
- ✅ `workInProgress/Storage/SnapshotLoader.cs` — uses `SearchOption.AllDirectories` to load from all source subdirectories
- ✅ `workInProgress.Tests/SnapshotWriterTests.cs` — updated 8 tests, added 2 new tests for source organization
- ✅ `workInProgress.Tests/SnapshotLoaderTests.cs` — updated 7 tests, added 1 new test for multi-source loading
- ✅ `workInProgress/Scraping/OhneMakler/OhneMaklerScraper.cs` — integrated source parameter in `RunAsync()`

Verification:
- ✅ `dotnet build` — 0 errors, 36 warnings
- ✅ `dotnet test` — 65/67 tests pass (2 pre-existing fixture failures unrelated to this commit)

### Commit 3: Add ScraperCoordinator to orchestrate multi-source scraping
**Status:** ✅ DONE (Ready to commit)

Files created:
- ✅ `workInProgress/Scraping/ScraperCoordinator.cs` — orchestrates all scrapers, manages persistence
- ✅ `workInProgress.Tests/ScraperCoordinatorTests.cs` — 10 comprehensive unit tests

Files modified:
- ✅ `workInProgress/Scraping/OhneMakler/OhneMaklerScraper.cs` — removed `RunAsync()`, `_repository`, `_snapshotWriter`, `IAsyncDisposable` (now pure IScraper)
- ✅ `workInProgress/Program.cs` — rewired to use ScraperCoordinator with dependency composition

Verification:
- ✅ `dotnet build` — 0 errors, 36 warnings
- ✅ `dotnet test` — 75/77 tests pass (2 pre-existing fixture failures unrelated to this commit)
- ✅ All 10 ScraperCoordinatorTests pass

## Phase 2: SQLite schema updates for multi-source support
**Status:** ✅ DONE (All changes implemented and tested)

### Key Architectural Decisions

1. **Entity Naming:** Rename single-source `OhneMaklerSnapshot` → source-neutral `FlatSnapshot` to match the `Flat` model naming and prepare for multi-source data.
2. **Table Naming:** Rename `OhneMaklerSnapshots` → `FlatsSnapshots`.
3. **Composite Key:** Use `(Source, ListingId)` as the primary key — scopes site-specific IDs per source, preventing collisions when multiple sources scrape (e.g., OhneMakler and ImmobilienScout24 both can have ID "123").
4. **Additional Unique Index:** Add `(Source, Url, Address)` unique constraint as a secondary safety net per `WohnungMonitor.md` spec.
5. **Repository Rename:** `OhneMaklerSnapshotRepository` → `FlatSnapshotRepository` (still implements `ISnapshotRepository`).
6. **Upsert Logic:** Update dedup logic to match on `(Source, ListingId)` pairs instead of `ListingId` alone.

### Files Created/Modified

Files created:
- ✅ `workInProgress/Models/FlatSnapshot.cs` (new entity, adds `Source` property with composite key)
- ✅ `workInProgress/Storage/FlatSnapshotRepository.cs` (new repository, updated upsert logic for composite key)
- ✅ `workInProgress.Tests/FlatSnapshotRepositoryTests.cs` (new test file, 8 tests including multi-source scenario)
- ✅ `workInProgress/Migrations/20261013120258_AddSourceToFlatsSnapshots.cs` (new migration, adds Source column and composite key)
- ✅ `workInProgress/Migrations/20261013120258_AddSourceToFlatsSnapshots.Designer.cs` (generated)

Files deleted/replaced:
- ✅ `workInProgress/Models/OhneMaklerSnapshot.cs` (removed, replaced by `FlatSnapshot.cs`)
- ✅ `workInProgress/Storage/OhneMaklerSnapshotRepository.cs` (removed, replaced by `FlatSnapshotRepository.cs`)
- ✅ `workInProgress.Tests/OhneMaklerSnapshotRepositoryTests.cs` (removed, replaced by `FlatSnapshotRepositoryTests.cs`)

Files modified:
- ✅ `workInProgress/Storage/ListingsDbContext.cs` — entity renamed to `FlatSnapshot`, table to `FlatsSnapshots`, composite key `(Source, ListingId)`, added `Source` property config, added unique index `(Source, Url, Address)`
- ✅ `workInProgress/Storage/ListingsDbContextFactory.cs` — updated to use `StoragePaths.GetDefaultDataDirectory()` for consistent connection string
- ✅ `workInProgress/Program.cs` — updated to use `new FlatSnapshotRepository()` instead of `OhneMaklerSnapshotRepository()`
- ✅ `workInProgress/Migrations/ListingsDbContextModelSnapshot.cs` (auto-regenerated by EF Core)

Verification:
- ✅ `dotnet build` — 0 errors
- ✅ `dotnet test` — 76/78 tests pass (2 pre-existing fixture failures in `OhneMaklerListingCardParserTests`, unrelated to Phase 2)
- ✅ All 8 `FlatSnapshotRepositoryTests` pass, including multi-source test asserting separate rows for same `ListingId` from different sources

## Testing & Verification

Each commit will be verified with:
1. `dotnet build` — solution compiles
2. `dotnet test` — all tests pass
3. `dotnet run -- scrape` — manual smoke test
4. `git diff` review — confirm scope alignment

## Future Phases (Post-Phase 1)

**Phase 2:** Database schema updates
- Add `Source` column to `Listings`/`Snapshots` tables
- Add unique constraints: `(Source, Url, Address)` on Listings
- Update `ISnapshotRepository`/`OhneMaklerSnapshotRepository` signatures

**Phase 3:** DI setup
- Introduce dependency injection container in `Program.cs`
- Register scrapers and coordinator with `services.AddScoped<...>()`

**Phase 4:** Add ImmobilienScout24 source
- Create `Scraping/ImmobilienScout24/` folder structure
- Implement `ImmobilienScout24Scraper` and parsers
- Add tests and fixtures under `workInProgress.Tests/ImmobilienScout24/`

**Phase 5:** Report generation
- Implement `report` command in `Program.cs`
- Group statistics per source
- Optional: `--source` CLI flag to filter by source
