# District Filtering Strategy

## Overview

All flats scraped are filtered to match `KarlsruheDistricts.All` (27 Karlsruhe districts) before saving to the database. Filtering ensures only valid Karlsruhe flats reach the canonical SQLite database.

## KarlsruheDistricts List

Located in `Models/KarlsruheDistricts.cs` (used by `ScraperCoordinator.cs`):

```csharp
"Beiertheim-Bulach", "Daxlanden", "Durlach", "Grötzingen", "Grünwettersbach",
"Grünwinkel", "Hagsfeld", "Hohenwettersbach", "Innenstadt-Ost", "Innenstadt-West",
"Knielingen", "Mühlburg", "Neureut", "Nordstadt", "Nordweststadt", "Oberreut",
"Oststadt", "Palmbach", "Rintheim", "Rüppurr", "Stupferich", "Südstadt",
"Südweststadt", "Waldstadt", "Weiherfeld-Dammerstock", "Weststadt", "Wolfartsweier"
```

## Normalization Strategy

`NormalizeDistrict(string? district)` handles district name variations:

1. **Exact match (case-insensitive)**: `"Oberreut"` → `"Oberreut"`
2. **Substring match (case-insensitive)**: `"KA-Oberreut"` contains `"Oberreut"` → `"Oberreut"`
3. **No match**: Returns `null` → flat is filtered out

### Examples

| Input | Output | Reason |
|-------|--------|--------|
| `"Oberreut"` | `"Oberreut"` | Exact match |
| `"oberreut"` | `"Oberreut"` | Case-insensitive match |
| `"KA-Oberreut"` | `"Oberreut"` | Substring: "Oberreut" is in "KA-Oberreut" |
| `"ka-innenstadt-west"` | `"Innenstadt-West"` | Case-insensitive substring |
| `"Bad Schönborn"` | `null` (filtered) | Not in KarlsruheDistricts |
| `null` | `null` (filtered) | Empty/null district |

## Filtering Flow

### Production Scrapers (FlatScraper)

1. **During scraping**: `ScraperCoordinator.SetupBatchSaveCallback()` configures a callback
2. **Every 20 flats**: `FlatScraper` calls the callback with a batch
3. **In callback**: `FilterAndNormalizeBatch()` filters and normalizes, then saves to database

**Benefit**: Flats filtered **before** database insertion. Memory efficient (batches of 20).

### Fallback (Test Mocks / Non-FlatScraper)

If scraper doesn't use batch callback:

1. **After scraping**: `ScrapeSourceAsync()` calls `FilterAndNormalizeBatch()` on all listings
2. **Fallback save**: Filtered flats saved to database

**Note**: Mocks and tests use this path. Less memory efficient but correct.

## Database Guarantee

✓ **Only normalized, allowed-district flats reach the database**
- No `District IS NULL` rows
- No out-of-area districts (Bad Schönborn, Bruchsal, etc.)
- All districts match `KarlsruheDistricts.All` exactly

## Testing

See `ScraperCoordinatorTests.cs` for 9 district filtering tests:
- Exact match (case-sensitive and insensitive)
- Substring match ("KA-" prefix handling)
- No match filtering
- Null/empty district filtering
- Mixed valid/invalid in one batch

Run: `dotnet test --filter "ScrapeAllSourcesAsync_FilterByDistrict"`
