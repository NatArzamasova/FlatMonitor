namespace FlatMonitor.Storage;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Where FlatMonitor keeps its files: the SQLite database and the data directory (JSONL backups and archived HTML pages).
/// Both come from configuration (<c>appsettings.json</c>, overridable by environment variables such as
/// <c>Storage__DataDirectory</c> or <c>ConnectionStrings__Flats</c>). Relative paths are resolved against the
/// content root, which is the FlatMonitor project directory under <c>dotnet run</c> and <c>dotnet ef</c>.
/// </summary>
public sealed record StorageOptions(string DataDirectory, string ConnectionString)
{
    public const string ConnectionStringName = "Flats";
    public const string DataDirectoryKey = "Storage:DataDirectory";

    public static StorageOptions FromConfiguration(IConfiguration configuration, string contentRootPath)
    {
        var dataDirectory = configuration[DataDirectoryKey]
            ?? throw MissingSetting($"'{DataDirectoryKey}'", contentRootPath);
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw MissingSetting($"connection string '{ConnectionStringName}'", contentRootPath);

        return new StorageOptions(
            Path.GetFullPath(dataDirectory, contentRootPath),
            ResolveSqliteDataSource(connectionString, contentRootPath));
    }

    /// <summary>Creates the data directory and the database file's directory if they are missing.</summary>
    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);

        var databaseDirectory = Path.GetDirectoryName(new SqliteConnectionStringBuilder(ConnectionString).DataSource);
        if (!string.IsNullOrEmpty(databaseDirectory))
            Directory.CreateDirectory(databaseDirectory);
    }

    private static string ResolveSqliteDataSource(string connectionString, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (!string.IsNullOrEmpty(builder.DataSource) && builder.DataSource != ":memory:")
            builder.DataSource = Path.GetFullPath(builder.DataSource, contentRootPath);
        return builder.ToString();
    }

    private static InvalidOperationException MissingSetting(string setting, string contentRootPath) =>
        new($"Missing configuration {setting}. Expected appsettings.json in the content root '{contentRootPath}' " +
            "(run from the FlatMonitor project directory, which dotnet run does) or an environment variable override.");
}
