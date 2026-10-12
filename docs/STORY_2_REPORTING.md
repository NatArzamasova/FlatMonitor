# Story 2: Reporting — `dotnet run -- report`

## Goal
Implement the report generator that ingests all historical snapshots into SQLite, performs aggregations, and outputs a comparison report (new listings, price changes, removed listings, statistics).

## Acceptance Criteria
- [ ] `dotnet run -- report` executes without error
- [ ] SQLite database (`data/listings.db`) is created on first run (or opened if exists)
- [ ] All snapshot JSONL files from `data/snapshots/` are loaded and replayed into the database
- [ ] Listings table deduplicates by (Url, Address); tracks FirstSeenAtUtc, LastSeenAtUtc, IsActive, OriginalPriceEur, CurrentPriceEur
- [ ] Snapshots table records metadata for each scrape run (ScrapedAtUtc, ListingCount)
- [ ] Statistics table computes daily aggregates (active count, new/removed/price-changed counts, avg €/m², avg days-on-market)
- [ ] Report output includes (comparing today vs yesterday):
  - New listings with price, rooms, area
  - Price changes with € and % delta
  - Removed listings with days-on-market
  - Aggregate stats: total active, avg €/m², avg days-on-market, 7-day & 30-day trends
- [ ] Report is printed to console in human-readable format

## Implementation Tasks

### Phase 1: Database Schema & Initialization
- [ ] Create `DatabaseInitializer` to:
  - Define SQLite schema: Snapshots, Listings, Statistics tables (see ohneMaklerMonitor.md for schema)
  - Apply schema on first run (`CREATE TABLE IF NOT EXISTS`)
  - Verify tables exist and have expected columns
- [ ] Configure SQLite connection string (local file at `data/listings.db`)

### Phase 2: Snapshot Ingestion & Replay
- [ ] Create `SnapshotLoader` to:
  - Enumerate all JSONL files from `data/snapshots/` in chronological order
  - Parse each line as a Listing object
  - Insert/upsert into Listings table (by Url + Address unique key)
  - Update FirstSeenAtUtc, LastSeenAtUtc, IsActive flags
  - Record scrape metadata in Snapshots table
- [ ] Handle incremental replays (only process new snapshots since last report run, or full replay each time?)

### Phase 3: Aggregation Queries
- [ ] Create `ReportService` with methods for:
  - Get today's active listings (count, avg price, avg area)
  - Get yesterday's snapshot (for comparison)
  - Find new listings (in today's snapshot, not in yesterday's)
  - Find removed listings (in yesterday's snapshot, not in today's) with days-on-market
  - Find price changes (listings where current price ≠ original price)
  - Compute daily statistics (populate Statistics table)
  - Compute rolling 7-day & 30-day trends

### Phase 4: Report Formatter & Output
- [ ] Create `ReportFormatter` to:
  - Format new listings section (title, price, rooms, area, address)
  - Format price changes section (title, old → new price, € and % delta)
  - Format removed listings section (title, days on market, original price)
  - Format aggregate stats section (total active, avg €/m², avg days-on-market)
  - Format trend section (7-day & 30-day rolling avg price, active count, new/removed counts)
- [ ] Print report to console

### Phase 5: Command Entry Point
- [ ] Update `Program.cs` to:
  - Parse command-line arguments (detect `report` command)
  - Wire up dependency injection (DatabaseInitializer, SnapshotLoader, ReportService, ReportFormatter)
  - Execute report generation and output
  - Handle DB connection lifecycle (open, close cleanly)

## Open Questions (Refine During Implementation)
- [ ] Should report always do a full replay of all snapshots, or track "last ingested snapshot" for incremental processing?
- [ ] What if there's no yesterday's snapshot (first run, or data gap)? Should report compare to "X days ago" or handle gracefully?
- [ ] How to define "today" and "yesterday" for report comparisons — by calendar date or by snapshot timestamp?
- [ ] Should rolling trends be in the Statistics table, or computed on-demand by ReportService?
- [ ] Output format — just console table, or also JSON? HTML? File export?

## Notes
- SQLite database is **derived and gitignored** — can be deleted and rebuilt from snapshots at any time
- Snapshot replay is deterministic — same snapshots always produce same DB state
- Report compares "today vs yesterday" based on snapshot timestamps, not calendar dates
