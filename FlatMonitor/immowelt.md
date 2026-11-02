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

**A browser — and it has to be a real Chrome the user started, attached over CDP.** Checked on 2026-11-02:

| Client | Result |
|---|---|
| `curl`, no User-Agent / Chrome User-Agent + full browser headers / home page | `403`, 771-byte DataDome page |
| Playwright's bundled Chromium, headless | `403`, DataDome page, no listings after 2 min |
| Playwright's bundled Chromium, headed | hard block ("Etwas im Verhalten des Browsers hat uns stutzig gemacht"), no captcha offered |
| User's Chrome (`--remote-debugging-port=9222`, own profile), robot check passed once by hand, Playwright attached with `ConnectOverCDPAsync` | **`200`** for list page 1, page 2 (by URL, `&page=2`) and an expose (by URL), 5 s apart |

**DataDome** (`geo.captcha-delivery.com`) guards the whole site. It recognizes Playwright's own browser (automation
flags) even headed, so the IS24 setup (`Chromium.LaunchAsync`) does not work here; `HttpClient` (OhneMakler) neither.

Consequences:
- The scraper attaches to `http://127.0.0.1:9222` (not `localhost`: resolves to IPv6 `::1`, Chrome listens on IPv4
  only), opens its own tab, and closes only that tab. The user's Chrome must be running, with the robot check passed;
  otherwise the source fails at start and `ScraperCoordinator` records it as a source error — the other sources still
  run and immowelt keeps its previous rows. A scrape started from the web server (`POST /api/scrape`) behaves the same.
- Pages are opened **by URL** (`GotoAsync`), list and expose alike. No clicking needed: the expose opened by URL
  passed DataDome (as IS24's expose pages, which `ImmobilienScout24DetailScraper` also opens by URL). If longer runs
  get blocked, clicking the card (with the search as referrer) is the next thing to try.
- Pause between pages (5 s in the spike; configurable). Opening pages back to back is what DataDome calls
  "übermenschliche Geschwindigkeit". ~9 list pages + ~270 exposes ≈ 25 min per run.
- No further evasion (fingerprint spoofing, captcha services, proxies). If DataDome blocks the attached Chrome during
  normal runs, drop immowelt as a source rather than escalate.

Starting Chrome (PowerShell); recent Chrome refuses remote debugging on the default profile, hence the own one:

```powershell
& "C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222 --user-data-dir="$env:LOCALAPPDATA\FlatMonitorChrome"
```

> **Note:** immowelt's `robots.txt` has `Disallow: */classified-search?*` for all user agents (expose pages are
> allowed). The scraper is for personal use at low volume: sequential requests with a pause.

### Do we need the detail pages?

**Yes.** The list data covers price, rooms, area, district and postal code, but the commission is only reliable on the
expose: the list says `tags.hasBrokerageFee: false` for every flat on pages 1–2, while the explored expose shows
"Provision für Käufer: 3,57% des Kaufpreises". Build year, heating, energy, service charge and condition are only on
the expose too.

## Findings (step 1, 2026-11-02)

Explored pages: `FlatMonitor/data/explore/Immowelt/list_page1_20261102_092004.html`, `list_page2_20261102_092011.html`,
`expose_20261102_092021.html`.

**Parse embedded JSON, not the markup.** Both page types carry their data in `<script>` blobs of the form
`window["NAME"]=JSON.parse("…escaped JSON…")`:

### List page: `window["__UFRN_FETCHER__"]`

- `data["classified-serp-init-data"]` is a string **compressed with lz-string** (`LZString.compressToBase64`,
  starts with `N4Ig…`). Decompressed it is JSON; everything below is under `pageProps`.
  (`__UFRN_LIFECYCLE_SERVERREQUEST__` on the list page only holds the user's IP and user agent.)
- `totalCount` (`"268"`), `page` (`"1"`); **30 flats per page** → 9 pages. Pagination: `&page=N` in the URL.
- `classifieds`: the 30 IDs of the page, in order. `suggestedClassifieds` (3 per page): **suggestions outside the
  search — leave out.** `classifiedsData` holds both (33 entries), keyed by ID.
- Pages 1 and 2: 60 distinct IDs, no overlap.
- Per flat (`classifiedsData[id]`):

| Field | Example | → `Flat` |
|---|---|---|
| `id` | `26AQWAY4363S` ("Online-ID" on the expose) | `ListingId` |
| `url` | `https://www.immowelt.de/expose/c79373b8-…` (= `metadata.legacyId`) | `Url` |
| `rawData.price` | `449000` (int; missing for some listings) | `PriceEur` |
| `hardFacts.price.previousPrice` | `"475.000 €"` (only if reduced) | `OriginalPriceEur` |
| `rawData.surface.main` | `118.09` | `LivingAreaSqm` |
| `rawData.nbroom` | `2` | `RoomsCount` |
| `location.address.zipCode` / `.district` | `76189` / `Daxlanden` | `PostalCode` / `District` |
| `mainDescription.headline` (or `hardFacts.title`) | "Mediterranes Flair im Herzen von Daxlanden…" | `Title` |
| `rawData.distributionType` | `BUY`, `COMPULSORY_AUCTION` | auction filter |
| `metadata.creationDate` | `2026-10-30T13:47:07Z` | (not stored yet) |

  Districts on pages 1–2 are Karlsruhe district names (Südweststadt, Durlach, Grötzingen, Mühlburg, Oberreut, …)
  → `ScraperCoordinator`'s normalization should recognize them as they are; check in the smoke run.
  `location.isAddressPublished` is `false` for most: no street address.

### Expose page: `window["__UFRN_LIFECYCLE_SERVERREQUEST__"]`

- Plain JSON (no lz-string): `app_demand_referral_cldp.data.classified`, with `sections`:

| Path | Example | → `Flat` / `FlatDetails` |
|---|---|---|
| `sections.price.base.main.value.main.ariaLabel` | `"305000 €"` | `PriceEur` |
| `sections.price.base.commissionFee.value` | `"3,57% des Kaufpreises"` | `CommissionPercent` |
| `sections.price.additional[].details[]` label `Hausgeld` | `"346 €"` | `ServiceChargeEur` |
| `sections.energy.features[]` type `yearOfConstruction` | `"1995"` | `BuildYear` |
| `sections.energy.features[]` type `state` | `"Neubau, neuwertig"` | `Condition` |
| `sections.energy.features[]` type `heatingSystem` | `"Zentralheizung"` | `Heating` |
| `sections.energy.certificates[0].features[]` type `basedOn` | `"Verbrauchsausweis"` | `EnergyCertificateType` |
| `sections.energy.certificates[0].features[]` type `primaryEnergySource` | `"Erdgas H"` | `PrimaryEnergySource` |
| `sections.energy.certificates[0].scales[0].efficiencyClass.rating` | `"A"` | `EnergyEfficiencyClass` |
| `sections.energy.certificates[0].scales[0].values[0].value` | `"40,8 kWh/(m²·a)"` | `EnergyConsumption` |
| `sections.hardFacts.facts[]` type `numberOfFloors` | `"1. Geschoss"` | `Floor` |
| `sections.hardFacts.facts[]` types `numberOfRooms` / `livingSpace` | `"3"` / `"75"` | rooms / area |
| `sections.location.address` | zip, district | as on the list |
| `title`, `rawData.distributionType`, `metadata.legacyId` | | |

  Also there: `application/ld+json` (not needed). Missing: a removed listing's expose (to save when one turns up,
  for `IsDeactivated`).

### New-build projects (found in the first smoke run, 2026-11-02)

The units of a new-build project are separate cards (own ID, price, area), but their `url` is the shared project
page (`/projekte/expose/{projectId}`), and so is their address: the second unit broke the unique index
(Source, Url, Address). Each unit still has its own expose, `/expose/{metadata.legacyId}` — for a single flat that is
exactly its `url` (all 30 cards of the fixture) — so the list parser uses that for project units. The project page
itself (`rawData.propertyType: PROJECT`, price "auf Anfrage", `units[]` with each unit's own `url`) is no longer opened.

### Auctions

Pages 1–2: 59 × `BUY`, 1 × `COMPULSORY_AUCTION` (with a price, 331,000 €). One `BUY` listing has no `rawData.price`.
Some listings publish a street (`location.address.street`, e.g. "Yorckstraße 49").

## Open questions

1. **Auctions.** An auction's "price" is a market value or minimum bid, which distorts the €/m² aggregations.
   Options: (a) drop them from the URL (`distributionTypes=Buy`), (b) keep them and leave out flats whose
   `rawData.distributionType` is not `BUY`. **Proposal: (a)**, plus (b) as a safety net in the parser.
2. **Duplicates across sources.** Many flats are listed on both immowelt and IS24 (same agent). The aggregations
   would count them twice. Out of scope here; note it for later (match by district + area + price).
3. ~~**lz-string in C#**~~ — ported (`LzString.cs`), tested with vectors from the JavaScript library lz-string 1.5.0
   and against the fixture.

## Architecture

```
Scraping/Immowelt/
├── ImmoweltOptions.cs                (CdpUrl, SearchUrl, PauseBetweenPagesMs, SaveDebugPages)
├── ImmoweltBrowser.cs                (attach over CDP, open a tab; disposing closes only the tab)
├── ImmoweltScraper.cs                (FlatScraper; Session over one tab)
├── ImmoweltPages.cs                  (SourceName, PageUrl(searchUrl, n), ReadDataScript(html, name))
├── ImmoweltJson.cs                   (property-path helpers, address formatting)
├── LzString.cs                       (DecompressFromBase64)
├── ImmoweltFlatsPageFetcher.cs       (GotoAsync(PageUrl(n)) for n = 2.., pause, yields HTML)
├── ImmoweltListParser.cs             (list page HTML → totalCount + List<Flat>, suggestions and auctions left out)
├── ImmoweltDetailScraper.cs          (open expose, pause, save to HtmlPageArchive, return HTML)
├── ImmoweltDetailParser.cs           (expose HTML → ImmoweltDetailPage with ApplyTo(card), IsDeactivated)
├── ImmoweltExplorer.cs               (exists: explore-immowelt)
└── ImmoweltPageInspection.cs         (exists)

FlatMonitor.Tests/Immowelt/
├── LzStringTests.cs
├── ImmoweltPagesTests.cs
├── ImmoweltListParserTests.cs
├── ImmoweltDetailParserTests.cs
├── ImmoweltOptionsTests.cs
├── ImmoweltScraperLiveTests.cs       ([Explicit], [Category("Live")])
└── Fixtures/ (the explored list page and expose)
```

The planned extraction of a shared `Common/PlaywrightBrowser` from `ImmobilienScout24Browser` is **dropped**: immowelt
attaches to a running Chrome instead of launching one, so there is little to share.

## Implementation steps

1. ✅ **Spike: `explore-immowelt` command** — `dotnet run -- explore-immowelt [--headless | --cdp]`; findings above.
2. ✅ **Fixtures** — `FlatMonitor.Tests/Fixtures/Immowelt/`: `list-page.html` (search page 2, only the
   `__UFRN_FETCHER__` script, verbatim: 137 KB) and `expose-page.html` (the data script trimmed to the sections the
   parser reads: 8 KB).
3. ✅ **`LzString` + `ImmoweltPages`** — decompression and blob reading, tested against the fixtures; `PageUrl(n)`
   adds/replaces `page=n`.
4. ✅ **`ImmoweltListParser`** — fixture-tested: total count, 30 flats, ID, URL, title, price, original price, rooms,
   area, postal code, district; suggestions and non-`BUY` flats left out. A flat that cannot be read → skipped and
   logged.
5. ✅ **`ImmoweltOptions` + `ImmoweltBrowser`** — `Scrapers:Immowelt` section in `appsettings.json` (`CdpUrl`,
   `SearchUrl` with `distributionTypes=Buy`, `PauseBetweenPagesMs`, `SaveDebugPages`). No Chrome at the CDP URL →
   clear error ("start Chrome with --remote-debugging-port=9222 …"). `explore-immowelt --cdp` attaches through
   `ImmoweltBrowser` too.
6. ✅ **`ImmoweltFlatsPageFetcher` + `ImmoweltScraper.FetchCardsAsync`** — page 1, then pages 2..⌈totalCount/30⌉ by
   URL with the pause, safety cap 50 pages. DataDome page or no blob on page 1 → throw (with debug page); on a later
   page → throw too (no silently shorter run). `ImmoweltScraper.EnrichAsync` returns the card until step 7.
7. ✅ **`ImmoweltDetailParser` + `ImmoweltDetailScraper` + `EnrichAsync`** — expose is the source of truth, card
   fills gaps (like the other sources); rooms and area come from the card (exact; the expose rounds them).
   `IsDeactivated` = `metadata.status.status: false` — an assumption, verify with a removed listing's expose. Commission via `GermanNumberParser` ("provisionsfrei"/missing fee → 0 only if the
   page says so, else `null`). Removed expose (HTTP 404/410 or `IsDeactivated`) → `null` (left out). Each expose
   after the pause, saved to `HtmlPageArchive`. Expose without data → throws (`FlatScraper` keeps the card); the
   DataDome guard → that card and all further ones kept without opening their exposes (one warning).
   `ImmoweltDetailParser.Parse` returns `null` for a page without data, as the list parser does.
8. ✅ **Registration** — `ServiceRegistrationExtensions`: options singleton, `AddScoped<ImmoweltScraper>()`,
   `AddScoped<IScraper>(...)`; extend `ServiceRegistrationExtensionsTests`.
9. **Live test + smoke run** — `ImmoweltScraperLiveTests` (`[Explicit]`, `[Category("Live")]`) reads page 1 and one
   expose from the attached Chrome; then `dotnet run -- scrape --source Immowelt` (only immowelt; the other sources
   keep their flats, `Scrapers:Sources` selects sources for the web server too) and check in SQLite: rows with `Source = 'Immowelt'`,
   districts recognized, €/m² plausible, no auctions.
10. **Docs** — `CLAUDE.md` / `WohnungMonitor.md` (third source, CDP Chrome, DataDome), `BUILD_AND_RUN.md`
    (starting Chrome, `Scrapers:Immowelt` settings, `explore-immowelt`).
