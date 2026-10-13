# Story 1: Local Scrape — `dotnet run -- scrape`

## Goal
Implement the scraper that fetches current apartment listings from ohne-makler.net and appends them to a timestamped JSONL snapshot file.

## Acceptance Criteria
- [ ] `dotnet run -- scrape` executes without error
- [ ] Scraper fetches listings from ohne-makler.net Karlsruhe apartment listings page
- [ ] Each listing is parsed into a `Listing` object (title, price, rooms, area, address, postal code, district, etc.)
- [ ] Detailed listing info is fetched from individual detail pages and parsed into `Details` object
- [ ] All listings are serialized to `data/snapshots/{utc-timestamp}.jsonl` (one JSON object per line)
- [ ] Format matches the schema in `ohneMaklerMonitor.md` (ListingId, Url, Title, Address, PriceEur, RoomsCount, LivingAreaSqm, Details, ScrapedAtUtc)
- [ ] Snapshot file is created fresh each run (new UTC timestamp); previous snapshots are never modified

## Implementation Tasks

### Phase 1: Models & Serialization
- [ ] Create `Listing` model class with properties: ListingId, Url, Title, Address, PostalCode, District, PriceEur, RoomsCount, LivingAreaSqm, Details, ScrapedAtUtc
- [ ] Create `Details` model class with nested properties: PropertyType, BuildYear, Floor, Condition, Heating, Flooring, ServiceChargeEur, EnergyCertificateType, PrimaryEnergySource, EnergyEfficiencyClass, EnergyConsumption, ListingStatus, CurrentUse, Availability
- [ ] Configure JSON serialization (System.Text.Json or Newtonsoft.Json) to match schema exactly

### Phase 2: HTTP Client & HTML Parser
- [ ] Set up HttpClient for fetching site pages
- [ ] Create HTML parser (HtmlAgilityPack or AngleSharp) to extract:
  - Search results page: listing cards (title, price, postal code, district, rooms, area, listing URL)
  - Detail page: extended info (property type, build year, floor, condition, heating, flooring, service charge, energy cert, energy efficiency class, energy consumption, listing status, current use, availability)
- [ ] Handle pagination (or confirm single page is sufficient for initial MVP)
- [ ] Implement error handling (retry logic? fail-fast? logging?)

### Phase 3: Snapshot Repository
- [ ] Create `SnapshotRepository` class to:
  - Generate timestamped file path: `data/snapshots/{utc-timestamp}.jsonl`
  - Write `Listing` objects as JSON Lines (one per line, newline-delimited)
  - Ensure `data/` directory exists (create if missing)

### Phase 4: Command Entry Point
- [ ] Update `Program.cs` to:
  - Parse command-line arguments (detect `scrape` command)
  - Wire up dependency injection (HttpClient, HtmlParser, SnapshotRepository)
  - Execute scraper, write snapshot, exit cleanly
  - Log progress/errors to console

## Open Questions (Refine During Implementation)
- [ ] Does the site require pagination? If yes, how many pages / listings per page?
- [ ] Are there rate limits or User-Agent requirements?
- [ ] Should failed listings (parse errors on detail pages) be skipped or cause the entire scrape to fail?
- [ ] How to handle the "no dates on the site" constraint — use scrape timestamp only?
- [ ] Any fields that might be missing/null on some listings? How to handle?

## Notes
- Use saved HTML fixtures from the site for parser unit tests (to be collected)
- Keep scraper HTML-structure-agnostic where possible (selectors in config? constants?)
- Snapshot file format is append-only and git-committable — each run is independent
