# What is next

As of 2026-11-02, after the Immowelt source (third source), the pipeline coverage jobs and the GitLab stages.
In order of priority.

## 1. Deduplicate flats across sources (next)

Many flats are listed on several portals by the same agent. In the first full Immowelt run, **188 of 246 Immowelt
flats** matched an ImmobilienScout24 flat in the same district with the same price and an area within 1 m²; 16 also
matched an OhneMakler flat. The €/m² aggregations (`FlatAggregations`) count each of them once per source, so districts
with many agent listings weigh more. Every scrape adds to the skew.

Steps:
1. **Measure the match** before designing anything: how many hits with district + price + area (± 1 m²), how many of
   them are wrong (different flats that happen to match), how many duplicates are missed (e.g. a price changed on one
   portal only). Check whether address, rooms or postal code make the match more reliable.
2. **Decide the design**, with the user:
   - where to deduplicate: when the aggregations are computed (rows stay per source) or when flats are saved
   - which source wins when the data differs (price, details)
   - whether the dashboard shows it (e.g. "also on IS24")
3. Implement with tests, recompute today's aggregations, compare before/after per district.

**Done (2026-11-03), a first version:** deduplicated when the aggregations are computed; the `FlatsSnapshots` rows stay
per source. `FlatAggregationCalculator` groups the flats by normalized title (HTML entities, dashes, punctuation, case
and spaces removed) + postal code; flats of different sources in a group are one flat, flats of the same source stay
different. A group keeps the flats of the source with the most of them (tie: first source by name), which decides the
district and rooms group.

Measured on the database of 2026-11-03 (789 flats with price and area): 236 cross-source pairs with the same
normalized title, none of them more than 1 m² or a postal code apart, one 6 % apart in price (a price change on one
portal). 18 of them are in different districts (IS24 "Innenstadt-West" / Immowelt "Weststadt"), which a district key
would miss. Missed: about 45 pairs that match on postal code, area ± 1 m² and price ± 3 % but have different titles
(reworded or cut off on one portal, mostly OhneMakler). Not used: rooms (the portals count them differently).

Still open: recompute and compare today's aggregations per district, the dashboard hint ("also on IS24"); IS24 titles
are saved with HTML entities (`&amp;`), which the parser could decode.

Background: open question 2 in [FlatMonitor/immowelt.md](FlatMonitor/immowelt.md); design note "Cross-source
duplicates" in [CLAUDE.md](CLAUDE.md).

## 2. Check the first pipeline run with coverage

After the next push, on GitLab and GitHub:
- `backend-coverage` and `frontend-coverage` succeed (no tests run there; they read the test jobs' artifacts)
- GitLab shows the line coverage on the jobs, the pipeline and the MR, and covered lines in the MR diff
- GitHub shows the coverage table in the coverage jobs' summary, and the `…-coverage-report` artifacts open
- GitLab draws three columns: build, test, coverage

Locally both chains passed (backend 87.5 % lines, frontend 99.2 %); the real pipelines are not run yet.
See [BUILD_AND_RUN.md](BUILD_AND_RUN.md#ci-pipelines).

## 3. Small leftovers from the Immowelt work

- **Live test** (step 9 of [FlatMonitor/immowelt.md](FlatMonitor/immowelt.md)): `ImmoweltScraperLiveTests`,
  `[Explicit]` + `[Category("Live")]`, reads results page 1 and one expose from the debug Chrome. Warns early when
  immowelt changes its pages.
- **3 s pause:** the default `Scrapers:Immowelt:PauseBetweenPagesMs` is now 3000 (was 5000, which passed a full run).
  The first 3 s run looked fine; watch a few more runs for "DataDome blocks the exposes". If it appears, go back to 5000.
- **CLI summary:** `dotnet run -- scrape` prints "✓ Partially scraped: 0 flats collected (with errors)" when the only
  source failed. Show ✗ when no flats were saved (`Program.cs`, `RunScrapeAsync`).
- **Deactivated listings:** `ImmoweltDetailParser` takes `metadata.status.status: false` as "no longer published". An
  assumption: verify with a removed listing's expose when one turns up, and save it as a fixture.

## 4. Clean up WohnungMonitor.md

Outdated beyond Immowelt: the ImmobilienScout24 section still says "TBD", and the schema sections ("Database schema",
"Refactoring for Multi-Source Support") describe tables that no longer exist (`Flats`, `Snapshots`, `Statistics`).
Align it with the current model (`FlatsSnapshots`, `FlatsSnapshots_Staging`, `FlatAggregations`, `ScrapingHistories`)
or cut the historical parts. Misleading for anyone reading it, including a future Claude session.
