namespace FlatMonitor.Scraping.Immowelt;

using System.Diagnostics;
using Microsoft.Extensions.Logging;

/// <summary>
/// Starts the installed Chrome with remote debugging, exactly as the user would by hand: no automation flags, so
/// DataDome sees the user's browser, not Playwright's. Cookies (and so a passed robot check) persist in its profile.
/// </summary>
internal sealed class ImmoweltChrome
{
    private const int StartTimeoutMs = 20_000;
    private const int PollIntervalMs = 250;

    /// <summary>Where Chrome is installed per machine and per user on Windows.</summary>
    private static readonly string[] InstallPaths =
    [
        @"%ProgramFiles%\Google\Chrome\Application\chrome.exe",
        @"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe",
        @"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe",
    ];

    private readonly ImmoweltOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger _logger;

    public ImmoweltChrome(ImmoweltOptions options, HttpClient http, ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Whether a Chrome answers at <see cref="ImmoweltOptions.CdpUrl"/>.</summary>
    public async Task<bool> IsListeningAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(VersionUrl(_options.CdpUrl), ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return false;
        }
    }

    /// <summary>
    /// Starts Chrome and waits until it answers at <see cref="ImmoweltOptions.CdpUrl"/>. Throws
    /// <see cref="InvalidOperationException"/> if there is no Chrome to start or it never answers.
    /// </summary>
    public async Task StartAsync(CancellationToken ct)
    {
        var chromePath = ChromePath(_options, path => File.Exists(path));

        var startInfo = StartInfo(chromePath, _options);
        var userDataDir = Environment.ExpandEnvironmentVariables(_options.ChromeUserDataDir);
        _logger.LogInformation("Immowelt: starting Chrome ({ChromePath}) with profile {UserDataDir}", chromePath, userDataDir);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Chrome ({chromePath}) did not start.");

        var waited = 0;
        while (!await IsListeningAsync(ct))
        {
            if (waited >= StartTimeoutMs)
            {
                // Chrome hands the arguments to a window of the same profile already open and exits: no port then
                throw new InvalidOperationException(
                    $"Chrome started but does not answer at {_options.CdpUrl} after {StartTimeoutMs / 1000} s. " +
                    $"Close the Chrome windows of the profile {userDataDir} and scrape again.");
            }

            await Task.Delay(PollIntervalMs, ct);
            waited += PollIntervalMs;
        }

        _logger.LogInformation("Immowelt: Chrome answers at {CdpUrl}", _options.CdpUrl);
    }

    /// <summary>
    /// The configured <c>chrome.exe</c>, else the first install location that exists. Throws
    /// <see cref="InvalidOperationException"/> if there is none.
    /// </summary>
    internal static string ChromePath(ImmoweltOptions options, Func<string, bool> exists)
    {
        const string setting = $"{ImmoweltOptions.SectionName}:{nameof(ImmoweltOptions.ChromePath)}";
        if (options.ChromePath != null)
        {
            var configured = Environment.ExpandEnvironmentVariables(options.ChromePath);
            return exists(configured)
                ? configured
                : throw new InvalidOperationException($"Chrome not found at {configured} ({setting}).");
        }

        var candidates = InstallPaths.Select(Environment.ExpandEnvironmentVariables).ToList();
        return candidates.FirstOrDefault(exists)
            ?? throw new InvalidOperationException(
                $"Chrome not found ({string.Join(", ", candidates)}); set {setting} to its chrome.exe.");
    }

    /// <summary>
    /// The command line a user would type: debugging port and own profile, without first-run dialogs; no automation
    /// or headless flags.
    /// </summary>
    internal static ProcessStartInfo StartInfo(string chromePath, ImmoweltOptions options)
    {
        var startInfo = new ProcessStartInfo(chromePath) { UseShellExecute = false };
        startInfo.ArgumentList.Add($"--remote-debugging-port={Port(options.CdpUrl)}");
        startInfo.ArgumentList.Add($"--user-data-dir={Environment.ExpandEnvironmentVariables(options.ChromeUserDataDir)}");
        startInfo.ArgumentList.Add("--no-first-run");
        startInfo.ArgumentList.Add("--no-default-browser-check");
        return startInfo;
    }

    internal static int Port(string cdpUrl) =>
        Uri.TryCreate(cdpUrl, UriKind.Absolute, out var uri) ? uri.Port : 9222;

    private static string VersionUrl(string cdpUrl) => cdpUrl.TrimEnd('/') + "/json/version";
}
