namespace FlatMonitor.Storage;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;

public sealed class FilterPresetRepository : IFilterPresetRepository
{
    private readonly FlatsDbContext _dbContext;

    public FilterPresetRepository(FlatsDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<FilterPreset>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.FilterPresets
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<FilterPreset?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _dbContext.FilterPresets
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, int? excludingId = null, CancellationToken cancellationToken = default)
    {
        // Name uses NOCASE collation, so this comparison ignores case in SQLite.
        return _dbContext.FilterPresets
            .AnyAsync(p => p.Name == name && (excludingId == null || p.Id != excludingId), cancellationToken);
    }

    public async Task<FilterPreset> CreateAsync(
        string name,
        IReadOnlyList<string> districts,
        CancellationToken cancellationToken = default)
    {
        var preset = new FilterPreset
        {
            Name = name,
            Districts = [.. districts],
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.FilterPresets.Add(preset);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return preset;
    }

    public async Task<FilterPreset?> UpdateAsync(
        int id,
        string name,
        IReadOnlyList<string> districts,
        CancellationToken cancellationToken = default)
    {
        var preset = await _dbContext.FilterPresets.FindAsync([id], cancellationToken);
        if (preset == null)
            return null;

        preset.Name = name;
        preset.Districts = [.. districts];
        preset.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return preset;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var preset = await _dbContext.FilterPresets.FindAsync([id], cancellationToken);
        if (preset == null)
            return false;

        _dbContext.FilterPresets.Remove(preset);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<FilterPreset?> SetDefaultAsync(int id, CancellationToken cancellationToken = default)
    {
        var preset = await _dbContext.FilterPresets.FindAsync([id], cancellationToken);
        if (preset == null || preset.IsDefault)
            return preset;

        // Two SaveChanges in one transaction: EF does not guarantee the order of UPDATEs within one
        // SaveChanges, and setting the new default before clearing the old one breaks the unique index.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await ClearDefaultWithoutTransactionAsync(cancellationToken);

        preset.IsDefault = true;
        preset.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return preset;
    }

    public Task ClearDefaultAsync(CancellationToken cancellationToken = default)
    {
        return ClearDefaultWithoutTransactionAsync(cancellationToken);
    }

    private async Task ClearDefaultWithoutTransactionAsync(CancellationToken cancellationToken)
    {
        var defaults = await _dbContext.FilterPresets
            .Where(p => p.IsDefault)
            .ToListAsync(cancellationToken);
        if (defaults.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var preset in defaults)
        {
            preset.IsDefault = false;
            preset.UpdatedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
