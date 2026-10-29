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

`KarlsruheDistricts.Match(string? scrapedName)` handles district name variations:

1. **Exact match (case-insensitive, trimmed)**: `"Oberreut"` → `"Oberreut"`
2. **Substring match (case-insensitive)**: `"KA-Oberreut"` contains `"Oberreut"` → `"Oberreut"`.
   If several districts are contained, the **longest** wins: `"KA-Nordweststadt"` contains both
   `"Nordweststadt"` and `"Weststadt"` → `"Nordweststadt"`
3. **No match**: Returns `null` → flat is filtered out

User input (filter presets) uses `KarlsruheDistricts.ToCanonical`, which only does the exact match.

### Examples

| Input | Output | Reason |
|-------|--------|--------|
| `"Oberreut"` | `"Oberreut"` | Exact match |
| `"oberreut"` | `"Oberreut"` | Case-insensitive match |
| `"KA-Oberreut"` | `"Oberreut"` | Substring: "Oberreut" is in "KA-Oberreut" |
| `"ka-innenstadt-west"` | `"Innenstadt-West"` | Case-insensitive substring |
| `"KA-Nordweststadt"` | `"Nordweststadt"` | Longest contained district (not "Weststadt") |
| `"Bad Schönborn"` | `null` (filtered) | Not in KarlsruheDistricts |
| `null` | `null` (filtered) | Empty/null district |

## Filtering Flow

Scrapers yield unfiltered flats one by one (`IScraper.ScrapeAsync` returns `IAsyncEnumerable<Flat>`). `ScraperCoordinator`:

1. Takes them in batches of 20
2. Keeps flats whose district `KarlsruheDistricts.Match` recognizes, as `flat with { District = <canonical name> }`
3. Saves the filtered batch to the database, then asks the scraper for the next flats

**Benefit**: Flats are filtered **before** database insertion, and no source is held in memory as a whole.

## Database Guarantee

✓ **Only normalized, allowed-district flats reach the database**
- No `District IS NULL` rows
- No out-of-area districts (Bad Schönborn, Bruchsal, etc.)
- All districts match `KarlsruheDistricts.All` exactly

## Testing

- `KarlsruheDistrictsTests.cs`: `Match` rules (exact, substring, longest match, untracked/blank names)
- `ScraperCoordinatorTests.cs`: filtering during a scrape (normalized district, every other field kept, filtered-out flats, mixed batches)

Run: `dotnet test --filter "FullyQualifiedName~KarlsruheDistrictsTests|FullyQualifiedName~District"`
