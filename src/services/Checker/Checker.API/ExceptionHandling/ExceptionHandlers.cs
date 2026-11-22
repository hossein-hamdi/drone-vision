using Checker.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Checker.API.ExceptionHandling;

public sealed class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not ApiException { StatusCode: 400 } error)
        {
            return false;
        }

        await ApiProblems.WriteAsync(context, new ValidationProblemDetails(error.Errors ?? new Dictionary<string, string[]>())
        {
            Status = StatusCodes.Status400BadRequest,
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
        if (exception is not ApiException error)
        {
            return false;
        }

        await ApiProblems.WriteAsync(context, new ProblemDetails
        {
            Status = error.StatusCode,
            Title = error.Title,
            Detail = error.Message
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

        logger.LogError(exception, "Unhandled Checker API error. TraceId: {TraceId}", context.TraceIdentifier);
        await ApiProblems.WriteAsync(context, new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Server Error",
            Detail = "An unexpected error occurred. Use the traceId when reporting this issue."
        });
        return true;
    }
}
