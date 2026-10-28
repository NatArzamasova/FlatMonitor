using FlatMonitor.Api.Contracts;
using FlatMonitor.Api.Controllers;
using FlatMonitor.Models;
using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace FlatMonitor.Tests.Api.Controllers;

[TestFixture]
public class FilterPresetsControllerTests
{
    private static readonly DateTime UpdatedAt = new(2026, 10, 28, 9, 30, 0, DateTimeKind.Utc);

    private Mock<IFilterPresetRepository> _repository = null!;
    private FilterPresetsController _controller = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new Mock<IFilterPresetRepository>();
        _controller = new FilterPresetsController(_repository.Object);
    }

    // ==================== GET ====================

    [Test]
    public async Task GetAllAsync_ReturnsPresetsAsDtos()
    {
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Preset(1, "East", isDefault: true, "Durlach"), Preset(2, "West")]);

        var result = await _controller.GetAllAsync();

        var response = GetResponse(result, 200);
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Filter presets fetched successfully");
        response.Data.Should().BeEquivalentTo(
        [
            new FilterPresetDto { Id = 1, Name = "East", Districts = ["Durlach"], IsDefault = true, UpdatedAtUtc = UpdatedAt },
            new FilterPresetDto { Id = 2, Name = "West", Districts = [], IsDefault = false, UpdatedAtUtc = UpdatedAt }
        ], options => options.WithStrictOrdering());
    }

    [Test]
    public async Task GetAllAsync_ReturnsEmptyListWhenThereAreNoPresets()
    {
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await _controller.GetAllAsync();

        GetResponse(result, 200).Data.Should().BeEmpty();
    }

    [Test]
    public async Task GetAllAsync_ReturnsInternalServerErrorWhenRepositoryFails()
    {
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("query failed"));

        var result = await _controller.GetAllAsync();

        var response = GetResponse(result, 500);
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Error fetching filter presets: query failed");
    }

    // ==================== CREATE ====================

    [Test]
    public async Task CreateAsync_CreatesNormalisedPresetAndReturns201()
    {
        _repository.Setup(r => r.CreateAsync("East", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, IReadOnlyList<string> districts, CancellationToken _) =>
                Preset(7, name, isDefault: false, [.. districts]));

        var result = await _controller.CreateAsync(Request(" East ", "südstadt", "Durlach", "durlach"));

        var response = GetResponse(result, 201);
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Filter preset created");
        response.Data!.Id.Should().Be(7);
        response.Data.Name.Should().Be("East");
        response.Data.Districts.Should().Equal("Durlach", "Südstadt");
        _repository.Verify(r => r.CreateAsync(
            "East",
            It.Is<IReadOnlyList<string>>(d => d.SequenceEqual(new[] { "Durlach", "Südstadt" })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateAsync_ReturnsBadRequestForInvalidRequestWithoutTouchingRepository()
    {
        var result = await _controller.CreateAsync(Request("East", "Bad Schönborn"));

        var response = GetResponse(result, 400);
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Unknown districts: Bad Schönborn");
        _repository.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CreateAsync_ReturnsBadRequestForNullBody()
    {
        var result = await _controller.CreateAsync(null);

        GetResponse(result, 400).Message.Should().Be("Request body is required");
        _repository.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CreateAsync_ReturnsConflictWhenNameIsTaken()
    {
        _repository.Setup(r => r.NameExistsAsync("East", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.CreateAsync(Request("East"));

        var response = GetResponse(result, 409);
        response.Success.Should().BeFalse();
        response.Message.Should().Be("A filter preset named 'East' already exists");
        _repository.Verify(r => r.CreateAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CreateAsync_ReturnsConflictWhenUniqueIndexRejectsConcurrentDuplicate()
    {
        _repository.SetupSequence(r => r.NameExistsAsync("East", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        _repository.Setup(r => r.CreateAsync("East", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("UNIQUE constraint failed"));

        var result = await _controller.CreateAsync(Request("East"));

        GetResponse(result, 409).Message.Should().Be("A filter preset named 'East' already exists");
    }

    [Test]
    public async Task CreateAsync_ReturnsInternalServerErrorForOtherDbUpdateException()
    {
        _repository.Setup(r => r.CreateAsync("East", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("disk full"));

        var result = await _controller.CreateAsync(Request("East"));

        GetResponse(result, 500).Message.Should().Be("Error creating filter preset: disk full");
    }

    [Test]
    public async Task CreateAsync_ReturnsInternalServerErrorWhenRepositoryFails()
    {
        _repository.Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db locked"));

        var result = await _controller.CreateAsync(Request("East"));

        GetResponse(result, 500).Message.Should().Be("Error creating filter preset: db locked");
    }

    // ==================== UPDATE ====================

    [Test]
    public async Task UpdateAsync_UpdatesNormalisedPreset()
    {
        _repository.Setup(r => r.UpdateAsync(3, "Far east", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Preset(3, "Far east", isDefault: true, "Grötzingen"));

        var result = await _controller.UpdateAsync(3, Request("Far east", "grötzingen"));

        var response = GetResponse(result, 200);
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Filter preset updated");
        response.Data!.Name.Should().Be("Far east");
        response.Data.IsDefault.Should().BeTrue();
        _repository.Verify(r => r.UpdateAsync(
            3,
            "Far east",
            It.Is<IReadOnlyList<string>>(d => d.SequenceEqual(new[] { "Grötzingen" })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_ChecksNameUniquenessExcludingThePresetItself()
    {
        _repository.Setup(r => r.UpdateAsync(3, "East", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Preset(3, "East"));

        var result = await _controller.UpdateAsync(3, Request("East"));

        GetResponse(result, 200);
        _repository.Verify(r => r.NameExistsAsync("East", 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_ReturnsConflictWhenNameBelongsToAnotherPreset()
    {
        _repository.Setup(r => r.NameExistsAsync("West", 3, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.UpdateAsync(3, Request("West"));

        GetResponse(result, 409).Message.Should().Be("A filter preset named 'West' already exists");
        _repository.Verify(r => r.UpdateAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task UpdateAsync_ReturnsBadRequestForInvalidRequestWithoutTouchingRepository()
    {
        var result = await _controller.UpdateAsync(3, Request("  "));

        GetResponse(result, 400).Message.Should().Be("Name is required");
        _repository.VerifyNoOtherCalls();
    }

    [Test]
    public async Task UpdateAsync_ReturnsNotFoundForUnknownId()
    {
        _repository.Setup(r => r.UpdateAsync(42, "East", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FilterPreset?)null);

        var result = await _controller.UpdateAsync(42, Request("East"));

        var response = GetResponse(result, 404);
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Filter preset 42 not found");
    }

    [Test]
    public async Task UpdateAsync_ReturnsConflictWhenUniqueIndexRejectsConcurrentDuplicate()
    {
        _repository.SetupSequence(r => r.NameExistsAsync("West", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        _repository.Setup(r => r.UpdateAsync(3, "West", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("UNIQUE constraint failed"));

        var result = await _controller.UpdateAsync(3, Request("West"));

        GetResponse(result, 409);
    }

    [Test]
    public async Task UpdateAsync_ReturnsInternalServerErrorWhenRepositoryFails()
    {
        _repository.Setup(r => r.UpdateAsync(3, "East", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db locked"));

        var result = await _controller.UpdateAsync(3, Request("East"));

        GetResponse(result, 500).Message.Should().Be("Error updating filter preset: db locked");
    }

    // ==================== DELETE ====================

    [Test]
    public async Task DeleteAsync_DeletesPresetAndReturnsRemainingPresets()
    {
        _repository.Setup(r => r.DeleteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Preset(2, "West")]);

        var result = await _controller.DeleteAsync(1);

        var response = GetResponse(result, 200);
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Filter preset deleted");
        response.Data!.Select(p => p.Id).Should().Equal(2);
    }

    [Test]
    public async Task DeleteAsync_ReturnsNotFoundForUnknownId()
    {
        _repository.Setup(r => r.DeleteAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.DeleteAsync(42);

        GetResponse(result, 404).Message.Should().Be("Filter preset 42 not found");
    }

    [Test]
    public async Task DeleteAsync_ReturnsInternalServerErrorWhenRepositoryFails()
    {
        _repository.Setup(r => r.DeleteAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db locked"));

        var result = await _controller.DeleteAsync(1);

        GetResponse(result, 500).Message.Should().Be("Error deleting filter preset: db locked");
    }

    // ==================== DEFAULT ====================

    [Test]
    public async Task SetDefaultAsync_ReturnsNewDefault()
    {
        _repository.Setup(r => r.SetDefaultAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Preset(1, "East", isDefault: true));

        var result = await _controller.SetDefaultAsync(1);

        var response = GetResponse(result, 200);
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Default filter preset set");
        response.Data!.IsDefault.Should().BeTrue();
    }

    [Test]
    public async Task SetDefaultAsync_ReturnsNotFoundForUnknownId()
    {
        _repository.Setup(r => r.SetDefaultAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync((FilterPreset?)null);

        var result = await _controller.SetDefaultAsync(42);

        GetResponse(result, 404).Message.Should().Be("Filter preset 42 not found");
    }

    [Test]
    public async Task SetDefaultAsync_ReturnsInternalServerErrorWhenRepositoryFails()
    {
        _repository.Setup(r => r.SetDefaultAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db locked"));

        var result = await _controller.SetDefaultAsync(1);

        GetResponse(result, 500).Message.Should().Be("Error setting default filter preset: db locked");
    }

    [Test]
    public async Task ClearDefaultAsync_ClearsDefaultAndReturnsAllPresets()
    {
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Preset(1, "East")]);

        var result = await _controller.ClearDefaultAsync();

        var response = GetResponse(result, 200);
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Default filter preset cleared");
        response.Data!.Should().ContainSingle().Which.IsDefault.Should().BeFalse();
        _repository.Verify(r => r.ClearDefaultAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ClearDefaultAsync_ReturnsInternalServerErrorWhenRepositoryFails()
    {
        _repository.Setup(r => r.ClearDefaultAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db locked"));

        var result = await _controller.ClearDefaultAsync();

        GetResponse(result, 500).Message.Should().Be("Error clearing default filter preset: db locked");
    }

    private static FilterPreset Preset(int id, string name, bool isDefault = false, params string[] districts) => new()
    {
        Id = id,
        Name = name,
        Districts = [.. districts],
        IsDefault = isDefault,
        UpdatedAtUtc = UpdatedAt
    };

    private static SaveFilterPresetRequest Request(string? name, params string[] districts) =>
        new() { Name = name, Districts = [.. districts] };

    private static ApiResponse<T> GetResponse<T>(ActionResult<ApiResponse<T>> result, int expectedStatusCode)
    {
        var objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        (objectResult.StatusCode ?? 200).Should().Be(expectedStatusCode);
        return objectResult.Value.Should().BeOfType<ApiResponse<T>>().Subject;
    }
}
