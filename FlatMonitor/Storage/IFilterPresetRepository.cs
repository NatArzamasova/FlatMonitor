namespace FlatMonitor.Storage;

using FlatMonitor.Models;

/// <summary>
/// Persists named filter presets. Callers validate names and districts; the unique indexes on
/// <c>Name</c> and on the default flag are the last line of defence.
/// </summary>
public interface IFilterPresetRepository
{
    /// <summary>All presets, ordered by name.</summary>
    Task<IReadOnlyList<FilterPreset>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<FilterPreset?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive; <paramref name="excludingId"/> skips the preset being renamed.</summary>
    Task<bool> NameExistsAsync(string name, int? excludingId = null, CancellationToken cancellationToken = default);

    Task<FilterPreset> CreateAsync(string name, IReadOnlyList<string> districts, CancellationToken cancellationToken = default);

    /// <returns>The updated preset, or <c>null</c> if no preset has <paramref name="id"/>.</returns>
    Task<FilterPreset?> UpdateAsync(int id, string name, IReadOnlyList<string> districts, CancellationToken cancellationToken = default);

    /// <returns><c>false</c> if no preset has <paramref name="id"/>. Deleting the default leaves no default.</returns>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Makes the preset the only default.</summary>
    /// <returns>The new default, or <c>null</c> if no preset has <paramref name="id"/>.</returns>
    Task<FilterPreset?> SetDefaultAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Leaves no preset marked as default.</summary>
    Task ClearDefaultAsync(CancellationToken cancellationToken = default);
}
