namespace FlatMonitor;

using Microsoft.Extensions.Logging;

public static class LoggingExtensions
{
    /// <summary>
    /// Console logging shared by the web server and the CLI. Levels come from the "Logging" section of
    /// appsettings.json: FlatMonitor's own logs from Information up, framework logs (ASP.NET Core,
    /// EF Core SQL commands, HttpClient) only from Warning up.
    /// </summary>
    public static ILoggingBuilder AddFlatMonitorLogging(this ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddConsole();
        return logging;
    }
}
