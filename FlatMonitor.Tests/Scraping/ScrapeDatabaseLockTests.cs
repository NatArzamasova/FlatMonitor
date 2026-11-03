using FlatMonitor.Scraping;
using FluentAssertions;
using NUnit.Framework;

namespace FlatMonitor.Tests.Scraping;

[TestFixture]
public class ScrapeDatabaseLockTests
{
    [Test]
    public async Task RunAsync_RunsOneCallAtATimeAndReturnsItsResult()
    {
        var database = new ScrapeDatabaseLock();
        var first = new TaskCompletionSource();
        var order = new List<string>();

        var running = database.RunAsync(async () =>
        {
            order.Add("first started");
            await first.Task;
            order.Add("first done");
            return 1;
        });
        var waiting = database.RunAsync(() =>
        {
            order.Add("second");
            return Task.FromResult(2);
        });

        order.Should().Equal("first started");
        first.SetResult();
        (await running).Should().Be(1);
        (await waiting).Should().Be(2);
        order.Should().Equal("first started", "first done", "second");
    }

    [Test]
    public async Task RunAsync_AfterAFailedCall_LetsTheNextOneRun()
    {
        var database = new ScrapeDatabaseLock();

        var failing = () => database.RunAsync(() => Task.FromException(new InvalidOperationException("database is locked")));
        await failing.Should().ThrowAsync<InvalidOperationException>();

        (await database.RunAsync(() => Task.FromResult("next"))).Should().Be("next");
    }

    [Test]
    public async Task RunAsync_CanceledWhileWaiting_ThrowsWithoutRunningTheCall()
    {
        var database = new ScrapeDatabaseLock();
        var first = new TaskCompletionSource();
        var running = database.RunAsync(() => first.Task);
        using var cts = new CancellationTokenSource();
        var ran = false;

        var waiting = database.RunAsync(() =>
        {
            ran = true;
            return Task.CompletedTask;
        }, cts.Token);
        cts.Cancel();

        await ((Func<Task>)(() => waiting)).Should().ThrowAsync<OperationCanceledException>();
        first.SetResult();
        await running;
        ran.Should().BeFalse();
    }
}
