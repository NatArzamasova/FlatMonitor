# AGENTS.md

This file provides guidance to Codex (claude.ai/code) when working with code in this repository.

## Project: Wohnung Monitor

A .NET 10 console application that scrapes apartment flats from multiple German sources (ohne-makler.net, immobilienscout24.de, etc.) and tracks market changes over time (new flats, price changes, removals, aggregate statistics).

See `WohnungMonitor.md` for full architecture, data model, and API details.

## Common Commands

### Build
```bash
dotnet build
```

### Run scraper (fetch current flats, append to archive)
```bash
dotnet run -- scrape
```

### Generate report (statistics comparing today vs yesterday)
```bash
dotnet run -- report
```

## Architecture

**Multi-source design:**
- Each data source (OhneMakler, ImmobilienScout24, etc.) implements `IScraper` interface
- Source-specific parsing logic is encapsulated in `Scraping/{Source}/` subdirectories
- `ScraperCoordinator` orchestrates all scrapers and manages snapshots per source

**Two storage layers:**
1. **SQLite database** (`data/flats.db`) — **canonical source of truth**. Persists all flats with source attribution. Used for all reads, aggregations, and statistics. Git-ignored.
2. **Snapshot files** (`data/snapshots/{source}/{utc-timestamp}.jsonl`) — optional backups for debugging only. JSON Lines format (one flat per line). Do NOT use snapshots as source for database rebuilds; snapshots may contain stale or unfiltered data.

**Two run modes:**
- `scrape` — fetches current flats from all active sources, enriches with detail pages, returns unfiltered flats to `ScraperCoordinator`. `ScraperCoordinator` filters by allowed districts (normalizing names), batch-saves filtered flats to SQLite (every 20 flats to avoid memory bloat)
- `report` — queries SQLite database directly for aggregations across sources, reports statistics (new/removed/price-changed counts, aggregate €/m², days-on-market, rolling trends, per-source breakdowns)

## Key Design Notes

- **No dates on the site itself** — database timestamps (scrapedAtUtc) are added at scrape time to track when each flat was discovered
- **Database is canonical** — SQLite is the sole source of truth; never rebuild the database from snapshots
- **Snapshots are backups only** — created for debugging purposes; do not use snapshots to reconstruct or validate database state
- **District filtering** — All scraped flats are filtered to match `AllowedDistricts` (26 Karlsruhe districts only). See [FILTERING.md](FILTERING.md) for strategy and examples
- **HTML coupling risk** — scraper is tightly bound to current site markup; mitigated by parser unit tests against saved fixtures, but real failures surface at scrape time
- **"Removed" ambiguity** — removal is inferred from absence in latest scrape (could be sold, delisted, or scrape miss); no ground truth from the site

## Parallel: MCP Servers

The `Mcp/` folder contains experimental Python-based MCP (Model Context Protocol) servers:
- `mcp_server.py` / `mcp_weather_server.py` — server implementations
- `test_api.py`, `test_dotenv.py`, `list_models.py` — test utilities
- `run_mcp_server.sh` — shell script to launch servers

These are exploratory; not integrated into the main .NET build.

## Commit Messages

Commit messages must follow the format: `type(scope): message`

**Types:** `feat`, `fix`, `refactor`, `docs`, `test`, `chore`

**Scopes:** `scrape`, `parser`, `storage`, `db`, `report`, `tests`, etc.

**Examples:**
- `feat(scrape): add pagination logic`
- `fix(parser): handle missing price fields`
- `test(storage): add snapshot upsert tests`
- `docs(story1): update implementation plan`

### Setup Git Hooks

After cloning the repository, run the hook setup script to install the commit-msg validator:

```bash
bash scripts/setup-hooks.sh
```

This installs a `commit-msg` hook that validates all commit messages match the required format. The hook will reject commits that don't follow `type(scope): message`.

## Testing

For C# code, use the following testing stack:
- **NUnit** — test framework for organizing and running tests
- **Moq** — mocking library for isolating dependencies
- **FluentAssertions** — fluent assertion syntax for readable test expectations

Example test structure:
```csharp
[Test]
public void ShouldParseFlatFromHtml()
{
    // Arrange
    var html = File.ReadAllText("fixtures/sample.html");
    var parser = new HtmlParser();
    
    // Act
    var flats = parser.Parse(html);
    
    // Assert
    flats.Should().HaveCount(3);
    flats.First().Price.Should().Be(1500);
}
```

Run tests with:
```bash
dotnet test
```

- Parser logic is covered by unit tests against saved HTML fixtures

## Data Flow

1. **Scrape**: 
   - For each `IScraper` (OhneMakler, ImmobilienScout24, etc.):
     - Fetch all pages → Parse listing cards → Enrich with detail pages → Return unfiltered flats
     - `ScraperCoordinator` sets up batch-save callback on scraper (if FlatScraper)
     - Callback filters flats by `AllowedDistricts` and saves to SQLite every 20 items
     - Fallback filter (for non-FlatScraper) ensures all flats are filtered before insertion
     - District names normalized during filtering (see [FILTERING.md](FILTERING.md))
     - Write snapshot files (debugging only, after all filtering/saving complete)
   
2. **Report**: 
   - Query SQLite FlatsSnapshots table directly (canonical source) → Aggregation queries per source → Cross-source comparison → Report output
