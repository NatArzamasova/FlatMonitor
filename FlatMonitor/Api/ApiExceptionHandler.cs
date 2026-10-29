namespace FlatMonitor.Api;

using FlatMonitor.Api.Contracts;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Turns exceptions that escape a controller into a 500 <see cref="ApiResponse{T}"/> with a generic message,
/// so the client gets the usual envelope but no internals (paths, SQL); the details are logged.
/// A request the client aborted is only logged at Debug.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    public const string ErrorMessage = "An unexpected error occurred";

    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var request = httpContext.Request;

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Request {Method} {Path} was aborted by the client", request.Method, request.Path);
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        _logger.LogError(exception, "Unhandled exception for {Method} {Path}", request.Method, request.Path);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new ApiResponse<object> { Success = false, Message = ErrorMessage }, cancellationToken);
        return true;
    }
}
