namespace FlatMonitor.Scraping;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

/// <summary>
/// Keeps Windows from going to sleep while held: a power request, as video players and installers make (listed by
/// <c>powercfg /requests</c>). A scrape takes half an hour; Windows' sleep after inactivity (Modern Standby once the
/// screen is off) cut the connection to the immowelt Chrome mid-run. The screen may still turn off and lock. On battery,
/// some laptops enter Modern Standby despite the request. Elsewhere than on Windows, and if Windows refuses, nothing
/// happens: a scrape never fails for it.
/// </summary>
public sealed class KeepSystemAwake : IDisposable
{
    private const uint PowerRequestContextVersion = 0;
    private const uint PowerRequestContextSimpleString = 0x1;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    /// <summary>System required: no sleep after inactivity. Execution required: the process is not suspended.</summary>
    private static readonly PowerRequestType[] Requests = [PowerRequestType.SystemRequired, PowerRequestType.ExecutionRequired];

    private IntPtr _handle;

    private KeepSystemAwake(IntPtr handle) => _handle = handle;

    /// <summary>Whether Windows accepted the request; <c>false</c> elsewhere or if it refused.</summary>
    public bool IsActive => _handle != IntPtr.Zero;

    /// <summary>Asks Windows to stay awake until the result is disposed; <paramref name="reason"/> shows in <c>powercfg /requests</c>.</summary>
    public static KeepSystemAwake Start(string reason, ILogger logger)
    {
        if (!OperatingSystem.IsWindows())
            return new KeepSystemAwake(IntPtr.Zero);

        var context = new ReasonContext
        {
            Version = PowerRequestContextVersion,
            Flags = PowerRequestContextSimpleString,
            SimpleReasonString = reason,
        };
        var handle = PowerCreateRequest(ref context);
        if (handle == IntPtr.Zero || handle == InvalidHandleValue)
        {
            logger.LogWarning("Could not keep Windows awake (error {Error}): it may go to sleep during the scrape",
                Marshal.GetLastPInvokeError());
            return new KeepSystemAwake(IntPtr.Zero);
        }

        foreach (var request in Requests)
        {
            if (!PowerSetRequest(handle, request))
                logger.LogWarning("Windows refused the power request {Request} (error {Error})", request, Marshal.GetLastPInvokeError());
        }

        logger.LogInformation("Keeping Windows awake until the scrape ends");
        return new KeepSystemAwake(handle);
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero)
            return;

        foreach (var request in Requests)
            PowerClearRequest(_handle, request);
        CloseHandle(_handle);
        _handle = IntPtr.Zero;
    }

    private enum PowerRequestType
    {
        SystemRequired = 1,
        ExecutionRequired = 3,
    }

    /// <summary>REASON_CONTEXT with a simple reason string.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ReasonContext
    {
        public uint Version;
        public uint Flags;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string SimpleReasonString;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr PowerCreateRequest(ref ReasonContext context);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PowerSetRequest(IntPtr handle, PowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PowerClearRequest(IntPtr handle, PowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
