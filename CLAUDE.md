# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project: OhneMakler Karlsruhe Monitor

A .NET 10 console application that scrapes apartment listings from ohne-makler.net and tracks market changes over time (new listings, price changes, removals, aggregate statistics).

See `ohneMaklerMonitor.md` for full architecture, data model, and API details.

## Common Commands

### Build
```bash
dotnet build
```

### Run scraper (fetch current listings, append to archive)
```bash
dotnet run -- scrape
```

### Generate report (statistics comparing today vs yesterday)
```bash
dotnet run -- report
```

## Architecture

**Two storage layers:**
1. **Snapshot files** (`data/snapshots/{utc-timestamp}.jsonl`) — canonical, git-committed JSON Lines format, one listing per line. Append-only, safe for CI to commit back without merge conflicts.
2. **SQLite cache** (`data/listings.db`) — derived from replaying all snapshots, git-ignored. Used only by `report` for fast aggregates; can be deleted and rebuilt from snapshots.

**Two run modes:**
- `scrape` — fetches current site listings, deserializes into `Listing` objects, appends to today's snapshot file
- `report` — loads all snapshots into SQLite on demand, queries for statistics (new/removed/price-changed counts, aggregate €/m², days-on-market, rolling trends)

## Key Design Notes

- **No dates on the site itself** — scraper relies entirely on snapshot timestamps (UTC) to build history
- **Idempotency for CI** — snapshot files are append-only, never rewritten; a scheduled job can safely commit new snapshots back to the repo
- **HTML coupling risk** — scraper is tightly bound to current site markup; mitigated by parser unit tests against saved fixtures, but real failures surface at scrape time
- **"Removed" ambiguity** — removal is inferred from absence in latest snapshot (could be sold, delisted, or scrape miss); no ground truth from the site

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
public void ShouldParseListingFromHtml()
{
    // Arrange
    var html = File.ReadAllText("fixtures/sample.html");
    var parser = new HtmlParser();
    
    // Act
    var listings = parser.Parse(html);
    
    // Assert
    listings.Should().HaveCount(3);
    listings.First().Price.Should().Be(1500);
}
```

Run tests with:
```bash
dotnet test
```

- Parser logic is covered by unit tests against saved HTML fixtures

## Data Flow

1. **Scrape**: Site → HtmlParser (HTMLAgilityPack or similar) → `Listing` objects → Snapshot JSONL file
2. **Report**: All Snapshot JSONL files → SQLite (upsert) → Aggregation queries → Report output
