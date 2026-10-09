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
  service charge, energy certificate type, primary energy source, contact
  name, listing status (private/commercial), current use, availability.
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

Scraped per listing per run:
`ListingId, Url, Title, PriceEur, RoomsCount, LivingAreaSqm, PropertyType,
Address, District, PostalCode, BuildYear, Floor, Condition, Heating,
Flooring, ServiceChargeEur, EnergyCertificateType, PrimaryEnergySource,
ContactName, ListingStatus, CurrentUse, Availability, ScrapedAtUtc`

Derived (computed from snapshot history, not scraped directly):
`FirstSeenAtUtc, LastSeenAtUtc, IsActive, RemovedAtUtc` (best-effort: first
snapshot after its last appearance), and a `PriceHistory` table (one row
per price change between consecutive snapshots).

## Report content

Each `report` run shows:

- New listings since the previous snapshot.
- Price changes since the previous snapshot (old → new, € and % delta).
- Removed listings since the previous snapshot, with days-on-market.
- Aggregate trends across all history: avg €/m² over time, active-listing
  count over time, avg days-on-market for removed listings.

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
