using FlatMonitor.Models;
using FluentAssertions;
using NUnit.Framework;

namespace FlatMonitor.Tests.Models;

[TestFixture]
public class FlatPreviewTests
{
    [Test]
    public void ArchivedFlatPreviewFrom_CopiesEveryField()
    {
        var preview = new FlatPreview
        {
            Source = "Immowelt",
            ListingId = "7",
            ImageUrl = "https://example.org/7.webp",
            ContentType = "image/webp",
            Data = [1, 2, 3],
            DownloadedAtUtc = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc),
        };

        ArchivedFlatPreview.From(preview).Should().BeEquivalentTo(preview);
    }
}
