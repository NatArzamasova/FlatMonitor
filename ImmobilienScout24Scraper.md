# Phase 4: Add ImmobilienScout24 Scraper

## Overview

Implement a second source scraper (`ImmobilienScout24Scraper`) to verify the multi-source architecture works end-to-end. This scraper will fetch apartment listings from immobilienscout24.de and integrate with the existing `ScraperCoordinator` and DI container (Phase 3).

## Scope

1. **New scraper class:** `ImmobilienScout24Scraper` implementing `IScraper`
2. **Source-specific parsers:** HTML parsing logic under `Scraping/ImmobilienScout24/` (mirrors OhneMakler structure)
3. **DI registration:** Register `ImmobilienScout24Scraper` in `Program.cs` alongside `OhneMaklerScraper`
4. **Unit tests:** Core parser tests with fixtures
5. **Smoke test:** Verify both sources scrape and persist to SQLite with correct `Source` attribution

## Architecture (first draft)

```
Scraping/ImmobilienScout24/
├── ImmobilienScout24Scraper.cs          (implements IScraper)
├── ImmobilienScout24ListingsPageFetcher.cs
├── ImmobilienScout24ListingCardParser.cs
├── ImmobilienScout24DetailPageParser.cs
├── ImmobilienScout24Selectors.cs        (XPath/CSS selectors)
└── ImmobilienScout24AddressHelper.cs    (address normalization if needed)

Tests/ImmobilienScout24/
├── ImmobilienScout24ScraperTests.cs
├── ImmobilienScout24ListingCardParserTests.cs
├── ImmobilienScout24DetailPageParserTests.cs
└── Fixtures/
    ├── listings-page-sample.html
    ├── detail-page-sample.html
    └── [other samples as needed]
```

## Decisions to refine (TBD)

1. **Postcode filtering:** OhneMakler filters to Karlsruhe postcodes. Should ImmobilienScout24 use the same filter, or is this configurable per source?
2. **HTML parsing approach:** Reuse `HtmlAgilityPack` and mirror OhneMakler's structure, or use a different approach?
3. **Model mapping:** Keep using the same `Flat` model for both sources, or create ImmobilienScout24-specific DTOs?
4. **Test fixtures:** Use real or mock HTML samples?
5. **Scraping strategy:** Same pagination loop as OhneMakler, or site-specific approach?

## Implementation steps (TBD - order to be confirmed)

1. Research immobilienscout24.de site structure (selectors, pagination, detail pages)
2. Implement `ImmobilienScout24Scraper` with stub parsers
3. Implement parsers incrementally with tests against fixtures
4. Register scraper in DI (`Program.cs`)
5. Run smoke test: `dotnet run -- scrape` with both sources active
6. Verify SQLite: confirm both sources' listings are stored with correct `Source` field
7. Update `multiSourceRefactoring.md` to mark Phase 4 done

## Implementation Progress

### Phase 4a: Browser Automation with Playwright ✅ DONE
- **Commit**: `feat(scraper): implement ImmobilienScout24 browser automation with Playwright`
- **Files created**:
  - `workInProgress/Scraping/ImmobilienScout24/ImmobilienScout24BrowserScraper.cs` — Playwright-based browser automation
  - `workInProgress/Scraping/ImmobilienScout24/ImmobilienScout24LoginManager.cs` — Legacy HTTP-based approach (deprecated)
  - `.env.example` — Credential template
  - `dotnet-tools.json` — Local Playwright CLI tooling
- **Status**: ✅ Working. Successfully logs in via Google OAuth, passes robot guard, extracts page HTML (1.4MB from test run)
- **Test command**: `dotnet run -- explore-is24`

### Phase 4b: Page Structure Analysis & Parser Implementation (IN PROGRESS)
- **HTML analysis**: Completed. Page structure mapped to data-testid selectors.
- **Key findings**:
  - Listings container: `data-testid="ListingsGrid"`
  - Listing card: `class="touchpoint-card"` with `data-testid="touchpoint-hs"`
  - Listing ID: Extracted from `href="/expose/{ID}"` (e.g., `/expose/164855556`)
  - Title: `data-testid="headline"`
  - Price/attributes: `data-testid="bottomBox"` and `data-testid="attributes"`
  - Pagination: `data-testid="pagination-button-next"`, `data-testid="pagination-button-prev"`
  - Detail page URL format: `/expose/{ID}`

## Research Findings

### Authentication & Access
- **Google OAuth required:** immobilienscout24.de uses Google OAuth (not form-based login)
- **Robot detection guard:** Site has anti-bot detection; must authenticate via visible browser
- **Playwright solution:** Browser automation handles auth & session, page HTML extracted for parsing
- **Status**: ✅ Resolved via `ImmobilienScout24BrowserScraper`

### Page Structure (Listings Page)
```
URL: https://www.immobilienscout24.de/Suche/de/baden-wuerttemberg/karlsruhe/wohnung-kaufen

Structure:
├── ListingsGrid (data-testid="ListingsGrid")
│   └── touchpoint-card (class="touchpoint-card") [repeated for each listing]
│       ├── headline (data-testid="headline") → listing title
│       ├── bottomBox (data-testid="bottomBox") → price + key attributes
│       ├── attributes (data-testid="attributes") → rooms, size, location
│       ├── expose link (href="/expose/{ID}") → detail page
│       └── image carousel (data-testid="{ID}-slide-N")
├── Pagination (data-testid="pagination-button-next", etc.)
└── Filters & Sorting
```

### Data Fields Available (Listings Page)
- **Listing ID**: `/expose/(\d+)` — numeric internal ID
- **Title/Description**: From `headline` element
- **Price**: In `bottomBox` (format: "€ XXX.XXX" or similar)
- **Size (m²)**: In `attributes` section
- **Rooms**: In `attributes` section
- **Location/Address**: In card, near headline or in attributes
- **Detail Page URL**: `/expose/{ID}` (relative; full URL: `https://www.immobilienscout24.de/expose/{ID}`)

### Pagination
- **Next Page Button**: `data-testid="pagination-button-next"`
- **Prev Page Button**: `data-testid="pagination-button-prev"`
- **Pattern**: Standard HTML pagination; URL likely updated via JavaScript or form submission

## Next Steps

1. **Phase 4b (Parser Implementation)**:
   - Create `ImmobilienScout24Selectors.cs` with CSS/XPath selectors for each data field
   - Create `ImmobilienScout24ListingCardParser.cs` to extract data from listing card
   - Create `ImmobilienScout24ListingsPageFetcher.cs` to handle pagination
   - Create unit tests with HTML fixtures (save sample listing card HTML)

2. **Phase 4c (Detail Page Parsing)** [Optional if detail page scraping needed]:
   - Navigate to `/expose/{ID}` via Playwright
   - Extract additional fields (description, images, agent info, etc.)
   - Create `ImmobilienScout24DetailPageParser.cs`

3. **Phase 4d (Scraper Integration)**:
   - Create `ImmobilienScout24Scraper` implementing `IScraper`
   - Register in `Program.cs` DI container
   - Run smoke test: `dotnet run -- scrape`
   - Verify both OhneMakler and ImmobilienScout24 listings in SQLite with correct `Source` field

4. **Phase 4e (Documentation)**:
   - Update `multiSourceRefactoring.md` to mark Phase 4 complete
   - Archive this planning document with final findings

## Decisions Resolved

1. **Authentication**: ✅ Decided - Use Playwright browser automation with Google OAuth
2. **Postcode filtering**: ⏳ Deferred - Apply same Karlsruhe filter as OhneMakler for consistency (can make configurable later)
3. **HTML parsing**: ✅ Decided - Use HtmlAgilityPack like OhneMakler; data-testid selectors are reliable
4. **Model mapping**: ✅ Decided - Reuse `Flat` model; `Source` field differentiates listings
5. **Test fixtures**: ✅ Planned - Save real HTML samples from authenticated page

## Questions

*Resolved. Ready to proceed with Phase 4b (parser implementation).*
