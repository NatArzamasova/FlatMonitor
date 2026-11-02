namespace FlatMonitor.Tests.Scraping;

using FlatMonitor.Scraping;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class KeepSystemAwakeTests
{
    [Test]
    public void Start_IsActiveOnWindowsOnly()
    {
        var logger = new ListLogger<KeepSystemAwakeTests>();

        using var awake = KeepSystemAwake.Start("FlatMonitor test", logger);

        awake.IsActive.Should().Be(OperatingSystem.IsWindows());
        logger.Entries.Should().NotContain(e => e.Level >= Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Test]
    public void Dispose_EndsTheRequest_AndCanBeRepeated()
    {
        var awake = KeepSystemAwake.Start("FlatMonitor test", new ListLogger<KeepSystemAwakeTests>());

        awake.Dispose();
        var again = () => awake.Dispose();

        awake.IsActive.Should().BeFalse();
        again.Should().NotThrow();
    }
}
