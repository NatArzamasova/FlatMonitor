# Story 1a: Write Snapshots to SQLite — Implementation Plan

See `docs/STORY_1A_SNAPSHOT_TO_DB.md` for the original spec. This plan adapts it to the
actual shape of `Listing`/`Details` and fixes two bugs in the spec's example code:
`Address` is a field on `Listing` directly (not nested under `Details`), and the upsert
transaction must be assigned to the command (`cmd.Transaction = transaction`) or
`Microsoft.Data.Sqlite` throws.

## Implementation Phases

- **Phase 1: SQLite Schema & Repository** — ⬜ In progress (this session)
  - Add `Microsoft.Data.Sqlite` package:
    - `Directory.Packages.props`: `<PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.12" />`
    - `workInProgress/workInProgress.csproj`: `<PackageReference Include="Microsoft.Data.Sqlite" />`
    - `workInProgress.Tests/workInProgress.Tests.csproj`: `<PackageReference Include="Microsoft.Data.Sqlite" />` (tests need direct `SqliteConnection` access to verify schema/rows independently of the repository under test)
  - `Storage/ListingsRepository.cs` (`IAsyncDisposable`), following `SnapshotWriter`'s style:
    - Constructor `(string? baseDirectory = null)`, defaults to `"data"`, calls `Directory.CreateDirectory(baseDirectory)` before opening the connection (spec's example omits this and fails on a clean checkout)
    - `InitializeAsync(CancellationToken ct = default)` — creates `Listings` table (all fields from `Listing`/`Details` flattened, `ListingId` PK) and indexes on `ScrapedAtUtc`, `PostalCode`, `District`
    - `UpsertListingsAsync(List<Listing> listings, CancellationToken ct = default)` — single transaction, `INSERT ... ON CONFLICT(ListingId) DO UPDATE` per listing; `cmd.Transaction = transaction` set explicitly; `Address` mapped from `listing.Address` (not `listing.Details?.Address`); all other detail columns from `listing.Details?.<Field>` with `DBNull.Value` fallback; `CreatedAtUtc` set once, `UpdatedAtUtc` refreshed every upsert
    - `DisposeAsync()` disposes the connection
  - Tests: `workInProgress.Tests/ListingsRepositoryTests.cs` (NUnit + FluentAssertions, temp-dir setup/teardown matching `SnapshotWriterTests.cs`):
    - `InitializeAsync` creates the table and all 3 indexes
    - Upsert of a new listing persists all fields, including `Address` from `listing.Address`
    - Upsert with `Details = null` stores nulls for detail columns
    - Upserting the same `ListingId` twice updates the row in place (count stays 1, `CreatedAtUtc` unchanged, `UpdatedAtUtc` changes, changed field reflects new value)
    - Upserting multiple listings inserts all rows
  - Not in scope for Phase 1: wiring into `ListingScraper`/`scrape` command, and `SnapshotLoader` (see Phase 2/3 below)

- **Phase 2: Load Snapshots into DB** — ⬜ Not started
  - `Storage/SnapshotLoader.cs` — `LoadAllSnapshots(string? baseDirectory = null)`, reads `data/snapshots/*.jsonl` in chronological filename order, deserializes with `JsonOptions.Snapshot`, skips malformed lines with a warning
  - Tests: `workInProgress.Tests/SnapshotLoaderTests.cs`

- **Phase 3: Integrate into Scraper** — ⬜ Not started
  - `ListingScraper.RunAsync()` gains optional `(string? baseDirectory = null, CancellationToken ct = default)` params; after writing the snapshot, constructs `ListingsRepository` inline, calls `InitializeAsync`/`UpsertListingsAsync`, wrapped in try/catch (DB failure logs a warning but does not fail `scrape` — snapshot is already the source of truth)
  - No `Program.cs` changes needed (new params are optional)

- **Phase 4: Additional Tests** — ⬜ Not started (covered incrementally with Phases 1–3 above rather than as a separate pass)

## Out of Scope

- `report` command / aggregation queries (Story 2)
- "New" vs "Removed" vs "Price Changed" logic (Story 2)
