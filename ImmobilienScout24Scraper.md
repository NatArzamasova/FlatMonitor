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

## Research Findings

### Access Requirements
- **User account required:** immobilienscout24.de requires a user account (username/password) to scrape listings
- **Robot detection guard:** The site has an anti-bot detection mechanism that blocks automated requests
- **Browser authentication flow:** Must authenticate via browser first (user will handle), then scraper can use session/cookies to make requests

### Next Steps
1. User opens browser and authenticates to immobilienscout24.de (bypassing robot guard)
2. Extract session cookies or auth tokens from browser
3. Use authenticated session in `ImmobilienScout24Scraper` to make requests
4. Research HTML structure of listings page once authenticated access is available

## Questions

*To be asked one-by-one after approval of this draft.*
