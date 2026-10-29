using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace FlatMonitor.Tests.Storage;

[TestFixture]
public class HtmlSnapshotStoreTests
{
    private string _dataDir = null!;
    private ListLogger<HtmlSnapshotStoreTests> _logger = null!;
    private HtmlSnapshotStore _store = null!;

    [SetUp]
    public void Setup()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), $"HtmlSnapshotStoreTests_{Guid.NewGuid()}");
        _logger = new ListLogger<HtmlSnapshotStoreTests>();
        _store = HtmlSnapshotStore.ForSource(new StorageOptions(_dataDir, "Data Source=:memory:"), "OhneMakler", _logger);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dataDir))
            Directory.Delete(_dataDir, recursive: true);
    }

    [Test]
    public void ForSource_UsesTheSourceFolderUnderTheDataDirectory()
    {
        _store.Directory.Should().Be(Path.Combine(_dataDir, "snapshots", "html", "OhneMakler"));
    }

    [Test]
    public async Task SaveAsync_CreatesTheFolderAndWritesTheHtmlUnderTheListingId()
    {
        await _store.SaveAsync("502814", "<html>detail</html>");

        var file = Directory.GetFiles(_store.Directory).Should().ContainSingle().Subject;
        Path.GetFileName(file).Should().MatchRegex(@"^502814_\d{8}_\d{6}\.html$");
        (await File.ReadAllTextAsync(file)).Should().Be("<html>detail</html>");
    }

    [Test]
    public async Task SaveAsync_ReplacesCharactersThatAreInvalidInFileNames()
    {
        await _store.SaveAsync("a/b:c", "<html/>");

        Path.GetFileName(Directory.GetFiles(_store.Directory).Single()).Should().StartWith("a_b");
    }

    [Test]
    public async Task SaveAsync_WhenTheFileCannotBeWritten_LogsAWarningInsteadOfThrowing()
    {
        // A file where the snapshot folder should be makes CreateDirectory fail
        Directory.CreateDirectory(Path.GetDirectoryName(_store.Directory)!);
        await File.WriteAllTextAsync(_store.Directory, "not a folder");

        var act = () => _store.SaveAsync("1", "<html/>");

        await act.Should().NotThrowAsync();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning);
    }

    [Test]
    public async Task ClearAsync_DeletesOnlyHtmlFiles()
    {
        await _store.SaveAsync("1", "<html/>");
        await _store.SaveAsync("2", "<html/>");
        await File.WriteAllTextAsync(Path.Combine(_store.Directory, "notes.txt"), "keep");

        await _store.ClearAsync();

        Directory.GetFiles(_store.Directory).Select(Path.GetFileName).Should().Equal("notes.txt");
    }

    [Test]
    public async Task ClearAsync_WithoutFolder_DoesNothing()
    {
        var act = () => _store.ClearAsync();

        await act.Should().NotThrowAsync();
        Directory.Exists(_store.Directory).Should().BeFalse();
    }
}
