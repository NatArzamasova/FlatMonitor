# Story 1a: `dotnet run -- scrape` — Write Snapshots to SQLite

## Goal

After Story 1 completes and snapshots are written to `data/snapshots/{timestamp}.jsonl`, **Story 1a loads those snapshots into SQLite for fast querying by Story 2 (`report` command).**

The SQLite database (`data/listings.db`) is:
- **Derived** from snapshots (can be deleted and rebuilt)
- **Git-ignored** (regenerated on each run, not committed)
- **Used only for queries** (Story 2 `report` command)

## Implementation Plan

### Phase 1: SQLite Schema & Repository

Create `Storage/ListingsRepository.cs`:

```csharp
public sealed class ListingsRepository : IAsyncDisposable
{
    private const string DbPath = "listings.db";
    private readonly SqliteConnection _connection;

    public ListingsRepository(string? baseDirectory = null)
    {
        baseDirectory ??= "data";
        var dbPath = Path.Combine(baseDirectory, DbPath);
        var connectionString = $"Data Source={dbPath}";
        _connection = new SqliteConnection(connectionString);
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Listings (
                ListingId TEXT PRIMARY KEY,
                Url TEXT NOT NULL,
                Title TEXT,
                Address TEXT,
                PostalCode TEXT,
                District TEXT,
                PriceEur INTEGER,
                RoomsCount INTEGER,
                LivingAreaSqm INTEGER,
                PropertyType TEXT,
                BuildYear INTEGER,
                Floor TEXT,
                Condition TEXT,
                Heating TEXT,
                Flooring TEXT,
                ServiceChargeEur INTEGER,
                EnergyCertificateType TEXT,
                PrimaryEnergySource TEXT,
                ContactName TEXT,
                ListingStatus TEXT,
                CurrentUse TEXT,
                Availability TEXT,
                ScrapedAtUtc TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            
            CREATE INDEX IF NOT EXISTS idx_ScrapedAtUtc ON Listings(ScrapedAtUtc);
            CREATE INDEX IF NOT EXISTS idx_PostalCode ON Listings(PostalCode);
            CREATE INDEX IF NOT EXISTS idx_District ON Listings(District);
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpsertListingsAsync(List<Listing> listings)
    {
        using var transaction = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        
        cmd.CommandText = @"
            INSERT INTO Listings (ListingId, Url, Title, Address, PostalCode, District, 
                PriceEur, RoomsCount, LivingAreaSqm, PropertyType, BuildYear, Floor, 
                Condition, Heating, Flooring, ServiceChargeEur, EnergyCertificateType, 
                PrimaryEnergySource, ContactName, ListingStatus, CurrentUse, Availability,
                ScrapedAtUtc, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@ListingId, @Url, @Title, @Address, @PostalCode, @District,
                @PriceEur, @RoomsCount, @LivingAreaSqm, @PropertyType, @BuildYear, @Floor,
                @Condition, @Heating, @Flooring, @ServiceChargeEur, @EnergyCertificateType,
                @PrimaryEnergySource, @ContactName, @ListingStatus, @CurrentUse, @Availability,
                @ScrapedAtUtc, @CreatedAtUtc, @UpdatedAtUtc)
            ON CONFLICT(ListingId) DO UPDATE SET
                Url = @Url, Title = @Title, Address = @Address, PostalCode = @PostalCode, District = @District,
                PriceEur = @PriceEur, RoomsCount = @RoomsCount, LivingAreaSqm = @LivingAreaSqm,
                PropertyType = @PropertyType, BuildYear = @BuildYear, Floor = @Floor,
                Condition = @Condition, Heating = @Heating, Flooring = @Flooring,
                ServiceChargeEur = @ServiceChargeEur, EnergyCertificateType = @EnergyCertificateType,
                PrimaryEnergySource = @PrimaryEnergySource, ContactName = @ContactName,
                ListingStatus = @ListingStatus, CurrentUse = @CurrentUse, Availability = @Availability,
                ScrapedAtUtc = @ScrapedAtUtc, UpdatedAtUtc = @UpdatedAtUtc;
        ";

        var now = DateTime.UtcNow.ToString("O");
        foreach (var listing in listings)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@ListingId", listing.ListingId);
            cmd.Parameters.AddWithValue("@Url", listing.Url ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Title", listing.Title ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Address", listing.Details?.Address ?? (object)DBNull.Value);
            // ... all other parameters
            cmd.Parameters.AddWithValue("@ScrapedAtUtc", listing.ScrapedAtUtc);
            cmd.Parameters.AddWithValue("@CreatedAtUtc", now);
            cmd.Parameters.AddWithValue("@UpdatedAtUtc", now);
            
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
```

### Phase 2: Load Snapshots into DB

Create `Storage/SnapshotLoader.cs`:

```csharp
public sealed class SnapshotLoader
{
    public List<Listing> LoadAllSnapshots(string? baseDirectory = null)
    {
        baseDirectory ??= "data";
        var snapshotsDir = Path.Combine(baseDirectory, "snapshots");
        var listings = new List<Listing>();

        if (!Directory.Exists(snapshotsDir))
            return listings;

        var snapshotFiles = Directory.GetFiles(snapshotsDir, "*.jsonl")
            .OrderBy(f => f); // Load in chronological order

        foreach (var file in snapshotFiles)
        {
            var lines = File.ReadAllLines(file);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var listing = JsonSerializer.Deserialize<Listing>(line, JsonOptions.Snapshot);
                if (listing != null)
                    listings.Add(listing);
            }
        }

        return listings;
    }
}
```

### Phase 3: Integrate into Scraper

Modify `ListingScraper.cs` to write to DB after snapshot:

```csharp
public async Task RunAsync()
{
    var listings = await FetchAndParseAsync();
    
    // Write snapshot
    var snapshotPath = await _snapshotWriter.WriteSnapshotAsync(listings);
    Console.WriteLine($"Snapshot written: {snapshotPath}");
    
    // Write to SQLite
    using (var repo = new ListingsRepository())
    {
        await repo.InitializeAsync();
        await repo.UpsertListingsAsync(listings);
    }
    
    Console.WriteLine($"Database updated with {listings.Count} listings");
}
```

### Phase 4: Tests for Repository & Loader

`RepositoryTests.cs`:
- Test upsert creates new rows
- Test upsert updates existing listings (by ListingId)
- Test schema exists after initialization
- Test indexes are created

`SnapshotLoaderTests.cs`:
- Test loading empty snapshots directory returns empty list
- Test loading single snapshot file
- Test loading multiple snapshot files in order
- Test malformed JSONL lines are skipped

## Dependencies

Add to `Directory.Packages.props`:
```xml
<PackageVersion Include="System.Data.SQLite" Version="1.0.118.0" />
```

Or use `Microsoft.Data.Sqlite` (lighter, no native DLL):
```xml
<PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.0" />
```

## Notes

- **Idempotent**: Snapshots are the source of truth; DB is rebuilt by upsert on each run
- **No deletes**: Old listings stay in DB (for history/reporting); Story 2 will mark them as "removed" by comparing today vs yesterday
- **Timestamps**: `CreatedAtUtc` set once, `UpdatedAtUtc` updated on upsert, `ScrapedAtUtc` from snapshot

## Out of Scope (Story 2)

- Reporting/aggregation queries
- "New" vs "Removed" vs "Price Changed" logic
- Daily comparisons

