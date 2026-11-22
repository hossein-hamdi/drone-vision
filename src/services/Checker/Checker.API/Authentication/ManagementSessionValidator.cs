using System.Net.Http.Json;
using Checker.Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace Checker.API.Authentication;

internal sealed class ManagementSessionValidator(
    HttpClient httpClient,
    IOptions<ManagementSessionOptions> options,
    ILogger<ManagementSessionValidator> logger) : IManagementSessionValidator
{
    private const string RefreshCookieName = "__Secure-DroneVision.Refresh";

    public async Task<ValidatedManagementSession?> ValidateRefreshCookieAsync(string refreshCookieValue, CancellationToken ct)
    {
        var baseUrl = options.Value.BaseUrl?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/auth/me");
        request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshCookieValue}");
        request.Headers.Add("X-Forwarded-Proto", "https");

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var user = await response.Content.ReadFromJsonAsync<ManagementAuthMeDto>(cancellationToken: ct);
            if (user is null || user.Id <= 0 || string.IsNullOrWhiteSpace(user.UserName))
            {
                return null;
            }

            return new ValidatedManagementSession(user.Id, user.UserName);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Management browser session verification failed.");
            return null;
        }
    }

    private sealed record ManagementAuthMeDto(int Id, string UserName);
}
