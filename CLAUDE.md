# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project: Wohnung Monitor

A .NET 10 application (ASP.NET Core web API with an Angular frontend, plus a CLI) that scrapes apartment flats from multiple German sources (ohne-makler.net, immobilienscout24.de, immowelt.de) and tracks market changes over time (new flats, price changes, removals, aggregate statistics).

See `WohnungMonitor.md` for full architecture, data model, and API details.

## Common Commands

### Build
```bash
dotnet build
```

### Run scraper (fetch current flats, append to archive)
```bash
dotnet run -- scrape
dotnet run -- scrape --source Immowelt   # only these sources (comma-separated); the others keep their flats
```

### Run the web server (default; API for the Angular frontend)
```bash
dotnet run
```
There is no `report` command: statistics are served by the web API (see [BUILD_AND_RUN.md](BUILD_AND_RUN.md)).

## Architecture

**Multi-source design:**
- Each data source (OhneMakler, ImmobilienScout24, Immowelt) implements `IScraper` interface
- Source-specific parsing logic is encapsulated in `Scraping/{Source}/` subdirectories
- `ScraperCoordinator` orchestrates all scrapers and manages snapshots per source

**Two storage layers:**
1. **SQLite database** (`FlatMonitor/data/listings.db`, set in `FlatMonitor/appsettings.json`) — **canonical source of truth**. Persists all flats with source attribution. Used for all reads, aggregations, and statistics. Git-ignored.
2. **Snapshot files** (`data/snapshots/{source}/{utc-timestamp}.jsonl`) — optional backups for debugging only. JSON Lines format (one flat per line). Do NOT use snapshots as source for database rebuilds; snapshots may contain stale or unfiltered data.

**Run modes:**
- `scrape` — fetches current flats from all active sources, enriches with detail pages, yields unfiltered flats to `ScraperCoordinator`. `ScraperCoordinator` filters by allowed districts (normalizing names), batch-saves filtered flats to SQLite (every 20 flats to avoid memory bloat)
- web server (no command, the default) — REST API read by the Angular frontend: current offers, aggregations by district and rooms and their history, filter presets; `POST /api/scrape` queues a scrape job that runs the same pipeline as `scrape`
- `scrape --source <names>` — the same, for the named sources only (`Scrapers:Sources` does it for the web server)
- `explore-is24` — opens ImmobilienScout24 in the browser and saves the listings page HTML for analysis
- `explore-immowelt [--headless | --cdp]` — saves two Immowelt results pages and an expose to `data/explore/Immowelt/`

## Key Design Notes

- **No dates on the site itself** — database timestamps (scrapedAtUtc) are added at scrape time to track when each flat was discovered
- **Database is canonical** — SQLite is the sole source of truth; never rebuild the database from snapshots
- **Snapshots are backups only** — created for debugging purposes; do not use snapshots to reconstruct or validate database state
- **District filtering** — All scraped flats are filtered to match `KarlsruheDistricts.All` (27 Karlsruhe districts only). See [FILTERING.md](FILTERING.md) for strategy and examples
- **HTML coupling risk** — scraper is tightly bound to current site markup; mitigated by parser unit tests against saved fixtures, but real failures surface at scrape time
- **Immowelt needs the user's Chrome** — DataDome blocks `HttpClient` and Playwright's own browser, so the Immowelt
  scraper attaches to the installed Chrome over CDP (`127.0.0.1:9222`), starting it if needed (own profile
  `FlatMonitorChrome`). Never add automation/headless flags or evasion. Pages are read from their JSON data scripts,
  3 s apart (5 s passed a full run; 3 s is being tried). See [FlatMonitor/immowelt.md](FlatMonitor/immowelt.md) and [BUILD_AND_RUN.md](BUILD_AND_RUN.md#immowelt-and-chrome)
- **Keep-awake during a scrape** — `ScrapeRunService` holds a Windows power request (`KeepSystemAwake`) for the
  whole run: Modern Standby cut the Chrome connection mid-run. A no-op outside Windows
- **Cross-source duplicates** — many flats are listed on several sources (188 of 246 Immowelt flats also on IS24).
  All sources are scraped and saved as they are; `FlatAggregationCalculator` counts a flat once when other sources
  have one with the same district, price, living area and normalized title. Within one source such flats stay
  different (identical units in a new building)
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
- Tests that call real websites are `[Explicit]` + `[Category("Live")]`: skipped by `dotnet test` and CI, run with `dotnet test --filter "TestCategory=Live"`
- CI: GitLab (`.gitlab-ci.yml`) and GitHub (`.github/workflows/ci.yml`) run the same scripts in `scripts/ci/` on every push; see [BUILD_AND_RUN.md](BUILD_AND_RUN.md#ci-pipelines)

## Data Flow

1. **Scrape**: 
   - For each `IScraper` (OhneMakler, ImmobilienScout24, etc.):
     - Fetch all pages → Parse listing cards → Enrich with detail pages → Yield unfiltered flats one by one (`IAsyncEnumerable<Flat>`)
     - `ScraperCoordinator` takes them in batches of 20, filters by `KarlsruheDistricts.All` and saves each batch to the
       staging table `FlatsSnapshots_Staging`
     - When a source finishes with flats, its rows in `FlatsSnapshots` are replaced in one transaction; a source that
       fails or finds nothing keeps its rows of the previous run. `FlatsSnapshots` is never cleared
     - District names normalized during filtering (see [FILTERING.md](FILTERING.md))
     - Each saved batch is appended to the source's snapshot file (debugging only)
     - Result: a `ScrapeSummary` (flats saved per source, errors of failed sources)
   
2. **Aggregate**: after the sources are scraped, the aggregations (€/m² by district and rooms) are computed and stored in `FlatAggregations`

3. **API**: the web API reads `FlatsSnapshots` and `FlatAggregations` (canonical source) and serves the Angular frontend
