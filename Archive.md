# Flats Archive: Implementation Plan

**Status:** steps 1–4 implemented (.NET: 817 tests passed).

## Goal

Keep **everything** every scrape found, for analyses we cannot foresee yet. Aggregations alone are not enough.

- `FlatsArchive`: a full copy of every published flat row, **per scrape run**. It is never updated or deleted.
- `FlatPreviewsArchive`: each flat's title photo, stored **once** per listing and linked to its archive rows by
  `(Source, ListingId)`.
- A fourth tab **Archive** in the frontend lists the flats that are **no longer offered**, with their **last seen**
  date, computed by the backend.

## Storage estimate (measured 2026-11-06, 790 flats)

| | per scrape | 100 scrapes |
|---|---|---|
| Flat rows with indexes | about 0.5 MB | about 50 MB |
| Title photos (about 15.6 KB each, about 97% of a flat's bytes) | stored once | about 790 + about 100 new a week ≈ 10,800 photos ≈ 165 MB |
| **Total** | | **about 200–250 MB** |

If every scrape copied the photos too, 100 scrapes would take about 1.3 GB. That's why photos are stored once.

## Today

- `FlatsSnapshots` (`PublishedFlat`) holds only the latest published scrape of each source. On publish,
  `FlatRepository.PublishSourceAsync` updates the flats still offered, adds new ones and deletes the rest, all in
  one `SaveChanges`.
- A flat that stays offered keeps its `CreatedAtUtc`, which is its **first seen** time. `ScrapedAtUtc` is when it
  was scraped in the run that published it.
- `FlatPreviews` keeps one photo per current flat. `RemoveStalePreviewsAsync` deletes the photos of removed flats
  on publish, and `FlatPreviewDownloader` downloads a photo again only when its `ImageUrl` changes.
- Each run, CLI `scrape` and web `POST /api/scrape` alike, goes through `ScrapeRunService`, which creates a
  `ScrapingHistories` row (`runId`) before calling `ScrapeExecutionService` → `ScraperCoordinator`. The coordinator
  does not know the `runId` yet.

## Design decisions

### Date and scrape id: what an archive row needs

The row already has `ScrapedAtUtc`, which is enough to say *when* a flat was seen. We add one column anyway:
**`ScrapingHistoryId`**, the run that archived the row. Reasons:

- A run lasts minutes to hours, and its sources run in parallel, so `ScrapedAtUtc` differs within one run.
  "All flats of scrape N" is then a plain `WHERE ScrapingHistoryId = N`, with no time window to guess.
- It links to the run's `StartTimeUtc`, `Status` and `Sources`, which says which sources a run covered. Without
  that, a flat missing from a scrape of other sources could not be told apart from a removed one.

Every other column is the same as in `FlatsSnapshots`, including `CreatedAtUtc`, copied from the published row,
so it keeps meaning "first seen". The key is **`(Source, ListingId, ScrapingHistoryId)`**: one row per flat per
run. That key order also serves the archive tab's per-flat grouping. A second index on `ScrapingHistoryId` serves
per-run queries. The unique index `(Source, Url, Address)` of the other two tables can't apply here: the same
flat is in every run.

### When rows are archived

They're archived in `PublishSourceAsync`, in **the same `SaveChanges`** that replaces the source's
`FlatsSnapshots` rows. The archive therefore always matches exactly what was published, atomically:

- A source that fails or finds nothing publishes nothing and archives nothing, just as it keeps its old rows.
- `scrape --source Immowelt` archives only Immowelt's flats, and `ScrapingHistories.Sources` records that.
- The staging table is not archived: it may hold half a failed run.

### Photos: stored once

`FlatPreviewsArchive` has the same columns and key `(Source, ListingId)` as `FlatPreviews`:

- `FlatPreviewRepository.SaveAsync` inserts the photo into the archive **if the listing has none yet**. The archive
  photo is never replaced or deleted, so it is the one the flat was **first** seen with. A later photo change by
  the portal only updates `FlatPreviews`.
- The link is logical, `(Source, ListingId)`, with no database foreign key: an archive photo belongs to all of a
  flat's archive rows.
- The photos of current flats are then stored in both tables. That's about 12 MB, constant rather than growing per
  scrape, and it keeps the current-offers code untouched.

### Archived offer: definition and computed fields

An **archived offer** is a flat that has rows in `FlatsArchive` but **none in `FlatsSnapshots`** (same `Source`
and `ListingId`). Its latest publish no longer had it: it was sold, delisted, or missed by a scrape (see "Removed"
ambiguity in CLAUDE.md).

- A source that was not scraped, or that failed, keeps its `FlatsSnapshots` rows, so its flats do **not** wrongly
  show up as archived.
- A flat missed by one scrape shows up as archived and goes back to Current Offers by itself the next time it is
  found. Nothing needs cleaning up.

One row per flat; the backend computes:

| Field | Computation |
|---|---|
| all `FlatOffer` fields | from the flat's **latest** archive row (highest `ScrapingHistoryId`) |
| `LastSeenUtc` | `MAX(ScrapedAtUtc)` of its archive rows |
| `FirstSeenUtc` | `MIN(CreatedAtUtc)` of its archive rows (first seen in `FlatsSnapshots`, also for backfilled rows) |
| `HasPreview` | `FlatPreviewsArchive` has a row for `(Source, ListingId)` |

The frontend derives "days listed" from the two dates; there's no need to store it.

### Backfill

The migration fills the archive with what we have today, so the tab and analyses don't start from nothing:

- `FlatPreviewsArchive` ← all of `FlatPreviews` (`INSERT … SELECT`).
- `FlatsArchive` ← all of `FlatsSnapshots`. Each row gets the latest run with `StartTimeUtc <= ScrapedAtUtc`, which
  is the run that published it. Its status doesn't matter: runs never overlap, and a canceled or failed run may
  still have published sources. A flat older than every run is left out (none in practice), because the run id is
  part of the key.

Flats removed before this feature are gone, so the archive history starts with this migration.

## UI

```
┌───────────────────────────────────────────────────────────────────────────────────────┐
│ FlatMonitor   Dashboard  Current Offers  History  ┌Archive┐          [Scrape] ↻  ⚙    │
│ Districts: [all ▾]  Rooms: [all ▾]  Price: [ – ]  Last seen: [last 3 months ▾]  Preset │
│ 312 flats no longer offered                                                           │
│ District   Rooms  Price      m²   €/m²   First seen  Last seen ▼  Days  Source        │
│ Durlach    3      389.000 €  82   4.744  02.10.2026  29.10.2026    27   [IS24] ⓘ       │
│ Weststadt  2      265.000 €  58   4.569  13.10.2026  22.10.2026     9   [IW]   ⓘ       │
└───────────────────────────────────────────────────────────────────────────────────────┘
```

- The route is `/archive` and the tab link comes after History, like the other tabs.
- It has the same table and card layout as Current Offers: sorting, district/rooms/price filters and filter
  presets, the photo on hover over ⓘ, and opening a row by click or middle click.
- It adds the columns **First seen**, **Last seen** (default sort, newest first) and **Days**, plus a **Last seen**
  period filter (4 weeks / 3 months / 1 year / all).
- The portal link stays, but its title says the listing is probably offline. Portals often still show sold
  listings.
- Reload ↻ and the end of a scrape reload the tab through `DataReloadService`, as on the other tabs.

## Steps

### 1. Backend model and migration ✅ DONE

- `ArchivedFlat : FlatRecord` with `int ScrapingHistoryId` (`Models/FlatRecord.cs`) and `static From(FlatRecord, int runId)`.
- `FlatRecordConfiguration<T>`: the key and the unique index are overridable (`protected virtual ConfigureKey`), so
  `ArchivedFlatConfiguration` (table `FlatsArchive`, prefix `idx_Archive_`) keys on `(Source, ListingId,
  ScrapingHistoryId)` and has no `(Source, Url, Address)` unique index. The other two tables are unchanged, and the
  migration doesn't touch them.
- `ScrapingHistoryId` is a foreign key to `ScrapingHistories` with `Restrict`: a run that has archived flats can't
  be deleted. Its index is `idx_Archive_ScrapingHistoryId`.
- `FlatPreviewRecord`: a new abstract base of `FlatPreview` and `ArchivedFlatPreview` (`static From(FlatPreviewRecord)`),
  mapped by the shared `FlatPreviewRecordConfiguration<T>` → tables `FlatPreviews` and `FlatPreviewsArchive`. There's
  no foreign key to `FlatsArchive`.
- `FlatsDbContext`: `DbSet<ArchivedFlat> FlatsArchive`, `DbSet<ArchivedFlatPreview> FlatPreviewsArchive`.
- Migration `20261106075202_AddFlatsArchive` with the backfill SQL above (`migrationBuilder.Sql`).
- Tests: `Models/FlatRecordTests`, `Models/FlatPreviewTests` (`From` copies every field);
  `Storage/FlatsArchiveModelTests` (keys, indexes, foreign key, same columns as `FlatsSnapshots` plus the run id,
  the other tables keep their keys, two runs of a flat side by side, unknown run rejected);
  `Storage/AddFlatsArchiveMigrationTests` (run per flat across statuses, flat older than every run left out,
  values and first-seen time kept, previews copied, current tables kept).

### 2. Archive at publish time ✅ DONE

- The run id is passed down as a required first parameter: `ScrapeRunService` (its `runId`) →
  `ScrapeExecutionService.ExecuteAsync(scrapingHistoryId, …)` → `ScraperCoordinator.ScrapeAllSourcesAsync(scrapingHistoryId, …)`
  → `IFlatRepository.PublishSourceAsync(source, scrapingHistoryId, ct)`.
- `PublishSourceAsync` adds an `ArchivedFlat` for every published row in the existing `SaveChanges`. `CreatedAtUtc`
  is the published row's (first seen) and `UpdatedAtUtc` is the time it was archived.
- `PublishSourceAsync` now detaches its rows (flats, archive rows, previews marked for deletion) **also after a
  failed save**, as `AddToRunAsync` already did. The other sources go on with the shared context, and its next
  `SaveChanges` must not retry the failed source's rows.
- Tests:
  - `FlatRepositoryTests`: archived with the run and every field; only the published source, not removed flats;
    the next run adds rows and leaves the earlier ones as they were; first-seen time kept; a failing archive write
    publishes nothing and leaves nothing tracked.
  - `ScraperCoordinatorTests`, `ScrapeExecutionServiceTests`, `ScrapeRunServiceTests`: the run id reaches
    `PublishSourceAsync`.
  - `ScrapeBackgroundServiceTests`: end to end, a job archives its published flats with its run, and a failed source
    archives nothing.
  - Tests that publish on SQLite get a run row from `Storage/TestRuns.cs`. `ScrapeBackgroundServiceTests` seeds its
    existing flat directly instead, because it expects only the job's run.

### 3. Archive photos once ✅ DONE

- `FlatPreviewRepository.SaveAsync`: in the same `SaveChanges`, insert into `FlatPreviewsArchive` the previews whose
  `(Source, ListingId)` it doesn't have yet. It's one ids-only query per source batch, like the existing lookup.
  A flat that appears twice in one batch is archived with its first photo. The archive rows are detached afterwards,
  also after a failed save, like the current previews.
- `RemoveStalePreviewsAsync` is unchanged: it touches only `FlatPreviews`. `FlatPreviewDownloader` is unchanged too:
  it still decides from `FlatPreviews` whether to download.
- Tests (`FlatPreviewRepositoryTests`): the first save archives the photo with every field; a new photo replaces
  the current one but not the archived one; the same flat twice in a batch is archived once; a flat that came back
  gets its photo stored again and keeps the archived one; archiving is per source; nothing stays tracked; a failed
  save archives nothing. `FlatRepositoryTests`: publishing a source without a flat keeps its archived photo.

### 4. Archived offers API ✅ DONE

- Read model `ArchivedOffer : FlatOffer` (`Storage/ReadModels/ArchivedOffer.cs`) with `DateTime FirstSeenUtc`,
  `DateTime LastSeenUtc`.
- `IFlatRepository.GetArchivedOffersAsync`: one LINQ query, which EF translates into a single SQL statement (checked).
  - It groups `FlatsArchive` by `(Source, ListingId)` where no `FlatsSnapshots` row matches (`NOT EXISTS`), takes
    `MAX(ScrapingHistoryId)` and `MIN(CreatedAtUtc)`, and joins back to the latest row by the primary key.
  - `LastSeenUtc` is that latest row's `ScrapedAtUtc`, since runs never overlap, so there's no `MAX(ScrapedAtUtc)`.
  - `HasPreview` is an `EXISTS` on `FlatPreviewsArchive`. The result is ordered by last seen, newest first.
  - `MIN(...)` bypasses the UTC converter, so both dates are given `DateTimeKind.Utc` afterwards and the JSON carries
    a `Z`.
- `IFlatPreviewRepository.GetArchivedAsync(source, listingId)`.
- `FlatsController`: `GET api/flats/archived-offers` (`ApiResponse<List<ArchivedOffer>>`) and
  `GET api/flats/{source}/{listingId}/archived-preview`.
  - The preview endpoint shares the private `PreviewFile` helper with `preview`: same content-type allow-list,
    `nosniff` and one-hour client cache.
- Tests:
  - `FlatRepositoryTests` (SQLite): removed flats listed once with the latest run's values, most recently seen first;
    flats still offered or offered again left out; a source that kept its flats; first seen is the earliest for a
    flat removed twice; UTC dates; `HasPreview` from the archived photo; empty archive.
  - `FlatPreviewRepositoryTests`: archived photo also after the current one is gone; `null` without one.
  - `FlatsControllerTests`: archived offers response; archived preview 200 (from the archive, not the current
    previews) / 404 / not a photo type; cancellation token passed on.

### 5. Frontend: Archive tab

- `ArchivedOffer` model and `FlatsService.getArchivedOffers()`; `archivedPreviewUrl()` next to `offerPreviewUrl()`.
- `ArchiveTabComponent` (`features/dashboard/tabs/archive-tab/`), route `archive`, nav link in
  `dashboard.component.html`.
- Reuse rather than copy: the shared filter toolbar, preset bar, sortable header, `OfferPreviewComponent` and
  `offer-row.ts` helpers. If the filter and sort code of `OffersTabComponent` would be duplicated, move it into a
  helper both tabs use first, as a separate refactoring step with no behavior change and the existing offers
  specs staying green.
- Columns and filters as in the UI section; default sort Last seen, newest first; an empty state when nothing has
  been archived yet.
- Specs: loads and lists archived offers; sorting and the last-seen filter; preview URL uses the archive endpoint;
  reload through `DataReloadService`; the nav shows four tabs.

### 6. Docs

- `CLAUDE.md` (data flow: archive at publish, photo once), `WohnungMonitor.md` (tables, endpoints, archive tab),
  `BUILD_AND_RUN.md` if anything about running changes.

## Verification

- `dotnet test` and the Angular specs are green.
- Run a scrape on a copy of `listings.db`: `FlatsArchive` grows by one row per published flat and
  `FlatPreviewsArchive` only by new listings. The DB grows by about 0.5 MB plus new photos, not by about 13 MB.
- The Archive tab lists the flats that left `FlatsSnapshots`, with plausible first and last seen dates.

## Open questions

- Archive photo: keep the **first** photo (planned) or replace it with the latest one?
- Archive tab: should it also show the **price at first sight** (price drops before removal)? It's cheap to add in
  step 4 (`PriceEur` of the earliest archive row).
