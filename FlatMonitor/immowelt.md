# Immowelt Scraper (third source)

## Goal

Add `ImmoweltScraper` as the third `IScraper`, next to `OhneMaklerScraper` and `ImmobilienScout24Scraper`. It reads
the Karlsruhe apartment-for-sale search, follows its pagination, enriches each flat from its expose page and yields
unfiltered `Flat`s with `Source = "Immowelt"`. Filtering, batching and saving stay in `ScraperCoordinator`.

Search URL (first page):

```
https://www.immowelt.de/classified-search?distributionTypes=Buy,Buy_Auction,Compulsory_Auction&estateTypes=Apartment&locations=AD08DE5585
```

## Decision: HttpClient (like OhneMakler) or browser (like ImmobilienScout24)?

**Browser (Playwright).** Checked on 2026-11-02 with `curl`:

| Request | Result |
|---|---|
| Search URL, no User-Agent | `403`, 771 bytes |
| Search URL, Chrome User-Agent + full browser headers (`Accept`, `Sec-Fetch-*`, `sec-ch-ua`, ...) | `403`, 771 bytes |
| `https://www.immowelt.de/` (home page) | `403`, 771 bytes |

The 771-byte body is a **DataDome** challenge (`geo.captcha-delivery.com`, "Please enable JS and disable any ad
blocker"). DataDome guards the whole site, so the expose pages are blocked to `HttpClient` the same way: the
OhneMakler approach does not fit, neither for the list nor for the detail pages. Faking headers or replaying DataDome
cookies in `HttpClient` is brittle (cookies expire, TLS fingerprinting) and is not pursued.

Consequence: one browser session per run, reused for the list pages **and** the detail pages — the IS24 pattern.
Unlike IS24 there is no login: only the DataDome check, which a real (headed) browser usually passes without a
captcha. Whether **headless** passes too is open (see step 1); default is headed, like IS24.

> **Note:** immowelt's `robots.txt` has `Disallow: */classified-search?*` for all user agents (expose pages are
> allowed). The scraper is for personal use at low volume; keep requests sequential with a delay (see below).

### Do we need the detail pages at all?

The card is probably enough for the core fields (price, rooms, area, district/postal code), but `FlatDetails`
(build year, heating, energy, floor, service charge) and the **commission** are only on the expose page, and the
other two sources fill them. So: fetch details, as the other sources do. Since the browser is already open, this costs
only time (~N pages × a few seconds). If a run turns out too slow or triggers captchas, an option
`FetchDetails = false` keeps the card data only (step 7, optional).

## What the site likely looks like (to verify in step 1)

Not verifiable with `curl` (blocked); from knowledge of the current immowelt (AVIV) frontend:

- **Server-rendered data blob.** List and expose pages embed their data as JSON in a script
  (`window["__UFRN_LIFECYCLE_SERVERREQUEST__"] = JSON.parse("...")` or similar). If present, **parse that JSON
  instead of the DOM**: stable field names, no CSS class churn, exact numbers. DOM selectors are the fallback.
- **Cards:** `data-testid="serp-core-classified-card-testid"` (or similar), link to `/expose/{id}` where `id` is an
  alphanumeric code (e.g. `2bz3k5x`) — that becomes `ListingId`.
- **Card fields:** price (`"349.000 €"`), rooms (`"3 Zimmer"`), area (`"78 m²"`), location
  (`"Südstadt, Karlsruhe (76137)"`) → `District`, `PostalCode`.
- **Pagination:** `&page=N` in the URL. Navigating by URL is simpler and more robust than clicking "next" (the IS24
  approach). The total count on page 1 ("123 Angebote") gives the page count; stop at the first page without cards
  anyway.
- **Expose:** price, Käuferprovision (commission), Baujahr, Heizung, Energieträger, Effizienzklasse, Etage, Hausgeld,
  Zustand, Verfügbarkeit.

## Open questions

1. **Auctions.** The search includes `Buy_Auction` and `Compulsory_Auction`. An auction's "price" is a market value
   or minimum bid, which distorts the €/m² aggregations. Options: (a) drop them from the URL (`distributionTypes=Buy`),
   (b) keep them, mark them (`FlatDetails.ListingStatus = "Zwangsversteigerung"`) and leave them out of aggregations.
   **Proposal: (a)**, the simplest — the other sources do not include auctions either.
2. **Duplicates across sources.** Many flats are listed on both immowelt and IS24 (same agent). The aggregations
   would count them twice. Out of scope here; note it for later (match by address + area + price).
3. **Headless.** Default headed like IS24; switch to headless if step 1 shows DataDome lets it through.

## Architecture

```
Scraping/Common/
└── PlaywrightBrowser.cs              (NEW, extracted from ImmobilienScout24Browser: launch, open URL, wait for selector)

Scraping/Immowelt/
├── ImmoweltOptions.cs                (Headless, SearchUrl, SaveDebugPages, DelayBetweenPagesMs)
├── ImmoweltScraper.cs                (FlatScraper; Session over one browser)
├── ImmoweltSelectors.cs              (card selector, wait selectors, PageUrl(searchUrl, n), ExposeUrl(id))
├── ImmoweltFlatsPageFetcher.cs       (GotoAsync(PageUrl(n)) for n = 2.., yields HTML)
├── ImmoweltCardParser.cs             (list page HTML → List<Flat>)
├── ImmoweltDetailScraper.cs          (open expose, wait, save to HtmlPageArchive, return HTML)
└── ImmoweltDetailParser.cs           (expose HTML → detail record with ApplyTo(card), IsDeactivated)

FlatMonitor.Tests/Immowelt/
├── ImmoweltCardParserTests.cs
├── ImmoweltDetailParserTests.cs
├── ImmoweltSelectorsTests.cs         (PageUrl / ExposeUrl)
├── ImmoweltScraperLiveTests.cs       ([Explicit], [Category("Live")])
└── Fixtures/ (saved list page, expose page, expose of a removed listing)
```

Shared code: `ImmobilienScout24Browser` / `ImmobilienScout24BrowserSession` contain nothing IS24-specific except
the options type and the card selector. Extract them into `Common/PlaywrightBrowser` (parameters: headless, URL,
wait selector, source name for logging) and let both scrapers use it, instead of copying ~100 lines. If the refactor
turns out larger than expected, copy first and extract later.

## Implementation steps

1. **Spike: `explore-immowelt` command** — like `explore-is24`: opens the search URL in Playwright (headed), saves
   page 1, page 2 and one expose page as HTML. Answers: does the browser pass DataDome (headed / headless)? Is there a
   JSON blob? Card selector, ID format, location format, pagination parameter, total count. Save the pages as test
   fixtures. Write the findings into this file ("Findings" section).
2. **Common browser** — extract `PlaywrightBrowser` from `ImmobilienScout24Browser`; IS24 tests stay green.
3. **`ImmoweltOptions` + `ImmoweltSelectors`** — `Scrapers:Immowelt` section in `appsettings.json`
   (`Headless`, `SearchUrl`, `SaveDebugPages`); `PageUrl(n)` adds/replaces `page=n`. Tests for the URL building and
   options reading (like `ImmobilienScout24Options`).
4. **`ImmoweltCardParser`** — fixture-tested: ID, URL, title, price, rooms, area, postal code, district (raw; the
   coordinator normalizes). Unreadable card → skipped and logged, like IS24.
5. **`ImmoweltFlatsPageFetcher` + `ImmoweltScraper.FetchCardsAsync`** — page 1 from the browser start, then pages 2..N
   by URL, a short delay between pages, safety cap 50 pages. No cards on page 1 → throw with the saved debug page
   (DataDome or changed markup); no cards on a later page → pagination complete. A DataDome page mid-run throws (no
   silently shorter run).
6. **`ImmoweltDetailParser` + `ImmoweltDetailScraper` + `EnrichAsync`** — expose page is the source of truth, card
   fills gaps (like the other sources). Commission from "Käuferprovision" (`GermanNumberParser`, "provisionsfrei" →
   0). `FlatDetails` fields as far as available. A removed/deactivated expose → `null` (left out). Reuse
   `DetailExtractors` where the label/value shape matches.
7. *(optional)* **`FetchDetails` option** — if the full run is too slow or hits captchas.
8. **Registration** — `ServiceRegistrationExtensions`: options singleton, `AddScoped<ImmoweltScraper>()`,
   `AddScoped<IScraper>(...)`; extend `ServiceRegistrationExtensionsTests`.
9. **Live test + smoke run** — `ImmoweltScraperLiveTests` (`[Explicit]`, `[Category("Live")]`) reads page 1 and one
   expose; then `dotnet run -- scrape` and check in SQLite: rows with `Source = 'Immowelt'`, districts recognized,
   €/m² plausible, no auction prices.
10. **Docs** — `CLAUDE.md` / `WohnungMonitor.md` (third source, browser, DataDome), `BUILD_AND_RUN.md`
    (`Scrapers:Immowelt` settings, `explore-immowelt`).

## Findings (filled in by step 1)

- DataDome headed: ?
- DataDome headless: ?
- JSON blob on list / expose page: ?
- Card selector / ID format: ?
- Location format (district, postal code): ?
- Pagination parameter, flats per page, total count: ?
- Expose fields available: ?
