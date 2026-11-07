using Management.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Management.API.ExceptionHandling;

public sealed class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not ApiException { StatusCode: 400 } error) return false;
        await ApiProblems.WriteAsync(context, new ValidationProblemDetails(error.Errors ?? new Dictionary<string, string[]>())
        {
            Status = 400,
            Title = error.Title,
            Detail = error.Message
        });
        return true;
    }
}

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not ApiException error) return false;
        await ApiProblems.WriteAsync(context, new ProblemDetails { Status = error.StatusCode, Title = error.Title, Detail = error.Message });
        return true;
    }
}

public sealed class DatabaseConflictExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var sql = exception as SqlException ?? exception.InnerException as SqlException;
        if (exception is not DbUpdateConcurrencyException && sql?.Number is not (2601 or 2627 or 547 or 1205)) return false;
        await ApiProblems.WriteAsync(context, new ProblemDetails
        {
            Status = 409,
            Title = "Conflict",
            Detail = "The record already exists, has related records, or changed concurrently. Reload and try again."
        });
        return true;
    }
}

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }
        if (exception is BadHttpRequestException badRequest)
        {
            await ApiProblems.WriteAsync(context, new ProblemDetails
            {
                Status = badRequest.StatusCode,
                Title = "Invalid Request",
                Detail = "The request could not be processed."
            });
            return true;
        }
        logger.LogError(exception, "Unexpected API failure. TraceId: {TraceId}", context.TraceIdentifier);
        await ApiProblems.WriteAsync(context, new ProblemDetails
        {
            Status = 500,
            Title = "Server Error",
            Detail = "An unexpected error occurred. Use the traceId when reporting this issue."
        });
        return true;
    }
}
