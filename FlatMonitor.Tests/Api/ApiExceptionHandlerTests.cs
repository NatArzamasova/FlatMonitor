using System.Net;
using System.Text.Json;
using FlatMonitor.Api;
using FlatMonitor.Api.Controllers;
using FlatMonitor.Api.Services;
using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace FlatMonitor.Tests.Api;

[TestFixture]
public class ApiExceptionHandlerTests
{
    [Test]
    public async Task TryHandleAsync_WritesGenericApiResponse500AndLogsTheException()
    {
        // Arrange
        var logger = new ListLogger<ApiExceptionHandler>();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/flats/current-offers";
        context.Response.Body = new MemoryStream();
        var exception = new InvalidOperationException(@"SQLite Error 1: 'no such table' in C:\data\listings.db");

        // Act
        var handled = await new ApiExceptionHandler(logger).TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Be("""{"success":false,"data":null,"message":"An unexpected error occurred"}""");
        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(exception);
        entry.Message.Should().Be("Unhandled exception for GET /api/flats/current-offers");
    }

    [Test]
    public async Task TryHandleAsync_WhenTheClientAborted_Returns499WithoutBodyOrErrorLog()
    {
        // Arrange
        var logger = new ListLogger<ApiExceptionHandler>();
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        context.Response.Body = new MemoryStream();

        // Act
        await new ApiExceptionHandler(logger).TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status499ClientClosedRequest);
        context.Response.Body.Length.Should().Be(0);
        logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }
}

/// <summary>
/// The real web pipeline on a random local port: the services from <c>AddFlatMonitorApi</c>, <c>UseExceptionHandler</c>
/// and the real controllers, with a snapshot repository that throws.
/// </summary>
[TestFixture]
public class ApiExceptionHandlingPipelineTests
{
    private const string SecretDetail = @"no such table: FlatsSnapshots (C:\secret\listings.db)";

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task StartServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddFlatMonitorApi();
        // No job runner: a queued scrape stays queued (there is no database to run it against)
        builder.Services.Remove(builder.Services.Single(d => d.ImplementationType == typeof(ScrapeBackgroundService)));
        builder.Services.AddControllers().AddApplicationPart(typeof(FlatsController).Assembly);

        var snapshots = new Mock<ISnapshotRepository>();
        snapshots.Setup(r => r.GetCurrentOffersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(SecretDetail));
        builder.Services.AddSingleton(snapshots.Object);
        builder.Services.AddSingleton(Mock.Of<IFlatAggregationRepository>());
        builder.Services.AddSingleton(Mock.Of<IScrapingHistoryRepository>());

        _app = builder.Build();
        _app.UseExceptionHandler();
        _app.MapControllers();
        await _app.StartAsync();

        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    [OneTimeTearDown]
    public async Task StopServer()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Test]
    public async Task UnhandledControllerException_Returns500ApiResponseWithoutInternals()
    {
        var response = await _client.GetAsync("/api/flats/current-offers");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("secret").And.NotContain("FlatsSnapshots");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("message").GetString().Should().Be(ApiExceptionHandler.ErrorMessage);
    }

    [Test]
    public async Task SuccessfulRequest_ReturnsTheApiResponseEnvelope()
    {
        var response = await _client.GetAsync("/api/districts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("data").GetArrayLength().Should().Be(27);
    }

    [Test]
    public async Task SecondScrapeTrigger_Returns409ApiResponse()
    {
        // The first trigger queues a job that stays queued, because this host has no job runner
        (await _client.PostAsync("/api/scrape", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);

        var response = await _client.PostAsync("/api/scrape", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("message").GetString().Should().Be("A scrape is already queued or running");
    }
}
