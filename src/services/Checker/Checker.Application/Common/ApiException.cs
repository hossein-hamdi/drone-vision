namespace Checker.Application.Common;

public sealed class ApiException(
    int statusCode,
    string title,
    string detail,
    IDictionary<string, string[]>? errors = null) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public IDictionary<string, string[]>? Errors { get; } = errors;

    public static ApiException Validation(string field, string message) =>
        new(400, "Validation Failed", "One or more validation errors occurred.",
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] });

    public static ApiException Validation(IDictionary<string, string[]> errors) =>
        new(400, "Validation Failed", "One or more validation errors occurred.", errors);

    public static ApiException Unauthorized() =>
        new(401, "Unauthorized", "A valid access token or browser session is required.");

    public static ApiException Forbidden() =>
        new(403, "Forbidden", "You do not have permission to perform this action.");

    public static ApiException NotFound(string resource) =>
        new(404, "Not Found", $"{resource} was not found.");

    public static ApiException Conflict(string detail) =>
        new(409, "Conflict", detail);

    public static ApiException ServiceUnavailable(string detail) =>
        new(503, "Service Unavailable", detail);
}
