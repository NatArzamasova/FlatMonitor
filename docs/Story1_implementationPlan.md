# Story 1: `dotnet run -- scrape` — Implementation Plan

## Implementation Phases

- **Phase 1: Models & Serialization** — ✅ **DONE**
  - `Models/Listing.cs`, `Models/Details.cs`, `Json/JsonOptions.cs`
  - Status: Models created with correct nullable annotations and PascalCase JSON serialization config.

- **Phase 2: HTTP Client & HTML Parser** — ⬜ Not started
  - `Scraping/Selectors.cs`, `ListingsPageFetcher.cs`, `ListingCardParser.cs`, `DetailPageParser.cs`, `ListingScraper.cs`
  - Add HtmlAgilityPack to `workInProgress.csproj`

- **Phase 3: Snapshot Repository** — ⬜ Not started
  - `Storage/SnapshotWriter.cs`

- **Phase 4: Command Entry Point** — ⬜ Not started
  - Rewrite `Program.cs` with command dispatch

- **Phase 5: Tests** — ⬜ Not started
  - New test project `workInProgress.Tests` with NUnit, Moq, FluentAssertions
  - `ListingCardParserTests.cs`, `DetailPageParserTests.cs`, `SnapshotWriterTests.cs`
  - HTML fixtures in `Fixtures/`

---

## Scope of this implementation pass: Phase 1 only

Full context and design for the whole story is kept below for reference. Remaining phases (HTTP client & HTML parser, snapshot repository, command entry point, tests) are implemented in later passes.

## Context

Story 1 (`docs/STORY_1_LOCAL_SCRAPE.md`) is the foundational piece of the OhneMakler Karlsruhe Monitor: it fetches live apartment listings from ohne-makler.net and writes them to an append-only JSONL snapshot, which later stories (Story 2 reporting, Story 3 CI) build on. The project is currently greenfield — `Program.cs` is still the default "Hello, World!" template, there are no models/services, and no NuGet packages are installed. This plan turns the story's open questions (confirmed with the user) and empirically-verified site structure into a concrete file-by-file design.

Confirmed decisions driving this plan:
- **Pagination**: site is paginated and must be fully traversed (confirmed via live fetch: `?page=N` is a real, disjoint-results query param; stop at the first empty page).
- **Detail-page parse failures**: log a warning, still emit the listing with partial/null data — never skip, never fail the whole scrape.
- **Missing fields**: serialize as JSON `null`, not omitted/empty/placeholder.
- **Dates**: scrape UTC timestamp only (confirmed no dates exist anywhere on the site).
- **Rate limiting**: none known — fetch normally/sequentially, revisit only if problems surface.
- **Tests**: per project convention, every story ships with tests by default even though this story's acceptance criteria don't explicitly list them — a test project with fixture-based parser tests is part of this plan, not deferred to later.

## Empirical site findings (verified via live fetch, not assumed)

- Listings page: `https://www.ohne-makler.net/immobilien/wohnung-kaufen/baden-wurttemberg/karlsruhe/`, paginated via `?page=N` (1-indexed). `page=1` and `page=2` return disjoint listing sets (~24 each); the first page with zero listing-card links marks the end. Page-1 HTML also contains a free-text total count ("Hier listen wir 98 Angebote...") usable as a sanity-check log line only.
- Listing cards link to detail pages at `/immobilie/{id}/` and already carry Title, PriceEur, PostalCode, District, RoomsCount, LivingAreaSqm. **Address is only on the detail page.**
- The site is Tailwind-utility-class soup with no semantic selectors — detail-page extraction must use a **label→value lookup** approach (generic, resilient to markup churn) rather than positional/hardcoded XPath per field:
  1. "Key facts" blocks: value `<span>` immediately followed by sibling label `<span>` (confirmed for Zimmer/Wohnfläche).
  2. Table rows: label `<td>` followed by value `<td>` (confirmed for Baujahr, Objektart, Zustand, Energieausweistyp, "Wesentlicher Energieträger" — exact wording matters).
  3. Labels not seen on the one sampled listing (Etage, Bodenbelag, Nebenkosten, Verfügbarkeit, Ansprechpartner) need re-verification against 2-3 real listings during implementation — they may be optional fields (consistent with "missing → null") or use different wording.

## NuGet packages

`workInProgress/workInProgress.csproj`:
- `HtmlAgilityPack` (~1.11.x) — chosen over AngleSharp because the extraction strategy is XPath-driven (`SelectNodes`/`SelectSingleNode`) and the site has no CSS hooks worth AngleSharp's nicer selector API anyway. Also CLAUDE.md's first-named option.
- `System.Text.Json` — no package needed, BCL-provided.
- Test project only: `NUnit`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`, `Moq`, `FluentAssertions` — per CLAUDE.md's documented testing stack.

## Project structure

```
workInProgress/
  workInProgress.csproj              (edit: add HtmlAgilityPack)
  Program.cs                         (rewrite: command dispatch)
  Models/
    Listing.cs
    Details.cs
  Scraping/
    Selectors.cs                     (centralized URLs, XPath, German label constants)
    ListingsPageFetcher.cs           (pagination loop, stops on first empty page)
    ListingCardParser.cs             (parses one results page → partial Listings + detail URLs)
    DetailPageParser.cs              (label→value lookup → Address + Details)
    ListingScraper.cs                (orchestrator: owns HttpClient, ties fetch+parse together)
  Storage/
    SnapshotWriter.cs                (writes data/snapshots/{utc-timestamp}.jsonl, creates dir if missing)
  Json/
    JsonOptions.cs                   (shared JsonSerializerOptions: PascalCase, nulls included)

workInProgress.Tests/                (new test project)
  workInProgress.Tests.csproj        (NUnit + Moq + FluentAssertions, references workInProgress)
  Fixtures/
    listings-page-1.html             (saved from a real scrape run during verification)
    detail-page-sample.html
  ListingCardParserTests.cs
  DetailPageParserTests.cs
  SnapshotWriterTests.cs
```

No `Repositories/`/DI/interfaces — single-command console app, one storage mechanism, no swapping needed. Keep it minimal per CLAUDE.md's no-premature-abstraction guidance.

## Models

```csharp
// Models/Listing.cs
public sealed class Listing
{
    public required string ListingId { get; init; }
    public required string Url { get; init; }
    public string? Title { get; init; }
    public string? Address { get; init; }
    public string? PostalCode { get; init; }
    public string? District { get; init; }
    public int? PriceEur { get; init; }
    public int? RoomsCount { get; init; }
    public int? LivingAreaSqm { get; init; }
    public Details? Details { get; init; }
    public required string ScrapedAtUtc { get; init; }
}

// Models/Details.cs
public sealed class Details
{
    public string? PropertyType { get; init; }
    public int? BuildYear { get; init; }
    public string? Floor { get; init; }
    public string? Condition { get; init; }
    public string? Heating { get; init; }
    public string? Flooring { get; init; }
    public int? ServiceChargeEur { get; init; }
    public string? EnergyCertificateType { get; init; }
    public string? PrimaryEnergySource { get; init; }
    public string? ContactName { get; init; }
    public string? ListingStatus { get; init; }
    public string? CurrentUse { get; init; }
    public string? Availability { get; init; }
}
```

`ListingId`/`Url`/`ScrapedAtUtc` are the only non-nullable fields (always derivable from our own code/regex match, never scraped values that can fail). All numeric/text fields that come from parsing are nullable — an `int` would silently coerce a failed parse to `0`, indistinguishable from a real zero, violating the "null on failure" decision. This is a deliberate, documented deviation from `ohneMaklerMonitor.md`'s literal non-nullable `int` types.

**JSON casing**: PascalCase (matching `ohneMaklerMonitor.md` exactly — `ListingId`, `PriceEur`, etc.), achieved by leaving `PropertyNamingPolicy = null` in `JsonOptions.Snapshot`. Nulls are written (not omitted) — `System.Text.Json` does this by default; `JsonOptions.cs` makes it explicit to guard against future regressions.

## Pagination

`Selectors.PageUrl(n)` → base URL for `n<=1`, else `{base}?page={n}`. `ListingsPageFetcher.FetchAllPagesHtmlAsync()` requests pages sequentially starting at 1, stops the first time a page's listing-card-link count is zero, with a hard safety cap (~50 pages) logged as a warning if hit (defends against a future markup change breaking the "zero means stop" signal). Does not attempt to parse the JS-driven pagination widget — confirmed to carry no usable href/data attributes.

## Error handling

- **Page 1 fetch fails** → fatal. No index, nothing to do. Propagate to `Program.cs`, log, exit non-zero.
- **Later page fetch fails** (page 2+, after page 1 succeeded) → log a warning, stop pagination, proceed with the partial (but real, non-empty) result set rather than crashing — we already have useful data.
- **Detail page fetch/parse fails** → per confirmed answer: log a warning with listing ID/URL + exception, still emit the `Listing` with `Address: null`/`Details: null` (or partially-populated `Details` if some fields parsed before failure). Extract each `Details` field independently (null-safe per field) so one bad label doesn't blow up the other eleven.
- **Exit codes**: `0` = snapshot written (even with some partial/null listings — expected, not a failure). Non-zero = no snapshot could be produced at all (page-1 fetch failed, zero listings found, unhandled exception).

## HttpClient

Single shared instance created once in `Program.cs`, passed into the scraper — avoids socket exhaustion from per-request instantiation. 30s timeout, a normal browser-like `User-Agent` string set defensively (cheap insurance against WAF/CDN blocking of the default .NET UA — not rate-limit handling). Sequential fetching, no artificial delays, per "just fetch normally" — note in code comments that parallelizing detail-page fetches is the natural first optimization if this proves slow, but out of scope now.

## Program.cs dispatch

Plain `args[0]` switch (`"scrape"` → run; anything else → usage/error, exit 1), top-level statements with local functions, consistent with the existing file's style. `report` is not added as a case yet (Story 2). Top-level `try/catch` around the scrape run: success → `return 0`, any exception → log to stderr, `return 1` (satisfies Story 3's CI "fail loudly" requirement).

## Tests (part of this story, not deferred)

New `workInProgress.Tests` project (NUnit + Moq + FluentAssertions, per CLAUDE.md):
- `ListingCardParserTests` — parse a saved results-page HTML fixture, assert correct count and correct field extraction (Title/PriceEur/PostalCode/District/RoomsCount/LivingAreaSqm/detail URL) per card, including a card with a missing/malformed field → null, not an exception.
- `DetailPageParserTests` — parse a saved detail-page HTML fixture, assert Address + each `Details` field extracted correctly via the label→value lookup; assert a label that doesn't appear on the fixture yields `null` for that field without affecting others.
- `SnapshotWriterTests` — given a list of `Listing` objects, assert the JSONL file: has one line per listing, correct PascalCase field names, nulls serialized as JSON `null`, file path matches `data/snapshots/{timestamp}.jsonl`, and `data/snapshots/` is created when missing (use a temp directory, not the real `data/` folder).

Fixtures are the actual HTML captured during manual verification (step 7 below), saved under `workInProgress.Tests/Fixtures/` — this also closes the "fixtures to be collected" note in the story doc.

## Explicitly out of scope

- SQLite / `data/listings.db` / `report` command (Story 2).
- `.gitlab-ci.yml` / CI pipeline (Story 3) — Story 1 only needs to behave well for it (non-interactive, fail-loud exit codes, snapshot files only ever created fresh, never rewritten).
- Retry/backoff/rate-limit handling — revisit only if real-world blocking is observed.

## Verification

1. `dotnet build` — confirms `HtmlAgilityPack` restores and everything compiles on net10.0 (both projects).
2. `dotnet run --project workInProgress -- scrape` against the live site; confirm console shows start/progress/warning/final-count/snapshot-path lines, and exit code is `0`.
3. Inspect the produced `data/snapshots/{timestamp}.jsonl`: correct filename pattern, line count matches logged count, each line valid standalone JSON, exact PascalCase schema match with `ohneMaklerMonitor.md`, genuine `null` (not `""`/`0`) for any unparsed field, single consistent `ScrapedAtUtc` across all lines.
4. Run `scrape` a second time; confirm a new timestamped file appears and the first file is byte-for-byte unchanged.
5. Spot-check 2-3 real listings' `Details` against the live detail pages in a browser to finalize the label map for the not-yet-confirmed fields (Floor/Etage, Flooring/Bodenbelag, ServiceCharge/Nebenkosten, Availability, ContactName/Ansprechpartner).
6. Save the HTML fetched during this manual run as the test fixtures described above; write and run the new test project (`dotnet test`) and confirm all pass.
7. Temporarily point `Selectors.ListingsBaseUrl` at an invalid host, confirm `scrape` exits non-zero with a clear error, then revert.

## Critical files

- `workInProgress/Program.cs`
- `workInProgress/Scraping/Selectors.cs`, `ListingsPageFetcher.cs`, `ListingCardParser.cs`, `DetailPageParser.cs`, `ListingScraper.cs`
- `workInProgress/Storage/SnapshotWriter.cs`
- `workInProgress/Models/Listing.cs`, `Details.cs`
- `workInProgress/Json/JsonOptions.cs`
- `workInProgress/workInProgress.csproj`
- `workInProgress.Tests/*` (new project)
