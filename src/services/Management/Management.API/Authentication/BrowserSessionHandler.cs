using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Management.API.ExceptionHandling;
using Management.Infrastructure.Authentication;
using Management.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Management.API.Authentication;

internal sealed class BrowserSessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, ManagementDbContext db, TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "BrowserSession";
    internal const string CookieName = "__Secure-DroneVision.Refresh";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(token)) return AuthenticateResult.NoResult();
        if (token.Length != 86 || token.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            return InvalidSession();

        var hash = JwtTokenService.Hash(token);
        var now = clock.GetUtcNow();
        var session = await (
            from refresh in db.UserTokens.AsNoTracking()
            join active in db.UserTokens.AsNoTracking() on refresh.SessionId equals (int?)active.Id
            join user in db.Users.AsNoTracking() on active.UserId equals user.Id
            where refresh.LoginProvider == IdentityTokenProviders.Refresh && refresh.Name == hash &&
                refresh.UserId == active.UserId && refresh.RevokedAtUtc == null && refresh.ExpiresAtUtc > now.UtcDateTime &&
                active.LoginProvider == IdentityTokenProviders.Session && active.IsBrowserSession == true &&
                active.RevokedAtUtc == null && active.ExpiresAtUtc > now.UtcDateTime &&
                (!user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= now)
            select new { UserId = user.Id, SessionId = active.Id }
        ).SingleOrDefaultAsync(Context.RequestAborted);
        if (session is null) return InvalidSession();

        var userId = session.UserId.ToString(CultureInfo.InvariantCulture);
        var identity = new ClaimsIdentity([
            new Claim("sub", userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("sid", session.SessionId.ToString(CultureInfo.InvariantCulture))
        ], Scheme.Name, "sub", ClaimTypes.Role);
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

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        ApiProblems.WriteAsync(Context, new ProblemDetails
        {
            Status = 401,
            Title = "Unauthorized",
            Detail = "A valid browser session is required."
        });

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ApiProblems.WriteAsync(Context, new ProblemDetails
        {
            Status = 403,
            Title = "Forbidden",
            Detail = "You do not have permission to perform this action."
        });
}
