# Backend Refactoring Suggestions (round 2)

## Overview

The first round (composition root, injected `DbContext`, `IAsyncEnumerable` pipeline, job queue, logging,
central error handling) is done. The backend is now ~4,200 lines of C# without migrations, and its structure is
sound. This round looks at what remains:

1. **Bugs**: a failed or empty source still counts as success, a partial run behaves differently before and
   after a restart, and a typo means IS24 `CurrentUse` is never "vermietet". Fix these first, each with a test.
2. **Structural**: replace backup → clear → restore with build-then-swap, give the CLI the same run
   bookkeeping as the API, and settle on one error-handling policy in the scrapers.
3. **Source-specific cleanups**: OhneMakler and ImmobilienScout24.
4. **API and model**: DTO placement, the overloaded word "snapshot", aggregation semantics.
5. **Small cleanups**: dead code, stale comments, package references.
6. **Suggested order**.

Line references are to the files as of commit `4ac6af7`.

---

## 1. Bugs

### 1.1 A source that finds nothing, or stops paginating early, counts as success ✅ Done

**Status:** Done, except the optional "large drop compared with the previous run" check. Also fixed: an
`HttpClient` timeout (`TaskCanceledException`) no longer counts as cancellation in the coordinator and in the
per-flat enrichment catches; only the caller's token cancels.

**Severity:** High · **Files:** `Scraping/OhneMakler/OhneMaklerFlatsPageFetcher.cs:38-49`,
`Scraping/ImmobilienScout24/ImmobilienScout24FlatsPageFetcher.cs:107-111`,
`Scraping/ImmobilienScout24/ImmobilienScout24Scraper.cs:61-68`, `Scraping/ScraperCoordinator.cs:109-110`

Round 1 (1.1) made exceptions reach the coordinator. But the page fetchers still turn failures into a normal
end of pagination:

- **OhneMakler page 1:** only `HttpRequestException` is fatal. An `HttpClient` timeout throws
  `TaskCanceledException`, which the next `catch (Exception)` treats as "later page failed, stop": 0 flats,
  no error. The same `catch` also swallows `OperationCanceledException` from the caller's token.
- **OhneMakler page n > 1, IS24 page n > 1:** any exception ends pagination with a warning. The run is
  `Successful` with, for example, half the listings.
- **IS24 page 1:** when the robot guard is shown, `WaitForSelectorAsync` times out ("proceeding anyway"),
  the page has no cards, `ParseListingsPage` returns `[]`, and the source yields nothing.

In each case `ScrapeSummary.Success` is true, so `ScrapeExecutionService` **deletes the backup**, the day's
aggregation is computed without those flats, and the history row says `Successful`. The coordinator only
logs "No listings found".

**Fix:**
- Fetchers: only "no next button" and "page has no cards" end pagination normally. Any other exception
  propagates (cancellation included). Page 1 without cards is an error for IS24 (`robot guard?`) and for
  OhneMakler.
- Coordinator: a source that saved 0 flats is recorded as an error (`"{Source}: no flats found"`). Optionally
  also flag a large drop compared with the previous run (e.g. < 50 % of the source's previous row count; the
  backup table has it).
- Tests: fetcher with a timeout on page 1 → exception; fetcher failing on page 2 → exception; coordinator
  with an empty scraper → `Success == false`.

### 1.2 A partially failed run is kept until the next restart, then silently undone ✅ Done

**Status:** Done with 2.1: a failed source keeps its previous rows, the others are replaced, and nothing is
restored at startup any more.

**Severity:** Medium · **Files:** `Api/Services/ScrapeExecutionService.cs:48-54`,
`Api/Services/ScrapeStartupRecovery.cs:40-53`

When one source fails, `ExecuteAsync` keeps the new data (the good sources' flats, none of the failed
source's) **and** keeps the backup. The run is `PartialFailed`. The API now serves the partial table and the
aggregation for today is computed from it.

When the service restarts, `ScrapeStartupRecovery` sees that the last run was not `Successful` and restores
the backup. The flats go back to the previous run, but today's aggregation stays partial. So what the user
sees depends on whether the server was restarted, and flats and aggregations can disagree.

**Fix:** decide per source, not per run (see 2.1): a failed source keeps its rows from the previous run, the
successful sources get their new rows. Then a partial run needs no backup, the aggregation includes every
source, and recovery only has to deal with interrupted runs. Until 2.1 is done, at minimum make recovery
skip `PartialFailed` (so it matches what the running service does) and add a test for it.

### 1.3 IS24 `CurrentUse` is never "vermietet" (typo) ✅ Done

**Status:** Done. Expose pages do have a field for it: the "Vermietet" tag `data-qa="is24qa-rented-label"`
(171 of 561 saved pages). `CurrentUse` is "Vermietet" when that tag is present or "Bezugsfrei ab" says
"vermietet", and empty otherwise; the "Sofort" → "Nicht vermietet" guess is gone. Tests use the real markup
(`Fixtures/ImmobilienScout24/detail-page-rented.html`) and real availability values.

**Severity:** Medium · **File:** `Scraping/ImmobilienScout24/ImmobilienScout24DetailParser.cs:80-81`

```csharp
if (availability.Contains("Vermitet", StringComparison.OrdinalIgnoreCase))
    currentUse = "Vermitet";
```

The German word is "vermietet". A real page never contains "Vermitet", so rented flats get `CurrentUse = null`.
The tests (`ImmobilienScout24DetailParserTests.cs:634-697, 783-795`) use the same typo in their inline HTML, so
they pass. Also, "Sofort" → "Nicht vermietet" is a guess: "bezugsfrei ab sofort" says when it is free, not
whether it is rented now.

**Fix:** match "vermietet", store "Vermietet", and fix the tests (use a real fixture value). Consider leaving
`CurrentUse` empty for IS24 unless the page has a field for it, instead of inferring it from availability.

### 1.4 An IS24 flat whose detail page fails is dropped, and reported as "outside the tracked districts" ✅ Done

**Status:** Done. `ApplyTo` falls back to the card's values. The coordinator logs flats without a district
apart from flats in an untracked district, as a warning above 10 % of the source's flats. A flat whose detail
page fails is still dropped (the card has no district), but now visibly.

**Severity:** Medium · **Files:** `Scraping/ImmobilienScout24/ImmobilienScout24DetailParser.cs:18-30`,
`Scraping/ImmobilienScout24/ImmobilienScout24CardParser.cs:33-44`, `Scraping/ScraperCoordinator.cs:111-112`

The card parser never sets `District`; only the expose metadata does. So:
- the detail page fails to load (`FetchDetailHtmlAsync` returns `null`): the card is kept without a district;
- the detail page loads but has no `commissionSplitConfig` script (robot guard, changed markup): `ApplyTo`
  sets district, price, rooms and area to `null`, even where the card had them.

Either way the coordinator drops the flat and logs it as "skipped … outside the tracked districts". A broken
detail parser looks like a quiet market.

**Fix:** in `ApplyTo`, fall back to the card value (`Metadata?.PriceEur ?? card.PriceEur`, …). In the
coordinator, count flats without a district separately from flats in an untracked district, and warn when a
source has many without one.

### 1.5 Smaller ✅ Done

**Status:** Done. A cancel before the history row is saved now ends the job as `Canceled` (no history row).
Unknown CLI commands print the usage and return 1; options such as `--urls` still start the web server
(`CliCommandParser`).

- **Canceled before the history row is saved** (`ScrapeBackgroundService.cs:88-89`): a cancel between
  `StartJob` and the first `SaveChangesAsync(jobCt)` ends up in the outer `catch` as "Unexpected error" and the
  job as `Failed`, not `Canceled`. Goes away with 2.2.
- **Unknown CLI command starts the web server** (`Program.cs:32`): `dotnet run -- scrap` silently starts the
  server. Print the usage and return 1 instead.

---

## 2. Structural refactorings

### 2.1 Build-then-swap instead of backup → clear → restore ✅ Done

**Status:** Done. A run writes into `FlatsSnapshots_Staging` (EF entity `StagedFlatSnapshot`, same columns via a
shared `FlatRecordConfiguration<T>`; migration `AddFlatsSnapshotsStaging`, which also drops the old backup table).
`PublishSourceAsync` replaces a source's rows in one `SaveChanges` (update / add / delete, so a flat still offered
keeps its `CreatedAtUtc` = first seen). The coordinator begins the run, publishes each source that finished with
flats, and empties staging at the end; it also stamps every flat with its scraper's source. A cancel still
refreshes the aggregations for the sources published before it. Recovery only marks `Running` runs as canceled
and empties staging. The history table from the last paragraph is not done.

**Severity:** High · **Impact:** correctness (1.2), simplicity, what users see during a scrape
**Files:** `Api/Services/ScrapeExecutionService.cs`, `Api/Services/ScrapeStartupRecovery.cs`,
`Storage/FlatSnapshotRepository.cs:12-39`, `Scraping/ScraperCoordinator.cs`

Today a scrape copies `FlatsSnapshots` to `FlatsSnapshots_Backup`, **empties** `FlatsSnapshots`, refills it
batch by batch, and on failure restores from the backup (in `ExecuteAsync`, and again at startup). Problems:

- During the whole scrape (IS24 alone takes many minutes) `GET current-offers` returns a half-filled table.
- Five repository methods, a recovery class and their tests exist only to undo the "clear" step.
- Partial failure has no good answer (1.2).

**Suggested design:** write the run into a staging table, then publish per source in one transaction.

```csharp
// ISnapshotRepository (sketch)
Task BeginRunAsync(CancellationToken ct);                           // DROP + CREATE FlatsSnapshots_Staging (same schema)
Task AddToRunAsync(IReadOnlyList<Flat> flats, CancellationToken ct); // INSERT into staging
Task PublishSourceAsync(string source, CancellationToken ct);       // one transaction:
                                                                    //   DELETE FROM FlatsSnapshots WHERE Source = @s;
                                                                    //   INSERT INTO FlatsSnapshots SELECT ... FROM staging WHERE Source = @s;
Task DiscardRunAsync(CancellationToken ct);                         // DROP staging
```

- The coordinator publishes a source only when it finished without error (and with flats, see 1.1). A failed
  source keeps its previous rows, so `PartialFailed` data is consistent and needs no backup.
- Cancellation or a crash leaves the live table untouched; recovery shrinks to "mark `Running` as `Canceled`,
  drop staging".
- `BackupFlatsSnapshotsAsync`, `RestoreFlatsSnapshotsAsync`, `HasBackupAsync`, `DeleteBackupAsync`,
  `ClearAsync` and the restore half of `ScrapeStartupRecovery` go away.
- The staging table can be an EF entity mapped to its own table (simplest: same `FlatSnapshot` type via
  `modelBuilder.SharedTypeEntity`) or raw SQL as today; either way add a migration.

Related open product decision from round 1 (2.8): the table is still "latest scrape only", so the goals in
`CLAUDE.md` (new flats, price changes, removals) cannot be computed. Publishing per source is the natural
place to add it later: compare staging with live before replacing (new keys, changed prices, missing keys)
and write those events to a history table.

### 2.2 One scrape run service for the API and the CLI ✅ Done

**Status:** Done. `ScrapeRunService` (scoped) records start, progress and end state through
`IScrapingHistoryRepository` (`StartAsync`, `AddProgressAsync`, `CompleteAsync`, the last two via
`ExecuteUpdateAsync`; they did not exist yet) and returns a `ScrapeRunResult` instead of throwing.
`ScrapeBackgroundService` only dequeues, starts the job and maps the result; the `ChangeTracker.Clear()`
workaround is gone. The `scrape` command uses the same service; Ctrl+C cancels the run gracefully, a second
Ctrl+C quits at once. Found on the way: `ScrapingHistories` dates have no UTC converter (they read back as
`Unspecified` and serialize without "Z"), unlike the flat tables.

**Severity:** Medium · **Files:** `Api/Services/ScrapeBackgroundService.cs:71-149`, `Program.cs:46-86`

`ScrapeBackgroundService` writes the `ScrapingHistory` row (start, progress, end state) through
`FlatsDbContext` directly; the CLI calls `ScrapeExecutionService` without any of it. Consequences:

- A CLI scrape leaves no history row, so the dashboard's "last scrape" is wrong and startup recovery cannot
  see an interrupted CLI run.
- The CLI passes no cancellation token: Ctrl+C kills the process mid-run (with today's design: table cleared,
  backup never restored until the server's next startup, and only if the last *history* row says so).
- The history writes bypass `IScrapingHistoryRepository`, which already exists.

**Fix:** move "create history row → execute → complete history row (Successful/PartialFailed/Failed/Canceled)"
into one scoped service (e.g. `ScrapeRun`, or into `ScrapeExecutionService` itself), using
`IScrapingHistoryRepository` (`StartAsync`, `AddProgressAsync`, `CompleteAsync`). `ScrapeBackgroundService`
then only dequeues, starts the job and calls it. The CLI calls the same service with a token from
`Console.CancelKeyPress`. Moving the history writes into the repository also removes the
`ChangeTracker.Clear()` workaround from the background service (the repository can use `ExecuteUpdateAsync`).

### 2.3 One error-handling policy in the scrapers ✅ Done

**Status:** Done. Error-hiding catches left: per card (`OhneMaklerFlatCardParser`, `ImmobilienScout24CardParser`)
and per flat enrichment (`FlatScraper`). Removed: the XPath helpers, the parser methods (incl. OhneMakler's
per-value `TryExtract`), the second per-card and the per-page catch in `ImmobilienScout24Scraper`, and the detail
fetch catches (`ImmobilienScout24DetailScraper` now throws, `OhneMaklerScraper` no longer catches). Kept on
purpose: the coordinator's per-source catch, the IS24 browser launch cleanup and the two "wait for content,
read the page anyway" timeouts (IS24 first page, expose attributes). The detail parsers lost their unused logger.
The OhneMakler card now carries `OriginalPriceEur = PriceEur` itself (the removed catch used to set it).
The coordinator logs a "parse health" line per source, a warning when more than 20 % of the saved flats lack
price, area or rooms.

**Severity:** Medium · **Impact:** markup changes become visible; less code
**Files:** see list below

`CLAUDE.md` names markup changes as the main risk. The scrapers currently hide them under many layers of
`catch`, most of which log and return `null`:

| Layer | Location |
|---|---|
| XPath helpers return `null` on exceptions | `Common/DetailExtractors.cs:16-24, 29-37`, `ImmobilienScout24CardParser.cs:82-89` |
| Whole parser methods return `null` | `OhneMaklerDetailPageParser.cs:54-86, 89-100`; `ImmobilienScout24DetailParser.cs:63-107, 112-149, 158-193` |
| Per card | `OhneMaklerFlatCardParser.cs:75-78`, `ImmobilienScout24CardParser.cs:46-50`, and again `ImmobilienScout24Scraper.cs:169-172` |
| Per page | `ImmobilienScout24Scraper.cs:177-180` |
| Per detail fetch | `ImmobilienScout24DetailScraper.cs:47-51`, `OhneMaklerScraper.cs:97-103` |
| Per flat enrichment | `FlatScraper.cs:80-85` |

HtmlAgilityPack does not throw for a missing node, only for an invalid XPath, which is a programming error that
tests should catch. **Keep two catches:** per card (skip the card) and per flat enrichment in `FlatScraper`
(keep the card data). Remove the rest. The IS24 detail scraper and the OhneMakler scraper then just throw,
and `FlatScraper` logs it once.

To make markup changes visible rather than quiet, have the coordinator log per source how many saved flats
lack price, area or rooms (a "parse health" line), and warn above a threshold.

### 2.4 `FlatScraper`: per-run state instead of nullable fields ✅ Done

**Status:** Done as sketched. `FlatScraper` takes the source name and its `HtmlSnapshotStore`, empties the store,
starts a session (`StartAsync` → `IScrapeSession`) and disposes it with `await using`. Both scrapers are now a
constructor plus `StartAsync`; their run state (page fetcher, browser, detail scraper, parsers) lives in a private
`Session`. `InitializeAsync`, `ClearPreviousSnapshotsAsync`, `CleanupAsync`, the nullable fields and their
guards are gone.

**Severity:** Low · **Files:** `Scraping/Common/FlatScraper.cs`, both scrapers

Both scrapers keep run state in nullable fields set by `InitializeAsync` (`_pageFetcher`, `_session`,
`_detailScraper`) and guard every method with `if (… == null) yield break / return baseListing`, which can
never happen. Both override `ClearPreviousSnapshotsAsync` with the same line.

**Suggested shape:**
```csharp
public abstract class FlatScraper : IScraper
{
    protected abstract Task<IScrapeSession> StartAsync(CancellationToken ct);   // launch browser, create fetchers

    protected interface IScrapeSession : IAsyncDisposable                        // DisposeAsync replaces CleanupAsync
    {
        IAsyncEnumerable<IReadOnlyList<Flat>> FetchCardsAsync(CancellationToken ct);
        Task<Flat> EnrichAsync(Flat card, CancellationToken ct);
    }
}
```
The base class owns the `HtmlSnapshotStore` (created from `Source`) and clears it; sessions get it passed in.
No nullable fields, no guards, `await using` instead of `try/finally CleanupAsync`.

---

## 3. Source-specific cleanups

### 3.1 Shared ✅ Done

**Status:** Done. Both page fetchers only pass the HTML on; each session parses a page once and stops at the
first page without cards (OhneMakler: an empty first page is the error from 1.1, now raised by the session).
The OhneMakler fetcher fetches a page only when the session asks for it. `ImmobilienScout24Selectors`,
`ImmobilienScout24DetailSelectors` and `OhneMaklerDetailSelectors` are static (`const`, or `static readonly`
where built by `BuildXPathPatterns`); the `_selectors` fields are gone.

- **Pages are parsed twice.** Both page fetchers load the HTML into an `HtmlDocument` only to count cards
  (`OhneMaklerFlatsPageFetcher.cs:52-61`, `ImmobilienScout24FlatsPageFetcher.cs:94-102`), then the scraper
  parses it again. Let the fetchers return HTML and let the scraper stop when a page parses to zero cards.
- **Selector classes:** `ImmobilienScout24Selectors`, `ImmobilienScout24DetailSelectors` and
  `OhneMaklerDetailSelectors` are instantiated classes whose properties return constants; `OhneMaklerSelectors`
  is static. Make all four `static` with `const`/`static readonly` fields, and drop the `_selectors` fields.

### 3.2 OhneMakler ✅ Done

**Status:** Done. `Flat.DetailPageUrl` and the tuple are gone (`ParsePage` returns `List<Flat>`, enrichment uses
`Url`; old JSONL files with the field still load). `ExtractCurrentUse` reads `CurrentUseXPath` / `UsageXPath`
with `ExtractText`; `ExtractFieldByMultiplePatterns` is deleted. `OhneMaklerAddressHelper` is a private
`SanitizeAddress` in the detail parser, using `HtmlEntity.DeEntitize` (also decodes `&amp;` etc.).
`InitializeAsync` and `CleanupAsync` were already gone with 2.4.

- **`Flat.DetailPageUrl` duplicates `Url`.** The card parser sets both to the same value and also returns it
  as a tuple element (`OhneMaklerFlatCardParser.cs:20, 70-73`). Remove `DetailPageUrl` from `Flat` and the tuple;
  enrich from `Url` (as IS24 does). It also leaks into the JSONL files.
- **`ExtractCurrentUse`** (`OhneMaklerDetailPageParser.cs:207-215`) repeats `BuildXPathPatterns` by hand and passes
  a `label` argument that `ExtractFieldByMultiplePatterns` ignores. Add `CurrentUseXPath`/`UsageXPath` to the
  selectors and use `ExtractText` like the other fields; then `ExtractFieldByMultiplePatterns` is unused.
- **`InitializeAsync` only creates the page fetcher**: create it in the constructor (or in the session, 2.4).
- **`CleanupAsync`** is `async` with an empty body; return `Task.CompletedTask` (or drop it with 2.4).
- **`OhneMaklerAddressHelper`**: a one-method static class used only by the detail parser; inline it as a private
  method there. Also: `HtmlEntity.DeEntitize` instead of replacing `&ndash;` by hand.

### 3.3 ImmobilienScout24 ✅ Done

**Status:** Done. `ImmobilienScout24BrowserScraper.LaunchAndAuthenticateAsync` is `ImmobilienScout24Browser.LaunchAsync(ct)`
(Playwright takes no token, so it checks between the steps). One CSS (`ListingCardCss`) and one XPath
(`ListingCardXPath`) card selector in `ImmobilienScout24Selectors`; the loose one the robot guard could satisfy is
gone. `ExposeMetadata` is a top-level record with `init` properties, without the never-set `OriginalPriceEur`.
The size heuristic is gone: a first page without cards throws, and the message says where the page was saved
(`ImmobilienScout24DebugPage`, UTC file name) or how to turn saving on.

- **`ImmobilienScout24BrowserScraper.LaunchAndAuthenticateAsync`** neither scrapes nor authenticates. Rename to
  `ImmobilienScout24Browser.LaunchAsync` (it returns the session), and pass the cancellation token.
- **The listing-card selector exists three times, differently:** `ImmobilienScout24BrowserScraper.cs:38`
  (loose CSS, any `div[class*='listing']`), `ImmobilienScout24FlatsPageFetcher.cs:16` (CSS) and
  `:96` (XPath), and `ImmobilienScout24Selectors.ListingCardXPath`. Keep one CSS and one XPath constant in
  `ImmobilienScout24Selectors`. With the loose one, the robot guard can "satisfy" the wait.
- **`ExposeMetadata`**: `OriginalPriceEur` is never set (so `ApplyTo`'s middle fallback is dead). Make it a
  `record` with `init` properties, defined next to `ImmobilienScout24DetailPage` rather than nested in the parser.
- **`WarnAboutSmallFirstPageAsync`**: with 1.1, a first page without cards is an error. Keep the debug-page
  saving, but log the error through the exception instead of a size heuristic (`< 10000` bytes).
- **`DateTime.Now`** in the debug file name (`ImmobilienScout24Scraper.cs:87`); everything else uses UTC.

---

## 4. API and model

### 4.1 Put DTOs in one place and return types, not entities ✅ Done

**Status:** Done. `FlatOffer`, `FlatHistory`, `FlatAggregation` moved to `Storage/ReadModels` (namespace
`FlatMonitor.Storage.ReadModels`), as the repositories return them. `ScrapingStatusResponse` returns the new
`ScrapingHistoryDto` record. `FlatHistory.ScrapingDate` is a `DateOnly`; `ScrapeJobStatusResponse.Status` is the
`ScrapeJobStatus` enum, serialized by name. `ApiJsonShapeTests` pin the JSON: unchanged for the frontend.

**Severity:** Low · **Files:** `Models/FlatOffer.cs`, `Models/FlatHistory.cs`, `Models/FlatAggregation.cs`,
`Api/Contracts/ScrapingStatusResponse.cs`, `Api/Contracts/ScrapeJobStatusResponse.cs`

- `FlatOffer`, `FlatHistory`, `FlatAggregation` are API read models but live in `Models` next to entities;
  the other DTOs live in `Api/Contracts`. Move them (or into `Storage/ReadModels` if repositories keep
  returning them).
- `ScrapingStatusResponse` returns the `ScrapingHistory` **entity**. Add a small DTO so a new column does not
  silently become API surface.
- `FlatHistory.ScrapingDate` is a `string` formatted inside the query (`FlatAggregationRepository.cs:45`).
  Use `DateOnly`: System.Text.Json writes it as `"yyyy-MM-dd"` already, so the JSON does not change.
- `ScrapeJobStatusResponse.Status` is `string` (`job.Status.ToString()`), while `ScrapingStatus` uses
  `JsonStringEnumConverter`. Do the same for `ScrapeJobStatus`; the JSON stays the same.

### 4.2 "Snapshot" means five things

`FlatSnapshot` (a row of the live table), `ISnapshotRepository` (that table), `FlatsSnapshots_Backup`,
`SnapshotWriter`/`SnapshotFile` (JSONL debug files), `HtmlSnapshotStore` and `ClearPreviousSnapshotsAsync`
(HTML debug files), and `FlatAggregationSnapshot` (a row of `FlatAggregations`). Reading the coordinator or
the CLAUDE.md "never rebuild the database from snapshots" rule requires knowing which one is meant.

Suggested class renames (table names unchanged, so no migration): `FlatSnapshot` → `FlatRecord`,
`ISnapshotRepository` → `IFlatRepository`, `FlatAggregationSnapshot` → `DailyFlatAggregation`,
`SnapshotWriter` → `JsonlBackupWriter`, `HtmlSnapshotStore` → `HtmlPageArchive`. Do this after 2.1, which
removes several of these members anyway.

### 4.3 Aggregation: `Count` and the average use different flats

**File:** `Storage/FlatAggregationCalculator.cs:13-22`

`Count` counts every flat with a price; `AvgPricePerMeter` averages only those with a living area, and is
`0` when none has one. A district with three flats, none with an area, shows "3 flats, 0 €/m²", which the
history chart draws as a real point.

**Fix:** filter on `PricePerMeter.HasValue` (the only value the aggregation is about), so `Count` is "flats in
the average" and the `0m` case disappears. `District != null` is always true after filtering and can go.
(Product question: if the "flats offered" count per district is wanted too, add it as a second field.)

### 4.4 Smaller

- `FilterPresetsController.CreateAsync`/`UpdateAsync` repeat the same "check name → save → on
  `DbUpdateException` check name again" block. Move it into the repository as a result
  (`Created | NameTaken`), or one private helper.
- `IFilterPresetRepository.GetByIdAsync` is only used by tests.

---

## 5. Small cleanups

- **Dead code:** `OhneMaklerSelectors.LabelToDetailsField`, `OhneMaklerDetailSelectors.TitleXPath`,
  `SnapshotWriter.WriteSnapshotAsync` (tests only), public `ParseDetailFields(string)` in both detail parsers
  (tests only; they can call `Parse(html).Details`), `DetailExtractors.ExtractFieldByMultiplePatterns` (after 3.2).
- **Stale comments:** both scraper class summaries say they "save flats to database in batches" (the coordinator
  does); `ImmobilienScout24Scraper` mentions "Google OAuth"; `ScrapeExecutionService`/coordinator comments
  about the backup change with 2.1; `PrintUsage` says `scrape` "write[s] snapshot" and hard-codes port 5000.
- **`explore-is24`** (`Program.cs:88-122`) builds a second `HostApplicationBuilder` only to read the IS24 options
  and creates its own logger factory. Build the host once like `scrape` and resolve the options from DI.
- **`ScraperCoordinator`** takes a `string dataDirectory` only to pass it to `SnapshotWriter.Create`; let
  `SnapshotWriter` take `StorageOptions`, then the coordinator can be registered with a plain `AddScoped`.
- **`KarlsruheDistricts.ToCanonical`** scans the set linearly; `All` is a case-insensitive `HashSet`, so
  `TryGetValue(name.Trim(), out var canonical)` does it directly (expose `All` as `HashSet` internally).
- **Packages** (`dotnet build` warnings): `System.Security.Cryptography.Xml`/`.Pkcs` are pinned to 9.0.0 "to
  fix vulnerabilities" (commit `fa41402`) and are now themselves flagged high severity. On .NET 10 they come
  with the framework: remove both references and the `Microsoft.Extensions.DependencyInjection` one (NU1510).
  The transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 is flagged high severity: pin a fixed version in
  `Directory.Packages.props`.

---

## 6. Suggested order

| Step | Items | Why this order |
|---|---|---|
| 1 | 1.3, 1.5, packages from 5 | Small, independent, immediate value |
| 2 | 1.1 + 2.3 | Same files: once failures propagate, most of the extra `catch`es must go anyway |
| 3 | 1.4 | Needs 2.3's "no silent null" in the parser |
| 4 | 2.2 | Makes CLI and API runs the same before changing what a run does |
| 5 | 2.1 (fixes 1.2) | Biggest change; touches repository, coordinator, recovery, a migration |
| 6 | 3.x, 2.4 | Source cleanups, easier once error handling is settled |
| 7 | 4.x, rest of 5 | API/model polish and renames last, after the members they rename have shrunk |

Each step should come with tests (NUnit + FluentAssertions, in-memory SQLite for the repository work) and leave
`dotnet test` green.
