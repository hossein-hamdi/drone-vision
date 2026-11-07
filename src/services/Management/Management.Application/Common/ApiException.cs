namespace Management.Application.Common;

public sealed class ApiException(int statusCode, string title, string detail,
    IDictionary<string, string[]>? errors = null) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public IDictionary<string, string[]>? Errors { get; } = errors;

    public static ApiException Validation(string field, string message) =>
        new(400, "Validation Failed", "One or more validation errors occurred.",
            new Dictionary<string, string[]> { [field] = [message] });
    public static ApiException Unauthorized() => new(401, "Unauthorized", "Invalid credentials or authentication token.");
    public static ApiException NotFound(string resource) => new(404, "Not Found", $"{resource} was not found.");
    public static ApiException Conflict(string detail) => new(409, "Conflict", detail);
}
