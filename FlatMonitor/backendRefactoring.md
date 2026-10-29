# Backend Refactoring Suggestions

## Overview

The backend (~4,600 lines of C#, migrations excluded) works, but it has grown in layers. Several
structures were added for one purpose and later partly replaced: the CLI `scrape` path, then the
API background job; two callbacks for batch saving; repositories that own their own `DbContext`.
The result is duplicated wiring, two ways of doing several things, and some real correctness bugs
hidden in the overlap.

This document lists, in order:

1. **Bugs found while reviewing**: fix these first, each with a test.
2. **Structural refactorings**: composition root, persistence, scrape pipeline, job handling.
3. **Source-specific cleanups**: OhneMakler and ImmobilienScout24.
4. **Smaller cleanups**: dead code, magic strings, doc drift.
5. **Suggested order**.

Line references are to the files as of commit `3ac3f09`.

---

## 1. Bugs found during review

### 1.1 Scraper failures are swallowed, so a failed source counts as success ✅ Done
> **Done:** removed the outer `catch` in `FlatScraper.ScrapeAllListingsAsync` (the `finally` → `CleanupAsync` stays);
> the per-flat enrichment `catch` no longer swallows `OperationCanceledException`. Tests: `FlatMonitor.Tests/Scraping/Common/FlatScraperTests.cs`.

**Severity:** High · **Files:** `Scraping/Common/FlatScraper.cs:93-96`, `Api/Services/ScrapeExecutionService.cs:51-57`

`FlatScraper.ScrapeAllListingsAsync` wraps the whole run in `catch (Exception)` and only writes
to the console. Exceptions never reach `ScraperCoordinator`, so `ScraperErrors` stays empty.

Concrete failure: ImmobilienScout24 authentication fails, so `InitializeAsync` throws. The
exception is swallowed and the scraper returns `[]`. The coordinator reports success, so
`ScrapeExecutionService` **deletes the backup** and writes a `"Successful"` history row. The
aggregation for the day is then computed without any IS24 flats. The table was cleared at the
start, so those flats are gone.

The same `catch` also swallows `OperationCanceledException` (from `ct.ThrowIfCancellationRequested()`
at line 59), so cancelling during the last source does not stop the run cleanly.

**Fix:** Remove the outer `catch` (keep the `finally` → `CleanupAsync`) and let the coordinator's
existing per-source `catch` record the error. Add a test: a scraper whose `InitializeAsync`
throws must lead to `Success == false` and keep the backup.

### 1.2 Every flat is saved twice ✅ Done
> **Done (minimal fix):** `ScrapeSourceAsync` skips the final upsert for `FlatScraper`s, whose batches are already
> saved by the callback, and now passes `ct` to the upsert. The structural fix (no callbacks at all) is still 2.3.
> Tests: `ScraperCoordinatorTests.ScrapeAllSourcesAsync_FlatScraper_SavesEachFlatExactlyOnce`; the fake scraper
> moved to `FlatMonitor.Tests/Scraping/Common/FakeFlatScraper.cs` so it can be shared.

**Severity:** Medium · **File:** `Scraping/ScraperCoordinator.cs:96-118`

For `FlatScraper` subclasses (that is, all production scrapers), the batch callback already
filters and upserts every 20 flats. After the scraper returns, `ScrapeSourceAsync` filters the
full list again and calls `UpsertListingsAsync` a second time. The comment says this is a
"fallback for test mocks", but it runs unconditionally. The cost is roughly double the DB work,
and `UpsertListingsAsync` reloads every existing row for the source on each call (see 2.2).

**Fix:** Remove the batch callback concept from the coordinator (see 2.3). If the callback
stays, at minimum skip the second upsert when the scraper is a `FlatScraper`.

### 1.3 Progress counter and result count disagree ✅ Done
> **Done:** progress is now reported by `ScraperCoordinator.SaveAndReportAsync` right after each save, with the
> count after district filtering (also for non-`FlatScraper` sources). `FlatScraper` no longer reports progress, and
> the `onBatchSaved` parameter was removed from `IScraper.ScrapeAsync`. Tests: `ScraperCoordinatorTests.*ReportsSavedCount*`.

**Severity:** Low · **Files:** `Scraping/Common/FlatScraper.cs:84-85`, `Scraping/ScraperCoordinator.cs:54`

`onBatchSaved(pageBuffer.Count)` reports the **unfiltered** batch size, so
`ScrapingHistory.FlatsScrapedCount` counts flats outside Karlsruhe too. `ScrapeResult.FlatsCount`
counts **filtered** flats. The UI shows the first number; the CLI prints the second.

**Fix:** Report the number actually saved (after district filtering).

### 1.4 CLI prints steps before they happen ✅ Done
> **Done:** removed the four `Console.WriteLine` calls; the CLI's console logger shows `ScrapeExecutionService`'s
> step logs as they happen. Added the first tests for `ScrapeExecutionService.ExecuteAsync` (in-memory SQLite):
> step logs in order on success, and a failing source keeps the backup (end-to-end check for 1.1).

**Severity:** Low · **File:** `Program.cs:84-86, 92`

"Backing up…", "Clearing…", and "Refreshing…" are printed before `ExecuteAsync` starts, and
"deleting backup" is printed after it finishes. The service already logs these steps with
`ILogger`. Delete the `Console.WriteLine` calls.

### 1.5 Info logs are invisible in the web server ✅ Done
> **Done:** new `LoggingExtensions.AddFlatMonitorLogging()` used by both the web server and the CLI: `FlatMonitor.*`
> from Information, `Microsoft.*`/`System.*` from Warning. This also stops the CLI from printing EF Core SQL
> commands. Moving the levels to `appsettings.json` is left for 2.1 (needs the content root fixed first).
> Replacing `Console.WriteLine` is still 2.6. Tests: `FlatMonitor.Tests/LoggingExtensionsTests.cs`.

**Severity:** Low · **File:** `Program.cs:184`

The minimum log level is `Warning`, so every `_logger.LogInformation` in `ScrapeBackgroundService`
and `ScrapeExecutionService` is dropped, while the ~110 `Console.WriteLine` calls in scrapers
still print. As a result, logging is effectively controlled by `Console` and not by
configuration (see 2.6).

---

## 2. Structural refactorings

### 2.1 One composition root, configuration instead of hard-coded paths ✅ Done
> **Done:** `FlatMonitor/appsettings.json` (connection string, `Storage:DataDirectory`, log levels) +
> `Storage/StorageOptions` (resolves relative paths against the content root, fails with a clear message if the
> settings are missing). `AddFlatMonitorCore(configuration, contentRoot)` is the single registration for web and
> CLI (CLI now uses `Host.CreateApplicationBuilder` and a proper async scope); `AddHttpClient<OhneMaklerScraper>`;
> scrapers registered as `IEnumerable<IScraper>`. `Directory.SetCurrentDirectory` and `StoragePaths` are gone;
> `RunWorkingDirectory` makes `dotnet run` use the project directory. Scrapers take `StorageOptions` instead of the
> unused repository; repositories no longer fall back to the real DB file. Migrations run in one helper at startup
> (the repositories' own `InitializeAsync` is still there; removed in 2.2). Not done: `ICommand` classes (optional).
> Tests: `AppSettingsTests`, `StorageOptionsTests`, `ServiceRegistrationExtensionsTests`. Verified by starting
> the new build on another port against the existing `FlatMonitor/data/listings.db` (577 offers returned).
**Severity:** High · **Impact:** Maintainability, correctness when adding services
**Files:** `Program.cs`, `ServiceRegistrationExtensions.cs`, `Storage/StoragePaths.cs`, `Storage/FlatsDbContextFactory.cs`

Current problems:
- The web server registers every scrape service by hand (`Program.cs:192-225`). This copies
  `ServiceRegistrationExtensions.AddScrapeServices` line for line. Adding a scraper means
  editing both places.
- The DB path is derived in **four** places and two different ways: `Program.cs:56` and `:189`
  use `CWD/FlatMonitor/data/listings.db`, while `StoragePaths` (used by the repositories' default
  constructors and the design-time factory) uses `AppContext.BaseDirectory/../../../data`. These
  only match because `Program.cs:18-20` first changes the working directory to
  `bin/Debug/net10.0/../../../..`. Running from `bin/Release/net10.0/publish` or a container breaks this.
- `HttpClient` is registered as a hand-built singleton twice.

**Suggested fix:**
```csharp
// ServiceRegistrationExtensions.cs
public static IServiceCollection AddFlatMonitorCore(this IServiceCollection services, IConfiguration config)
{
    services.AddDbContext<FlatsDbContext>(o => o.UseSqlite(config.GetConnectionString("Flats")));
    services.AddHttpClient<OhneMaklerScraper>(c =>
    {
        c.Timeout = TimeSpan.FromSeconds(30);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 ...");
    });
    services.AddScoped<IScraper, OhneMaklerScraper>();
    services.AddScoped<IScraper, ImmobilienScout24Scraper>();   // coordinator takes IEnumerable<IScraper>
    services.AddScoped<ISnapshotRepository, FlatSnapshotRepository>();
    // ...
    return services;
}
```
- Use `Host.CreateApplicationBuilder` for the CLI and `WebApplication.CreateBuilder` for the
  server. Both call `AddFlatMonitorCore(builder.Configuration)`.
- Put `ConnectionStrings:Flats` and `DataDirectory` in `appsettings.json` (relative to the
  content root), with `.env`/environment variables overriding them. Remove
  `Directory.SetCurrentDirectory` and `StoragePaths`'s `../../..`.
- Run migrations in exactly one place, at startup (see 2.2).
- Optionally move the CLI commands (`scrape`, `explore-is24`) to small `ICommand` classes, so
  `Program.cs` only parses arguments and dispatches.

### 2.2 Repositories: inject the `DbContext`, stop owning it ✅ Done
> **Done:** `FlatSnapshotRepository`/`FlatAggregationRepository` take the scoped `FlatsDbContext` (registered by type);
> `InitializeAsync`, `_ownsContext` and `IAsyncDisposable` are gone from the repositories, their interfaces and
> `ScraperCoordinator`. New `ISnapshotRepository.ClearAsync`, so `ScrapeExecutionService` no longer depends on
> `FlatsDbContext`. Backup/restore/delete-backup are one `ExecuteSqlRawAsync` each and errors propagate (also in
> the upsert). `GetCurrentOffersAsync` passes `ct`; `ScrapingHistoryRepository.GetLatestAsync` drops the projections.
> Because the background service now shares the context with the repositories, it clears the change tracker before
> saving a Failed/Canceled history row (otherwise unsaved flats/aggregations would be written with it).
> Not done here: moving restore-on-failure into `ScrapeExecutionService` (2.5).
> Tests: `FlatSnapshotRepositoryTests` (`ClearAsync`, `DeleteBackupAsync`, restore without backup, canceled offers query),
> `ServiceRegistrationExtensionsTests.AddFlatMonitorCore_RepositoriesUseTheScopedDbContext`,
> `ScrapeBackgroundServiceTests.ProcessJob_WhenScrapeFailsWithUnsavedChanges_SavesFailedHistoryWithoutThem`,
> `ScrapeExecutionServiceExecuteTests.ExecuteAsync_BacksUpThenClearsThroughRepositoryBeforeScraping`.

**Severity:** High · **Impact:** Testability, transactions, hidden dependencies
**Files:** `Storage/FlatSnapshotRepository.cs:11-41`, `Storage/FlatAggregationRepository.cs:12-36`, `Storage/I*Repository.cs`

`FlatSnapshotRepository` and `FlatAggregationRepository` create their own `FlatsDbContext` from
options. They fall back to a hard-coded SQLite file when no options are given, so
`new FlatSnapshotRepository()` silently opens the real DB. `ScrapingHistoryRepository` and
`FilterPresetRepository` already inject the scoped context. As a result, one scrape uses **three**
contexts and connections: the `ExecuteDeleteAsync` in `ScrapeExecutionService:38` runs on a
different context than the backup and the upserts. No transaction can span them.

**Suggested fix:**
- Every repository takes `FlatsDbContext` in the constructor, like `FilterPresetRepository`.
  Drop `_ownsContext`, `GetDefaultConnectionString`, and `IAsyncDisposable` on the interfaces.
- Remove `InitializeAsync` from `ISnapshotRepository` and `IFlatAggregationRepository`. Its
  `ProviderName.Contains("InMemory")` check is test logic in production code. Tests call
  `EnsureCreated` in their own setup, and production migrates once at startup.
- Move "clear table" into the repository (`ClearAsync`) so `ScrapeExecutionService` stops
  depending on `FlatsDbContext` directly.
- Backup/restore/delete-backup (`:43-101`) each open the connection manually, never close it,
  and dispose the command by hand. Use `Database.ExecuteSqlRawAsync(...)` (one line each). Also
  replace the `Console.Error` + rethrow with plain propagation.
- `GetCurrentOffersAsync` ignores its `ct` (`:216`).
- `ScrapingHistoryRepository.GetLatestAsync` repeats the same projection twice. Since the rows
  are plain entities, `AsNoTracking().OrderByDescending(...).FirstOrDefaultAsync()` needs no
  projection at all.

### 2.3 Simplify the scrape pipeline: scrapers produce, the coordinator persists ✅ Done
> **Done:** `IScraper.ScrapeAsync` returns `IAsyncEnumerable<Flat>`; `FlatScraper` yields each enriched flat (one entry
> point, `ScrapeAllListingsAsync`, `SaveListingsCallback` and the `allFlats` list are gone). `ScraperCoordinator` uses
> `.Chunk(20)`, filters, upserts, reports, and appends to the JSONL snapshot as it goes (`SnapshotWriter.Create` →
> `SnapshotFile`), so no source is held in memory. It returns a `ScrapeSummary` (flats per source incl. partial counts of
> failed sources, errors); `ScraperErrors`, `SetOnBatchSavedCallback` and `ScrapeResult` are gone.
> **Deviation:** progress is a `Func<int, Task>` parameter, not `IProgress<int>`: the background service writes the history
> row on the shared `DbContext` per batch, which must be awaited (`Progress<T>` would run it concurrently on the thread pool).
> Tests: `FlatScraperTests` (lazy yield, early stop cleans up), `ScraperCoordinatorTests` (rewritten with a stub
> `IScraper`: batching, save-before-next-batch, partial failure, cancellation, streamed snapshot), `ScrapeSummaryTests`,
> `ScrapeExecutionServiceExecuteTests.ExecuteAsync_ReportsEachSavedBatchToTheCallback`.

**Severity:** High · **Impact:** Understandability; this is the root cause of bugs 1.1 to 1.3
**Files:** `Scraping/IScraper.cs`, `Scraping/Common/FlatScraper.cs`, `Scraping/ScraperCoordinator.cs`

Batching currently involves **two** callbacks:
- `FlatScraper.SaveListingsCallback`: a mutable property that the coordinator sets via a type check (`scraper is FlatScraper`).
- `onBatchSaved`: a `Func<int, Task>` passed through `ScrapeExecutionService` → `SetOnBatchSavedCallback` (mutable state on a scoped service) → `IScraper.ScrapeAsync`.

In addition, the scraper builds an `allFlats` list to return anyway, so the "avoid memory bloat"
goal is not met. Every flat is held in memory until the end.

**Suggested shape:**
```csharp
public interface IScraper
{
    string Source { get; }
    IAsyncEnumerable<Flat> ScrapeAsync(CancellationToken ct);   // yields enriched flats one by one
}
```
```csharp
// ScraperCoordinator
await foreach (var batch in scraper.ScrapeAsync(ct).Chunk(20))   // System.Linq.AsyncEnumerable in .NET 10
{
    var filtered = _districtFilter.Apply(batch);
    await _repository.UpsertAsync(filtered, ct);
    progress.Report(filtered.Count);                              // IProgress<int> instead of Func<int,Task>
    // optional: append to the JSONL snapshot writer here, streaming
}
```
Benefits:
- Scrapers know nothing about persistence, filtering, or batching. They become easier to test
  with fixtures (assert on yielded flats).
- No type check, no mutable callback properties, no double save.
- Errors propagate naturally (fixes 1.1).
- `ScraperCoordinator` returns a `ScrapeSummary` (counts per source, errors) instead of exposing
  a mutable `ScraperErrors` list that must be read after the call.

`FlatScraper` can remain as a template base class (`InitializeAsync` → pages → enrich →
`CleanupAsync`), but it should only `yield return` enriched flats.

### 2.4 Extract district normalization into one place ✅ Done
> **Done:** `KarlsruheDistricts.Match` (exact via `ToCanonical`, then contained district; the longest wins, so
> "KA-Nordweststadt" no longer depends on the set's iteration order to beat "Weststadt") replaces the coordinator's
> `NormalizeDistrict`. `Flat` is a `record`; `CreateFlatWithAdjustedDistrict` and the manual copies in both scrapers' `EnrichListingWithDetailsAsync`
> are `with` expressions (same output: the card fields they used to drop are never set by the card parsers). OhneMakler computes the
> absolute URL once, and its enrichment `catch` no longer swallows `OperationCanceledException`. FILTERING.md updated.
> Tests: `KarlsruheDistrictsTests.Match_*`, `ScraperCoordinatorTests.ScrapeAllSourcesAsync_NormalizingTheDistrict_KeepsEveryOtherField`,
> `OhneMaklerScraperEnrichmentTests` (whole scraper against the fixtures via a fake HTTP handler, incl. a failing detail page).

**Severity:** Medium · **Files:** `Scraping/ScraperCoordinator.cs:140-181`, `Models/KarlsruheDistricts.cs`

- `NormalizeDistrict` (exact, then substring match) lives in the coordinator. `ToCanonical`
  (exact only) lives in `KarlsruheDistricts`. Put both in `KarlsruheDistricts` (for example
  `ToCanonical` and `Match`) or in a small `DistrictFilter` class, and unit test it directly.
  `FILTERING.md` already documents the rules.
- `CreateFlatWithAdjustedDistrict` copies 16 properties by hand and silently drops new ones. Make
  `Flat` a `record` (or keep the class with `init` setters) and use `flat with { District = normalized }`.
  The same pattern removes the manual copies in `OhneMaklerScraper.EnrichListingWithDetailsAsync`
  (2 copies) and `ImmobilienScout24Scraper.EnrichListingWithDetailsAsync`.

### 2.5 Scrape jobs: one source of truth, no polling
**Severity:** Medium · **Files:** `Api/Services/ScrapeJobService.cs`, `ScrapeBackgroundService.cs`, `ScrapeStateService.cs`, `ScrapingCounterService.cs`, `Api/Controllers/ScrapeController.cs`

- `ScrapeStateService` and `ScrapingCounterService` are **dead code**: they are injected into
  `ScrapeBackgroundService` but never used. Delete them (and the mock in `ScrapeControllerTests`).
- `ScrapeBackgroundService.ExecuteAsync` polls every second. Replace the `ConcurrentQueue` +
  `Task.Delay` with a `Channel<ScrapeJob>` (`await reader.ReadAsync(ct)`).
- `++_jobCounter` is not thread-safe (use `Interlocked.Increment`), and `_jobTracker` grows
  forever. Keep only the last N jobs, or derive status from `ScrapingHistories`, which already
  persists every run.
- `POST /api/scrape` queues a new job each time it is called. Clicking twice runs two full scrapes back to
  back, and the second one clears the table the first one just filled. Return `409 Conflict`
  (or the existing job id) while a job is queued or running.
- The controller depends on the concrete `ScrapeBackgroundService` to cancel. Move
  `CancelCurrentJob` to a small `IScrapeJobQueue` / `ScrapeJobService` that both the controller
  and the hosted service use. The hosted service should not need to be registered twice
  (`AddSingleton` + `AddHostedService(sp => ...)`).
- `ProcessJobAsync` contains three nearly identical blocks for history updates and restores
  (`:96-158`). Extract them into `CompleteHistoryAsync(history, status, error)` and
  `TryRestoreBackupAsync()`. The CLI path (`Program.cs:107-120`) restores the backup in its own
  way. Move the restore-on-failure logic **into** `ScrapeExecutionService` so both entry points
  share it:

```csharp
public async Task<ScrapeResult> ExecuteAsync(IProgress<int>? progress, CancellationToken ct)
{
    await _snapshots.BackupAsync(ct);
    try
    {
        await _snapshots.ClearAsync(ct);
        var summary = await _coordinator.ScrapeAllSourcesAsync(progress, ct);
        await _aggregations.RefreshAsync(ct);
        if (summary.Success) await _snapshots.DeleteBackupAsync(ct);
        return summary;
    }
    catch
    {
        await _snapshots.RestoreAsync(CancellationToken.None);
        throw;
    }
}
```
- Also consider: the `PartialFailed` case keeps the backup but leaves the partially filled table
  live. Decide explicitly whether a partial failure should restore the failed source's rows (this
  is possible per source once the coordinator works per source).

### 2.6 Replace `Console.WriteLine` with `ILogger<T>`
**Severity:** Medium · **Impact:** Configurable verbosity, structured logs, testable output

There are about 110 `Console.Write*` calls, mostly in `Program.cs` (33), the IS24 scraper files (45), and
`ScraperCoordinator` (7). Some are debug-level noise, for example IS24 prints the first 200
characters of every card's HTML (`ImmobilienScout24Scraper.cs:210`). Inject `ILogger<T>` into
the scrapers, coordinator, and repositories, and use levels (`Debug` for per-card output,
`Information` for per-page output, `Warning` for skipped pages). `FlatScraperLogger` becomes
`LoggerMessage` source-generated methods or simply disappears. `DEBUG=1` stack-trace checks
(`Program.cs:46`, `ScraperCoordinator.cs:136`) become `_logger.LogError(ex, ...)`.

Keep `Console` only for the CLI's final user-facing summary.

### 2.7 API: centralize error handling and the response envelope
**Severity:** Low–Medium · **Files:** `Api/Controllers/FlatsController.cs`, `FilterPresetsController.cs`

Every action repeats `try { ... } catch (Exception ex) { return StatusCode(500, new ApiResponse { Message = ex.Message }) }`.
This also leaks exception messages (paths, SQL) to the client.

- Add `app.UseExceptionHandler()` + `AddProblemDetails()` (or a small middleware that maps
  unhandled exceptions to `ApiResponse { Success = false }` with a generic message and logs the
  details). Then remove the try/catch blocks from controllers.
- `FilterPresetsController` already has `Success<T>`/`Failure<T>`/`ServerError<T>` helpers.
  Move them to a shared `ApiControllerBase` (or extension methods) so `FlatsController`,
  `DistrictsController`, and `ScrapeController` use the same helpers. `ScrapeController` currently
  returns anonymous `{ message }` objects instead of `ApiResponse`.
- `FlatsController` is a thin wrapper around two repositories. It could be split by resource
  (`/api/flats/offers`, `/api/aggregations`, `/api/aggregations/history`) but that changes the
  frontend contract, so it is optional.
- Pass `HttpContext.RequestAborted` (an action parameter `CancellationToken ct`) through to the
  repositories.
- `TriggerScrapeAsync` is not async. Rename it to `TriggerScrape`. It returns `ScrapeResponse` with
  `FlatsCount = 0` and `Success = true`; `ScrapeQueuedResponse` already exists for this case.
  Check whether that contract is unused.

### 2.8 Domain model cleanup
**Severity:** Low–Medium · **Files:** `Models/*`

- `Flat.Source` defaults to `"OhneMakler"` (`Flat.cs:17`). A flat without an explicit source
  becomes OhneMakler. Make it `required`.
- `CommissionPercent` exists on both `Flat` and `FlatDetails`. IS24 sets both, OhneMakler sets only
  `Details.CommissionPercent = 0`, and `FlatSnapshot` keeps only the `Details` copy. Keep one.
- `FlatSnapshot` drops `Title`; `UpsertListingsAsync` only updates part of the fields (no
  `Title`, no `CommissionPercent`). Consider a single mapping method `FlatSnapshot.From(Flat)` /
  `snapshot.UpdateFrom(Flat)` and use it for both insert and update.
- `ScrapedAtUtc`, `CreatedAtUtc`, and `UpdatedAtUtc` are stored as `string` (`"O"` format).
  Use `DateTime` (EF/SQLite stores it as ISO text anyway), which removes string-based ordering assumptions.
- `ScrapingHistory.Status` is a magic string (`"Running"`, `"Successful"`, `"PartialFailed"`, ...),
  compared by literal in `ScrapingHistoryRepository.cs:30`. Use an enum with
  `HasConversion<string>()`, so the DB values stay the same.
- `FlatAggregation` (API DTO) has `PostalCode` and `Availability`, which are always `null`
  (`FlatAggregationRepository.GetLatestAsync`). Drop them once the frontend no longer reads them.
- `PricePerMeter` is computed in `FlatSnapshotRepository.CalculatePricePerMeter`. That is domain
  logic in the storage layer. Compute it once when the `Flat` is built (or as a computed property).
- Design note: the table is cleared before every scrape, so the "upsert" is effectively an
  insert and `CreatedAtUtc` always equals the scrape time. Either simplify to insert-only and
  drop the audit columns, or (more useful for the "new/removed/days-on-market" goals in
  `WohnungMonitor.md`) stop clearing the table, upsert for real, and track `FirstSeenUtc` /
  `LastSeenUtc` / `IsActive`. The second option would also make the backup/restore table unnecessary.

### 2.9 Upsert performance
**Severity:** Low (currently) · **File:** `Storage/FlatSnapshotRepository.cs:107-113`

Each batch of 20 loads **all** existing rows for the source into the change tracker, and the tracker keeps them across
batches. With ~1,000 flats, that is about 50 full-source loads. Load only the keys in the batch
(`Where(s => s.Source == src && ids.Contains(s.ListingId))`) and call
`ChangeTracker.Clear()` after each save. This becomes moot if 2.8's design note goes
insert-only.

---

## 3. Source-specific cleanups

### 3.1 Shared: remove unused repository dependency from scrapers
`OhneMaklerScraper(HttpClient, ISnapshotRepository? = null)` and
`ImmobilienScout24Scraper(ISnapshotRepository? = null)` store `_repository` and never use it.
The `?? new FlatSnapshotRepository()` default also opens a second DB context per scraper. Remove the
parameter.

### 3.2 Shared: HTML snapshot storage
`ClearPreviousSnapshotsAsync` is copied verbatim in both scrapers (only the folder name differs),
and the detail HTML saving logic is duplicated (`OhneMaklerScraper.SaveDetailHtmlSnapshotAsync`,
`ImmobilienScout24DetailScraper` constructor + `FetchAndParseDetailPageAsync`), including the
`ExtractListingIdFromUrl` helper. Extract them into an `HtmlSnapshotStore(source)` service with
`ClearAsync()` and `SaveAsync(listingId, html)`, registered in DI with the configured data
directory.

Also, `OhneMaklerScraper.cs:200` saves through a fire-and-forget `Task.Run`, so exceptions are
lost and writes can race with the next run's `Clear`. Await the write, or do not save at all
unless a debug flag is set.

### 3.3 OhneMakler
- `EnrichListingWithDetailsAsync` builds the `Flat` twice (success and failure branches) and
  computes the absolute URL three times (`:129`, `:156`, `:191`). Compute it once in the card
  parser (store absolute URLs from the start), and use `with` expressions.
- The detail page HTML is parsed into an `HtmlDocument` **five** times per flat:
  `ParseDetailFields`, `ExtractPrice`, and the three `Extract*FromDetailPage` helpers. Parse once and
  move title, address, rooms, and area extraction into `OhneMaklerDetailPageParser`, so it returns a
  single result (`OhneMaklerDetailPage { Details, Title, Address, Rooms, Area, Price }`). That
  makes all of it testable against the fixtures already in `FlatMonitor.Tests/Fixtures/OhneMakler`.
- `ExtractAreaFromDetailPage` uses `//*[contains(., 'm²')]`, which matches `<html>` first and
  relies on the first regex hit across the whole page. Add a fixture test before touching it.
- The postcode stop condition in `FetchAndParseAllPagesAsync` belongs to the page fetcher or a
  named policy. It is the reason pagination stops, which is not obvious in the scraper.

### 3.4 ImmobilienScout24
- `EnrichListingWithDetailsAsync` → `FetchAndParseDetailPageAsync` parses the details, and the result
  is only used as a null check. Then `GetPageAsync` fetches the HTML again, and `ParseDetailFields`,
  `ExtractExposeMetadata`, and `ExtractOriginalPrice` each parse it again. `ParseDetailFields` also calls
  `ExtractExposeMetadata` internally. That makes four or more parses per flat. Have the detail scraper return the
  HTML (`Task<string?> FetchDetailHtmlAsync(url)`) and have the parser return one combined result.
- `ImmobilienScout24DetailScraper.ExtractAddressFromCurrentPageAsync` is unused.
- `ImmobilienScout24BrowserScraper`: the `IPlaywright` instance is never disposed. `Headless = false`
  and the search URL are hard-coded, so move them to configuration (`Scrapers:ImmobilienScout24:Headless`, `SearchUrl`).
  It returns `(null, null)` on error instead of throwing. Let it throw; the caller converts the null result into an exception anyway.
- `ImmobilienScout24FlatsPageFetcher` implements `DisposeAsync`, which closes the page and browser
  that the scraper also closes in `CleanupAsync`. Nobody calls it, so drop it. Its `_isLastPage`
  field can be a local variable. The `_browser` field is unused.
- Fixed delays (`Task.Delay(2000)`, `Task.Delay(1000)`) and debug dumps to `%TEMP%` when the page
  is < 10 KB: replace the delays with `WaitForSelectorAsync` on the listing cards, and put the dumps behind a debug option.
- The fallback card selector `//div[contains(@class, 'card')]` (`:197`) matches almost anything.
  Remove it or log a warning and return nothing.

### 3.5 Generic extraction helpers
- `FlatExtractor` is only used by IS24, and its `"@data-obid"` special case (`:45`) is IS24-specific.
  Either move it into the IS24 folder, or give `IFlatSelectors` an `IdAttribute` property.
- `Regex` objects are created inline in `FlatExtractor`, `DetailExtractors`, and `OhneMaklerScraper`.
  Use `[GeneratedRegex]` static partial methods.
- `ParsePrice`/`ParseSize`/`ParseRooms`/`ExtractIntField` are German number parsing spread over
  three classes. Consolidate them into one `GermanNumberParser` with unit tests ("1.250.000 €", "84,5 m²", "3,5 Zimmer").
  Note: `ParseRooms` truncates 3.5 rooms to 3, while `Flat.RoomsCount` is a `double`.
- `IDetailParser` is only used by tests (`BeAssignableTo<IDetailParser>`). Either use it
  polymorphically or drop it.

---

## 4. Smaller cleanups

- `Scraping/Debug/SnapshotLoader.cs` is used only by its tests. Move it to the test project or delete it.
- `FlatsDbContext.cs` has a duplicated `using FlatMonitor.Models;` and a no-op alias
  `using ScrapingHistory = ScrapingHistory;`. The comment "flattened into the OhneMaklerSnapshots
  table" is outdated (the table is `FlatsSnapshots`). Consider one `IEntityTypeConfiguration<T>` class per
  entity instead of a 190-line `OnModelCreating`.
- `JsonOptions` comment references `ohneMaklerMonitor.md`, which does not exist.
- `FlatScraper` doc comment typos ("falts"), and `ScrapeAsync` vs. `ScrapeAllListingsAsync` are two
  public entry points for the same thing. Keep one.
- `ScrapeExecutionService:39` calls `SaveChangesAsync` after `ExecuteDeleteAsync`, which does
  nothing.
- **Doc drift:** `CLAUDE.md`, `AGENTS.md`, and `WohnungMonitor.md` describe `data/flats.db` and a
  `report` command. The code uses `FlatMonitor/data/listings.db` and has no `report` command (reporting is the web API).
  Update the docs together with 2.1.

---

## 5. Suggested order

Each step is independently shippable and keeps `dotnet test` green.

| # | Step | Why first | Risk |
|---|------|-----------|------|
| 1 | ✅ Fix 1.1 (swallowed exceptions) + test | Data loss risk today | Low |
| 2 | Delete dead code: `ScrapeStateService`, `ScrapingCounterService`, unused scraper repo params, `ExtractAddressFromCurrentPageAsync`, no-op `SaveChanges` | Shrinks the surface before restructuring | Low |
| 3 | ✅ 2.1 composition root + configuration (single DI registration, connection string, `AddHttpClient`) | Every later step touches DI | Medium |
| 4 | 2.2 repositories inject `FlatsDbContext`; move clear/restore into the repository; restore logic into `ScrapeExecutionService` | Enables transactions and simpler tests | Medium |
| 5 | 2.3 + 2.4 streaming `IScraper`, coordinator owns filtering/persistence, `Flat` as record | Fixes 1.2/1.3 structurally | Medium–High (touches both scrapers) |
| 6 | 2.6 `ILogger` everywhere | Mechanical once DI is clean | Low |
| 7 | 2.5 job queue via `Channel`, conflict on double trigger | Independent | Low |
| 8 | 3.x per-source parser consolidation (single parse, `HtmlSnapshotStore`) | Needs fixture tests first | Medium |
| 9 | 2.7 API error handling, 2.8 model cleanups (enum status, `DateTime`, single `CommissionPercent`) | 2.8 needs a migration; coordinate with the frontend | Medium |
| 10 | Decide 2.8's design note (clear-and-insert vs. real upsert with first/last seen) | Product decision; unlocks "removed"/"days on market" | High (design) |
