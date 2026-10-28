namespace FlatMonitor;

using Microsoft.Extensions.Logging;

public static class LoggingExtensions
{
    /// <summary>
    /// Console logging shared by the web server and the CLI: FlatMonitor's own logs from Information up,
    /// framework logs (ASP.NET Core, EF Core SQL commands, HttpClient) only from Warning up.
    /// </summary>
    public static ILoggingBuilder AddFlatMonitorLogging(this ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        return logging;
    }
}
