namespace Checker.API.Authentication;

public sealed record ValidatedManagementSession(int UserId, string UserName);

public interface IManagementSessionValidator
{
    Task<ValidatedManagementSession?> ValidateRefreshCookieAsync(string refreshCookieValue, CancellationToken ct);
}
