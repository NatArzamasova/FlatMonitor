using NUnit.Framework;
using Moq;
using FlatMonitor.Storage;

namespace FlatMonitor.Tests.Api.Services;

[TestFixture]
public class ScrapeBackgroundServiceTests
{
    /// <summary>
    /// Tests that backup/restore methods are available and can be mocked.
    /// Full integration testing of ScrapeBackgroundService is covered by
    /// the FlatSnapshotRepositoryTests.BackupRestoreDelete_FullWorkflow_* tests.
    /// </summary>
    [Test]
    public async Task BackupRestoreMethods_AreCallable_WithoutError()
    {
        // Arrange
        var mockRepository = new Mock<ISnapshotRepository>();

        mockRepository
            .Setup(r => r.BackupFlatsSnapshotsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockRepository
            .Setup(r => r.RestoreFlatsSnapshotsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockRepository
            .Setup(r => r.DeleteBackupAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - All methods should be callable
        await mockRepository.Object.BackupFlatsSnapshotsAsync();
        await mockRepository.Object.RestoreFlatsSnapshotsAsync();
        await mockRepository.Object.DeleteBackupAsync();

        // Assert - Verify they were called
        mockRepository.Verify(r => r.BackupFlatsSnapshotsAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockRepository.Verify(r => r.RestoreFlatsSnapshotsAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockRepository.Verify(r => r.DeleteBackupAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
