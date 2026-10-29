namespace FlatMonitor.Api.Controllers;

using FlatMonitor.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Base for the API controllers: every response body is an <see cref="ApiResponse{T}"/>.
/// Actions need no try/catch: <see cref="ApiExceptionHandler"/> turns unhandled exceptions into a 500 response.
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    protected static ApiResponse<T> Success<T>(T data, string message) =>
        new() { Success = true, Data = data, Message = message };

    protected static ApiResponse<T> Failure<T>(string message) =>
        new() { Success = false, Message = message };
}
