using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Checker.API.ExceptionHandling;

public static class ApiProblems
{
    public static void Enrich(HttpContext context, ProblemDetails problem)
    {
        var status = problem.Status ?? context.Response.StatusCode;
        problem.Status = status;
        var phrase = ReasonPhrases.GetReasonPhrase(status);
        problem.Title ??= string.IsNullOrEmpty(phrase) ? "Error" : phrase;
        problem.Type ??= $"https://www.rfc-editor.org/rfc/rfc9110.html#name-{status}-{problem.Title.ToLowerInvariant().Replace(' ', '-')}";
        problem.Instance ??= context.Request.Path;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
    }

    public static async Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        Enrich(context, problem);
        var service = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        if (!await service.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem }))
        {
            await context.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: context.RequestAborted);
        }
    }
}

public sealed class ApiProblemResult(ProblemDetails problem) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context) => ApiProblems.WriteAsync(context.HttpContext, problem);
}

public sealed class ApiProblemDetailsWriter(IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> options) : IProblemDetailsWriter
{
    public bool CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        ApiProblems.Enrich(context.HttpContext, context.ProblemDetails);
        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
            context.ProblemDetails,
            options.Value.SerializerOptions.GetTypeInfo(context.ProblemDetails.GetType()),
            contentType: "application/problem+json",
            cancellationToken: context.HttpContext.RequestAborted));
    }
}
