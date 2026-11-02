namespace FlatMonitor.Tests.Immowelt;

using System.Net;
using System.Net.Sockets;
using FlatMonitor.Scraping.Immowelt;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

/// <summary>Starting Chrome itself needs an installed Chrome: covered by the live run, not here.</summary>
[TestFixture]
public class ImmoweltChromeTests
{
    [Test]
    public void StartInfo_IsTheCommandLineAUserWouldType()
    {
        var options = new ImmoweltOptions { CdpUrl = "http://127.0.0.1:9333", ChromeUserDataDir = @"C:\Profiles\Scraper" };

        var startInfo = ImmoweltChrome.StartInfo(@"C:\Chrome\chrome.exe", options);

        startInfo.FileName.Should().Be(@"C:\Chrome\chrome.exe");
        startInfo.ArgumentList.Should().Equal(
            "--remote-debugging-port=9333",
            @"--user-data-dir=C:\Profiles\Scraper",
            "--no-first-run",
            "--no-default-browser-check");
    }

    [Test]
    public void StartInfo_HasNoAutomationOrHeadlessFlags()
    {
        // DataDome recognizes an automated browser by these
        var startInfo = ImmoweltChrome.StartInfo(@"C:\Chrome\chrome.exe", new ImmoweltOptions());

        startInfo.ArgumentList.Should().NotContain(arg => arg.Contains("headless") || arg.Contains("automation") || arg.Contains("enable-blink-features"));
    }

    [Test]
    public void StartInfo_ExpandsEnvironmentVariablesInTheProfile()
    {
        var startInfo = ImmoweltChrome.StartInfo(@"C:\Chrome\chrome.exe", new ImmoweltOptions());

        startInfo.ArgumentList[1].Should().Be(
            "--user-data-dir=" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlatMonitorChrome"));
    }

    [Test]
    public void ChromePath_WithoutSetting_IsTheFirstInstallLocationThatExists()
    {
        var perUser = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe");

        ImmoweltChrome.ChromePath(new ImmoweltOptions(), path => path == perUser).Should().Be(perUser);
    }

    [Test]
    public void ChromePath_WithoutSettingAndInstall_FailsAndNamesTheSetting()
    {
        var act = () => ImmoweltChrome.ChromePath(new ImmoweltOptions(), _ => false);

        act.Should().Throw<InvalidOperationException>().WithMessage("Chrome not found*Program Files*Scrapers:Immowelt:ChromePath*");
    }

    [Test]
    public void ChromePath_Setting_IsUsedEvenIfChromeIsInstalled()
    {
        var options = new ImmoweltOptions { ChromePath = @"D:\Portable\chrome.exe" };

        ImmoweltChrome.ChromePath(options, _ => true).Should().Be(@"D:\Portable\chrome.exe");
    }

    [Test]
    public void ChromePath_SettingThatDoesNotExist_Fails()
    {
        var options = new ImmoweltOptions { ChromePath = @"D:\Portable\chrome.exe" };

        var act = () => ImmoweltChrome.ChromePath(options, _ => false);

        act.Should().Throw<InvalidOperationException>().WithMessage(@"Chrome not found at D:\Portable\chrome.exe*");
    }

    [TestCase("http://127.0.0.1:9222", 9222)]
    [TestCase("http://127.0.0.1:9333/", 9333)]
    [TestCase("not a url", 9222)]
    public void Port_IsTheCdpUrlsPort(string cdpUrl, int expected)
    {
        ImmoweltChrome.Port(cdpUrl).Should().Be(expected);
    }

    [Test]
    public async Task IsListeningAsync_WithoutChrome_IsFalse()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var chrome = new ImmoweltChrome(new ImmoweltOptions { CdpUrl = $"http://127.0.0.1:{FreePort()}" }, http, NullLogger.Instance);

        (await chrome.IsListeningAsync(default)).Should().BeFalse();
    }

    [Test]
    public async Task IsListeningAsync_WithChromesVersionEndpoint_IsTrue()
    {
        using var listener = new HttpListener();
        var port = FreePort();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var answer = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            context.Request.Url!.AbsolutePath.Should().Be("/json/version");
            context.Response.StatusCode = 200;
            context.Response.Close();
        });
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var chrome = new ImmoweltChrome(new ImmoweltOptions { CdpUrl = $"http://127.0.0.1:{port}" }, http, NullLogger.Instance);

        (await chrome.IsListeningAsync(default)).Should().BeTrue();
        await answer;
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
