# Wohnung Monitor

## Purpose

Track apartment-for-sale listings from multiple German sources (ohne-makler.net, immobilienscout24.de, etc.) 
for Karlsruhe over time. These sites have no archive of their own — no posted/updated dates, and removed 
listings just vanish. This tool builds our own history by scraping repeatedly from all sources and 
recording what we see, timestamped by scrape time and attributed by source.

## Supported Sources

### OhneMakler (ohne-makler.net)
Checked live on 2026-10-09 against `/immobilien/wohnung-kaufen/baden-wurttemberg/karlsruhe/`:
- Search results page: paginated list of active listings, each linking to `/immobilie/{id}/`.
- Per-listing: price, title, postal code + district, rooms, m², thumbnail.
- Detail page (e.g. `/immobilie/502814/`): price, rooms, living area, property type, address, 
  build year, floor, condition, heating, flooring, service charge, energy certificate type, 
  primary energy source, energy efficiency class, energy consumption, listing status, current use, availability.
- **No date field anywhere** — not on search results, not on detail pages.

### ImmobilienScout24 (immobilienscout24.de)
TBD — architecture supports pluggable sources; add parsers as needed.

### Future sources
The architecture is designed to accommodate additional sources (Makler.de, Immowelt, etc.) 
by implementing the `IScraper` interface.

## Architecture

### Run modes
Two run modes, one executable:

- `dotnet run -- scrape` — fetch current listings from all sources, append to the archive.
- `dotnet run -- report`  — read the archive, print a statistics report.

### Storage layers

1. **Snapshot files (canonical, git-committed)** — each `scrape` run writes
   one JSON Lines file per source to `data/snapshots/{source}/{utc-timestamp}.jsonl`, 
   one line per listing seen. Append-only, plain text, diffs cleanly, never
   rewritten — safe for a scheduled CI job to commit back to the repo
   without merge conflicts.
2. **SQLite cache (derived, gitignored)** — `data/listings.db`, rebuilt on
   demand by replaying all snapshot files in order. Used only by `report`
   for fast queries/aggregates. Never a source of truth — if it's deleted,
   it's just rebuilt from the snapshot history.

### Multi-source architecture

**Key principle:** Each source is independent, pluggable, and encapsulates its own 
parsing logic and quirks. The framework provides:

1. **IScraper interface** — contract for all sources:
   ```csharp
   public interface IScraper
   {
       string Source { get; }  // "OhneMakler", "ImmobilienScout24", etc.
       Task<IEnumerable<Flat>> ScrapeAsync();
   }
   ```

2. **Source-specific parsers** — live in `Scraping/{SourceName}/` folders:
   - `Scraping/OhneMakler/` — OhneMaklerScraper, OhneMaklerListingCardParser, OhneMaklerDetailPageParser
   - `Scraping/ImmobilienScout24/` — ImmobilienScout24Scraper, IS24ListingCardParser, IS24DetailPageParser
   - `Scraping/{NewSource}/` — pluggable for future sources

3. **Snapshot format changes** — add `Source` field:
   ```json
   {
     "Source": "OhneMakler",
     "ListingId": "502814",
     ...rest unchanged...
     "ScrapedAtUtc": "2026-10-09T14:30:00Z"
   }
   ```

4. **ScraperCoordinator** — orchestrates multiple sources:
   ```csharp
   public class ScraperCoordinator
   {
       private readonly IEnumerable<IScraper> scrapers;
       private readonly ISnapshotRepository snapshotRepository;
       
       public async Task ScrapeAllSourcesAsync()
       {
           foreach (var scraper in scrapers)
           {
               var listings = await scraper.ScrapeAsync();
               await snapshotRepository.AppendSnapshotAsync(scraper.Source, listings);
           }
       }
   }
   ```

5. **Database schema update** — add source attribution to Listings:
   ```sql
   ALTER TABLE Listings ADD Source TEXT NOT NULL DEFAULT 'OhneMakler';
   -- Later: add composite unique constraint on (Source, Url, Address)
   -- to allow same listing URL across different sources
   ```

## Data model

### JSONL Snapshot format (canonical, git-committed)

Each `data/snapshots/{source}/{utc-timestamp}.jsonl` contains one JSON object per line,
one per listing seen in that scrape run. The `Source` field identifies which data source
the listing came from:

```json
{
  "Source": "OhneMakler",
  "ListingId": "502814",
  "Url": "/immobilie/502814/",
  "Title": "Altbauperle Weststadt - Zwei Appartements in einer Wohnung",
  "Address": "Weltzienstraße 21",
  "PostalCode": "76135",
  "District": "Weststadt",
  "PriceEur": 270000,
  "RoomsCount": 3,
  "LivingAreaSqm": 82,
  "Details": {
    "PropertyType": "Dachgeschosswohnung",
    "BuildYear": 1904,
    "Floor": "4. OG",
    "Condition": "gepflegt",
    "Heating": "Ofenheizung",
    "Flooring": "Laminat",
    "ServiceChargeEur": 405,
    "EnergyCertificateType": "gesetzlich nicht erforderlich",
    "PrimaryEnergySource": "Gas",
    "EnergyEfficiencyClass": "C",
    "EnergyConsumption": "120 kWh/(m²·a)",
    "ListingStatus": "Privatangebot",
    "CurrentUse": "Vermietet",
    "Availability": "Nach Vereinbarung"
  },
  "ScrapedAtUtc": "2026-10-09T14:30:00Z"
}
```

**Note:** Snapshot filenames include the source for clarity and organization: 
`data/snapshots/OhneMakler/2026-10-09T14-30-00Z.jsonl` and 
`data/snapshots/ImmobilienScout24/2026-10-09T14-30-00Z.jsonl`.

### SQLite schema (derived, gitignored, rebuilt from snapshots)

**`Snapshots` table** — one row per scrape run per source:
- `SnapshotId` (PK, auto-increment)
- `Source` (TEXT, NOT NULL) — "OhneMakler", "ImmobilienScout24", etc.
- `ScrapedAtUtc` (DATETIME, indexed)
- `ListingCount` (INT) — how many listings in this run
- Unique constraint: (Source, ScrapedAtUtc)

**`Listings` table** — deduplicated by (Source, Url, Address); one row per unique listing ever seen:
- `ListingId` (PK, auto-increment)
- `Source` (TEXT, NOT NULL) — identifies which scraper found this listing
- `Url` (TEXT, NOT NULL) — may be relative or absolute depending on source
- `Address` (TEXT, NOT NULL)
- `Title` (TEXT)
- `PostalCode`, `District` (TEXT)
- `OriginalPriceEur` (DECIMAL) — first price seen from this source
- `CurrentPriceEur` (DECIMAL) — latest price seen from this source
- `RoomsCount`, `LivingAreaSqm` (INT/DECIMAL)
- `Details` (JSON) — nested {PropertyType, BuildYear, Floor, Condition, Heating, ...}
- `FirstSeenAtUtc` (DATETIME) — earliest snapshot containing this listing
- `LastSeenAtUtc` (DATETIME) — latest snapshot containing this listing
- `IsActive` (BOOLEAN) — present in the most recent snapshot from this source
- Unique constraint: (Source, Url, Address)

**`Statistics` table** — one row per calendar day per source:
- `StatisticId` (PK, auto-increment)
- `Source` (TEXT, NOT NULL) — which source these stats are for
- `SnapshotDateUtc` (DATE, indexed)
- `TotalActiveListings` (INT)
- `NewListingsCount` (INT)
- `RemovedListingsCount` (INT)
- `PriceChangedCount` (INT)
- `AvgPricePerSqmEur` (DECIMAL)
- `AvgDaysOnMarketRemoved` (DECIMAL) — for listings removed that day
- Unique constraint: (Source, SnapshotDateUtc)

## Report content

Each `report` run shows aggregated metrics across all sources (or per-source if requested):

- **New listings**: listings in today's snapshot not in yesterday's, grouped by source.
- **Price changes**: listings where CurrentPriceEur differs from OriginalPriceEur (€ and % delta), grouped by source.
- **Removed listings**: in yesterday's snapshot but not today's, with days-on-market (LastSeenAtUtc − FirstSeenAtUtc), grouped by source.
- **Aggregate statistics**: pulled from the Statistics table for today's date:
  - Total active listings per source.
  - Average €/m² per source.
  - Average days-on-market for removed listings (per source).
  - 7-day and 30-day rolling trends (if history exists) per source.
  - Cross-source comparison (e.g., total unique listings across all sources, overlap detection if same property listed on multiple sites).
  
**Note:** The architecture allows future enhancement to detect the same property listed on multiple sources 
(via fuzzy address matching or other heuristics), which would enable market-wide analytics.

## Running on a schedule

Origin is GitLab (`code.andrena.de`), so scheduling uses **GitLab CI
scheduled pipelines**, not GitHub Actions:

- A scheduled pipeline runs `dotnet run -- scrape`, then commits + pushes
  the new `data/snapshots/*.jsonl` file back to the repo.
- Requires a push-capable token (project access token or deploy token)
  configured in GitLab project settings — a manual one-time setup step.

## Refactoring for Multi-Source Support

To evolve from single-source to multi-source architecture, apply these changes in order:

### 1. Extract IScraper interface
Create `Scraping/IScraper.cs`:
```csharp
public interface IScraper
{
    string Source { get; }
    Task<IEnumerable<Listing>> ScrapeAsync();
}
```

### 2. Rename and refactor ListingScraper
- Rename `Scraping/ListingScraper.cs` → `Scraping/OhneMakler/OhneMaklerScraper.cs`
- Make it implement `IScraper`
- Move OhneMakler-specific parsers into `Scraping/OhneMakler/` subdirectory:
  - `ListingCardParser` → `OhneMaklerListingCardParser`
  - `DetailPageParser` → `OhneMaklerDetailPageParser`
  - `AddressHelper` → `OhneMaklerAddressHelper`
  - `Selectors` → `OhneMaklerSelectors`
- Remove domain-specific assumptions from generic helpers

### 3. Update OhneMaklerSnapshot model
Add `Source` field:
```csharp
public class OhneMaklerSnapshot
{
    public string Source { get; set; } = "OhneMakler";
    public IEnumerable<Listing> Listings { get; set; }
    public DateTime ScrapedAtUtc { get; set; }
}
```

### 4. Refactor ISnapshotRepository
Update signature to be source-aware:
```csharp
public interface ISnapshotRepository
{
    Task AppendSnapshotAsync(string source, IEnumerable<Listing> listings);
    Task<IEnumerable<Listing>> LoadSnapshotsAsync(string source, DateTime? since = null);
    Task<IEnumerable<string>> GetSourcesAsync();
}
```

### 5. Create ScraperCoordinator
New class `Scraping/ScraperCoordinator.cs`:
```csharp
public class ScraperCoordinator
{
    private readonly IEnumerable<IScraper> scrapers;
    private readonly ISnapshotRepository repository;
    
    public async Task ScrapeAllSourcesAsync()
    {
        foreach (var scraper in scrapers)
        {
            var listings = await scraper.ScrapeAsync();
            await repository.AppendSnapshotAsync(scraper.Source, listings);
        }
    }
}
```

### 6. Update database schema
Add `Source` field to `Listings` and `Snapshots` tables:
```sql
ALTER TABLE Listings ADD Source TEXT NOT NULL DEFAULT 'OhneMakler';
ALTER TABLE Snapshots ADD Source TEXT NOT NULL DEFAULT 'OhneMakler';

-- Later: enforce unique constraint per source
ALTER TABLE Listings ADD CONSTRAINT UQ_Listings_Source_Url_Address 
  UNIQUE(Source, Url, Address);
```

### 7. Update Program.cs
Register multiple scrapers and the coordinator:
```csharp
services.AddScoped<OhneMaklerScraper>();
// services.AddScoped<ImmobilienScout24Scraper>();
// services.AddScoped<NewSourceScraper>();

services.AddScoped<ScraperCoordinator>();
services.AddScoped<ISnapshotRepository, OhneMaklerSnapshotRepository>();
```

### 8. Add new source: ImmobilienScout24
Create directory `Scraping/ImmobilienScout24/` with:
- `ImmobilienScout24Scraper` implements `IScraper`
- `IS24ListingCardParser`
- `IS24DetailPageParser`
- `IS24Selectors`
- Test fixtures in `workInProgress.Tests/Fixtures/ImmobilienScout24/`

### 9. Update report generation
Modify reporting logic to:
- Group statistics by source
- Show per-source trends
- Optionally merge cross-source comparisons (same property on multiple sites)
- CLI flag: `--source OhneMakler` to filter report by source

## Open questions / risks

- **Scraper coupling:** Each scraper is coupled to its source's current HTML structure; 
  will break silently if the site changes markup. Mitigated by parser unit tests against 
  saved HTML fixtures, but real breakage surfaces at scrape time. Each source needs robust test coverage.
- **Duplicate listings:** Same property may appear under different URLs or addresses across sources.
  Future enhancement: add fuzzy matching to detect and link the same property on different sites.
- **"Removed" ambiguity:** Removal is inferred from absence in latest snapshot — could be sold,
  delisted, or a scrape miss. No ground truth from the site alone.
- **Scrape frequency:** Determines resolution of all time-based stats (days-on-market, price-change timing) 
  — daily is the assumed cadence unless stated otherwise. Multi-source scraping increases total runtime.
