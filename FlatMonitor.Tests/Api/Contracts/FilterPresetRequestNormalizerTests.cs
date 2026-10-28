using FlatMonitor.Api.Contracts;
using FluentAssertions;
using NUnit.Framework;

namespace FlatMonitor.Tests.Api.Contracts;

[TestFixture]
public class FilterPresetRequestNormalizerTests
{
    [Test]
    public void TryNormalize_AcceptsValidRequest()
    {
        var ok = FilterPresetRequestNormalizer.TryNormalize(
            Request("East", "Durlach", "Grötzingen"), out var normalized, out var error);

        ok.Should().BeTrue();
        error.Should().BeEmpty();
        normalized.Name.Should().Be("East");
        normalized.Districts.Should().Equal("Durlach", "Grötzingen");
    }

    [Test]
    public void TryNormalize_RejectsNullRequest()
    {
        var ok = FilterPresetRequestNormalizer.TryNormalize(null, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Be("Request body is required");
    }

    [Test]
    public void TryNormalize_TrimsName()
    {
        FilterPresetRequestNormalizer.TryNormalize(Request("  East  "), out var normalized, out _);

        normalized.Name.Should().Be("East");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void TryNormalize_RejectsBlankName(string? name)
    {
        var ok = FilterPresetRequestNormalizer.TryNormalize(Request(name), out _, out var error);

        ok.Should().BeFalse();
        error.Should().Be("Name is required");
    }

    [Test]
    public void TryNormalize_AcceptsNameOfMaxLength()
    {
        var ok = FilterPresetRequestNormalizer.TryNormalize(Request(new string('a', 100)), out _, out _);

        ok.Should().BeTrue();
    }

    [Test]
    public void TryNormalize_RejectsTooLongName()
    {
        var ok = FilterPresetRequestNormalizer.TryNormalize(Request(new string('a', 101)), out _, out var error);

        ok.Should().BeFalse();
        error.Should().Be("Name must be at most 100 characters");
    }

    [Test]
    public void TryNormalize_TreatsNullDistrictsAsAllDistricts()
    {
        var request = new SaveFilterPresetRequest { Name = "All", Districts = null };

        var ok = FilterPresetRequestNormalizer.TryNormalize(request, out var normalized, out _);

        ok.Should().BeTrue();
        normalized.Districts.Should().BeEmpty();
    }

    [Test]
    public void TryNormalize_DropsBlankDistricts()
    {
        var request = new SaveFilterPresetRequest { Name = "East", Districts = ["Durlach", "", "  ", null] };

        FilterPresetRequestNormalizer.TryNormalize(request, out var normalized, out _);

        normalized.Districts.Should().Equal("Durlach");
    }

    [Test]
    public void TryNormalize_CanonicalisesDedupesAndSortsDistricts()
    {
        FilterPresetRequestNormalizer.TryNormalize(
            Request("East", "südstadt", " Durlach ", "DURLACH", "Grötzingen"), out var normalized, out _);

        normalized.Districts.Should().Equal("Durlach", "Grötzingen", "Südstadt");
    }

    [Test]
    public void TryNormalize_RejectsUnknownDistrictsAndListsThem()
    {
        var ok = FilterPresetRequestNormalizer.TryNormalize(
            Request("East", "Durlach", "Bad Schönborn", "KA-Oberreut"), out _, out var error);

        ok.Should().BeFalse();
        error.Should().Be("Unknown districts: Bad Schönborn, KA-Oberreut");
    }

    private static SaveFilterPresetRequest Request(string? name, params string[] districts) =>
        new() { Name = name, Districts = [.. districts] };
}
