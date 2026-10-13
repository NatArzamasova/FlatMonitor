namespace workInProgress.Scraping.ImmobilienScout24;

using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using HtmlAgilityPack;

public class ImmobilienScout24LoginManager
{
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _httpClient;

    public ImmobilienScout24LoginManager()
    {
        _handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = true,
        };
        _httpClient = new HttpClient(_handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        Console.WriteLine("Loading .env credentials...");
        var username_env = Environment.GetEnvironmentVariable("IS24_USERNAME");
        var password_env = Environment.GetEnvironmentVariable("IS24_PASSWORD");

        if (string.IsNullOrEmpty(username_env) || string.IsNullOrEmpty(password_env))
        {
            Console.Error.WriteLine("Error: IS24_USERNAME or IS24_PASSWORD not set in .env");
            return false;
        }

        Console.WriteLine($"Credentials loaded for: {username_env}");

        // The site has anti-bot protection. Try to access the listings page first.
        var listingsUrl = "https://www.immobilienscout24.de/Suche/de/baden-wuerttemberg/karlsruhe/wohnung-kaufen";
        Console.WriteLine($"Checking access to listings page...");

        try
        {
            var response = await _httpClient.GetAsync(listingsUrl);
            Console.WriteLine($"Response status: {response.StatusCode}");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Console.WriteLine("✓ Page accessible.");
                return true;
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized ||
                     response.StatusCode == HttpStatusCode.Forbidden)
            {
                Console.WriteLine("✗ Page blocked (anti-bot protection or requires login).");
                return false;
            }
            else
            {
                Console.WriteLine($"Unexpected status: {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error checking access: {ex.Message}");
            return false;
        }
    }

    public async Task<string?> FetchPageAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.Forbidden ||
                response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.Error.WriteLine($"Access denied to {url} (HTTP {(int)response.StatusCode})");
                Console.Error.WriteLine("Robot guard detected. Opening browser for manual authentication...");
                OpenBrowserForRobotGuard();
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching {url}: {ex.Message}");
            return null;
        }
    }

    private void OpenBrowserForRobotGuard()
    {
        var url = "https://www.immobilienscout24.de/Suche/de/baden-wuerttemberg/karlsruhe/wohnung-kaufen";
        Console.WriteLine($"\nOpening browser: {url}");

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                System.Diagnostics.Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                System.Diagnostics.Process.Start("open", url);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open browser: {ex.Message}");
            Console.WriteLine($"Please open manually: {url}");
        }

        Console.WriteLine("\nAfter passing the robot guard in your browser, press Enter to continue...");
        Console.ReadLine();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _handler?.Dispose();
    }
}
