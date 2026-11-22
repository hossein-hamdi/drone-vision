using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Checker.API.ExceptionHandling;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Checker.API.Authentication;

public sealed class BrowserSessionHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IManagementSessionValidator sessionValidator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "BrowserSession";
    public const string CookieName = "__Secure-DroneVision.Refresh";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        // Matches Management.API BrowserSessionHandler 512-bit Base64Url format guard (86 chars).
        if (token.Length != 86 || token.Any(static c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
        {
            return InvalidSession();
        }

        var session = await sessionValidator.ValidateRefreshCookieAsync(token, Context.RequestAborted);
        if (session is null)
        {
            return InvalidSession();
        }

        var userId = session.UserId.ToString(CultureInfo.InvariantCulture);
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", userId),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, session.UserName)
            ],
            Scheme.Name,
            "sub",
            ClaimTypes.Role);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private AuthenticateResult InvalidSession()
    {
        Response.OnStarting(() =>
        {
            Response.Cookies.Delete(CookieName, new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict
            });
            return Task.CompletedTask;
        });

        return AuthenticateResult.Fail("The browser session is no longer active.");
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return ApiProblems.WriteAsync(Context, new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "A valid access token or browser session is required."
        });
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ApiProblems.WriteAsync(Context, new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = "You do not have permission to perform this action."
        });
}
