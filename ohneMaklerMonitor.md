# OhneMakler Karlsruhe Monitor

## Purpose

Track apartment-for-sale listings on ohne-makler.net for Karlsruhe over time.
The site has no archive of its own — no posted/updated dates, and removed
listings just vanish. This tool builds our own history by scraping
repeatedly and recording what we see, timestamped by scrape time.

## What the site gives us

Checked live on 2026-10-09 against
`/immobilien/wohnung-kaufen/baden-wurttemberg/karlsruhe/`:

- Search results page: paginated list of active listings, each linking to
  `/immobilie/{id}/`. Per-listing: price, title, postal code + district,
  rooms, m², thumbnail.
- Detail page (e.g. `/immobilie/502814/`): price, rooms, living area,
  property type, address, build year, floor, condition, heating, flooring,
  service charge, energy certificate type, primary energy source, energy
  efficiency class, energy consumption, listing status (private/commercial), current use, availability.
- **No date field anywhere** — not on search results, not on detail pages.

## Architecture

Two run modes, one executable:

- `dotnet run -- scrape` — fetch current listings, append to the archive.
- `dotnet run -- report`  — read the archive, print a statistics report.

Two storage layers:

1. **Snapshot files (canonical, git-committed)** — each `scrape` run writes
   one JSON Lines file to `data/snapshots/{utc-timestamp}.jsonl`, one line
   per listing seen. Append-only, plain text, diffs cleanly, never
   rewritten — safe for a scheduled CI job to commit back to the repo
   without merge conflicts.
2. **SQLite cache (derived, gitignored)** — `data/listings.db`, rebuilt on
   demand by replaying all snapshot files in order. Used only by `report`
   for fast queries/aggregates. Never a source of truth — if it's deleted,
   it's just rebuilt from the snapshot history.

## Data model

### JSONL Snapshot format (canonical, git-committed)

Each `data/snapshots/{utc-timestamp}.jsonl` contains one JSON object per line,
one per listing seen in that scrape run:

```json
{
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

### SQLite schema (derived, gitignored, rebuilt from snapshots)

**`Snapshots` table** — one row per scrape run:
- `SnapshotId` (PK, auto-increment)
- `ScrapedAtUtc` (DATETIME, indexed)
- `ListingCount` (INT) — how many listings in this run

**`Listings` table** — deduplicated by (Url, Address); one row per unique listing ever seen:
- `ListingId` (PK, auto-increment)
- `Url` (TEXT, NOT NULL)
- `Address` (TEXT, NOT NULL)
- `Title` (TEXT)
- `PostalCode`, `District` (TEXT)
- `OriginalPriceEur` (DECIMAL) — first price seen
- `CurrentPriceEur` (DECIMAL) — latest price seen
- `RoomsCount`, `LivingAreaSqm` (INT/DECIMAL)
- `Details` (JSON) — nested {PropertyType, BuildYear, Floor, Condition, Heating, ...}
- `FirstSeenAtUtc` (DATETIME) — earliest snapshot containing this listing
- `LastSeenAtUtc` (DATETIME) — latest snapshot containing this listing
- `IsActive` (BOOLEAN) — present in the most recent snapshot
- Unique constraint: (Url, Address)

**`Statistics` table** — one row per calendar day:
- `StatisticId` (PK, auto-increment)
- `SnapshotDateUtc` (DATE, PK, indexed)
- `TotalActiveListings` (INT)
- `NewListingsCount` (INT)
- `RemovedListingsCount` (INT)
- `PriceChangedCount` (INT)
- `AvgPricePerSqmEur` (DECIMAL)
- `AvgDaysOnMarketRemoved` (DECIMAL) — for listings removed that day

## Report content

Each `report` run shows (comparing today's snapshot to yesterday's):

- **New listings**: listings in today's snapshot not in yesterday's.
- **Price changes**: listings where CurrentPriceEur differs from OriginalPriceEur (€ and % delta).
- **Removed listings**: in yesterday's snapshot but not today's, with days-on-market (LastSeenAtUtc − FirstSeenAtUtc).
- **Aggregate statistics**: pulled from the Statistics table for today's date:
  - Total active listings.
  - Average €/m².
  - Average days-on-market for removed listings (that day).
  - 7-day and 30-day rolling trends (if history exists).

## Running on a schedule

Origin is GitLab (`code.andrena.de`), so scheduling uses **GitLab CI
scheduled pipelines**, not GitHub Actions:

- A scheduled pipeline runs `dotnet run -- scrape`, then commits + pushes
  the new `data/snapshots/*.jsonl` file back to the repo.
- Requires a push-capable token (project access token or deploy token)
  configured in GitLab project settings — a manual one-time setup step.

## Open questions / risks

- Scraper is coupled to current site HTML structure; will break silently
  if the site changes markup. Mitigated by parser unit tests against saved
  HTML fixtures, but real breakage will only surface at scrape time.
- "Removed" only means "not seen in latest snapshot" — could be sold,
  delisted, or a transient scrape failure (pagination miss, site downtime).
  No way to distinguish "sold" from "withdrawn" from the site alone.
- Scrape frequency determines resolution of all time-based stats (days-
  on-market, price-change timing) — daily is the assumed cadence unless
  stated otherwise.
