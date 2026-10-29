using System.Text.Json;
using System.Text.Json.Nodes;
using FlatMonitor.Api.Contracts;
using FlatMonitor.Models;
using FlatMonitor.Storage.ReadModels;
using FluentAssertions;
using NUnit.Framework;
using static FlatMonitor.Api.Services.ScrapeJobService;

namespace FlatMonitor.Tests.Api.Contracts;

/// <summary>
/// The JSON the Angular app reads. The DTO changes of round 2 (4.1) must not change it; serialized with the same
/// web defaults (camelCase) as ASP.NET Core's controllers.
/// </summary>
[TestFixture]
public class ApiJsonShapeTests
{
    private static JsonNode? ToJson<T>(T value) => JsonSerializer.SerializeToNode(value, JsonSerializerOptions.Web);

    [Test]
    public void ScrapingHistoryDto_SerializesExactlyLikeTheEntityDid()
    {
        var history = new ScrapingHistory
        {
            Id = 7,
            StartTimeUtc = new DateTime(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc),
            EndTimeUtc = new DateTime(2026, 10, 29, 8, 20, 0, DateTimeKind.Utc),
            Status = ScrapingStatus.PartialFailed,
            ErrorMessage = "ImmobilienScout24: Login failed",
            FlatsScrapedCount = 575,
        };

        var dto = ToJson(ScrapingHistoryDto.From(history));

        JsonNode.DeepEquals(dto, ToJson(history)).Should().BeTrue($"the DTO gave {dto}");
        dto!["status"]!.GetValue<string>().Should().Be("PartialFailed");
    }

    [Test]
    public void ScrapingHistoryDto_FromNull_IsNull()
    {
        ScrapingHistoryDto.From(null).Should().BeNull();
    }

    [Test]
    public void FlatHistory_ScrapingDate_IsADateWithoutTime()
    {
        var json = ToJson(new FlatHistory { ScrapingDate = new DateOnly(2026, 10, 20), District = "Oststadt" });

        json!["scrapingDate"]!.GetValue<string>().Should().Be("2026-10-20");
    }

    [TestCase(ScrapeJobStatus.Queued, "Queued")]
    [TestCase(ScrapeJobStatus.Running, "Running")]
    [TestCase(ScrapeJobStatus.Canceled, "Canceled")]
    public void ScrapeJobStatusResponse_Status_IsTheName(ScrapeJobStatus status, string expected)
    {
        var json = ToJson(new ScrapeJobStatusResponse { JobId = Guid.NewGuid(), Status = status, CreatedAtUtc = DateTime.UtcNow });

        json!["status"]!.GetValue<string>().Should().Be(expected);
    }
}
