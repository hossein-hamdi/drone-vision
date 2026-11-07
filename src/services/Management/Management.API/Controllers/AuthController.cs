using Management.Application.Abstractions.Authentication;
using Management.Application.Common;
using Management.Application.DTOs;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace Management.API.Controllers;

[Route("auth")]
[Tags("Auth")]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(IAuthenticationService authentication, IAntiforgery antiforgery) : ApiController
{
    private const string RefreshCookie = "__Secure-DroneVision.Refresh";

    /// <summary>Get the anonymous CSRF request token and cookie before browser authentication requests.</summary>
    [HttpGet("csrf")]
    [ProducesResponseType<CsrfResponse>(200)]
    public ActionResult<CsrfResponse> Csrf() => new CsrfResponse(antiforgery.GetAndStoreTokens(HttpContext).RequestToken!);

    /// <summary>Register a user without roles or administrative permissions.</summary>
    [HttpPost("register")]
    [EnableRateLimiting("credentials")]
    [ProducesResponseType<UserDto>(201)]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = await authentication.RegisterAsync(request, ct);
        return Created($"/users/{user.Id}", user);
    }

    /// <summary>Sign in. Browser transport sets an HttpOnly refresh cookie; native transport returns the secret in JSON.</summary>
    /// <remarks>Default browser transport requires X-CSRF-TOKEN from GET /auth/csrf. Native clients explicitly set X-Auth-Transport: native, omit browser headers and cookies, and use secure OS token storage.</remarks>
    [HttpPost("login")]
    [EnableRateLimiting("credentials")]
    [ProducesResponseType<AuthenticationResponse>(200)]
    public async Task<ActionResult<AuthenticationResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var browser = await ValidateTransportAsync();
        return Respond(await authentication.LoginAsync(request, browser, ct), browser);
    }

    /// <summary>Rotate a refresh token. A reused token revokes its entire session.</summary>
    /// <remarks>Browser: send the refresh cookie and X-CSRF-TOKEN, with no refreshToken JSON property. Native: send X-Auth-Transport: native and refreshToken in JSON. Access JWT is unnecessary; expired JWTs do not get silently refreshed.</remarks>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthenticationResponse>(200)]
    public async Task<ActionResult<AuthenticationResponse>> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshRequest? request, CancellationToken ct)
    {
        var browser = await ValidateTransportAsync();
        try { return Respond(await authentication.RefreshAsync(ReadToken(request, browser), browser, ct), browser); }
        catch (ApiException e) when (e.StatusCode == 401)
        {
            if (browser) Response.Cookies.Delete(RefreshCookie, CookieOptions());
            throw;
        }
    }

    /// <summary>Revoke the refresh-token session and all its access tokens. Works when the access JWT has expired.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshRequest? request, CancellationToken ct)
    {
        var browser = await ValidateTransportAsync();
        try { await authentication.LogoutAsync(ReadToken(request, browser), browser, ct); }
        finally { if (browser) Response.Cookies.Delete(RefreshCookie, CookieOptions()); }
        return NoContent();
    }

    private async Task<bool> ValidateTransportAsync()
    {
        var transport = Request.Headers["X-Auth-Transport"].ToString();
        if (transport.Length > 0 && transport != "native") throw ApiException.Validation("transport", "Unknown authentication transport.");
        if (transport == "native")
        {
            if (Request.Headers.ContainsKey("Origin") || Request.Headers.ContainsKey("Sec-Fetch-Site") || Request.Cookies.ContainsKey(RefreshCookie))
                throw ApiException.Validation("transport", "Native transport cannot be combined with browser headers or a refresh cookie.");
            return false;
        }
        try { await antiforgery.ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException)
        {
            throw ApiException.Validation("csrf", "A valid CSRF cookie and X-CSRF-TOKEN header are required.");
        }
        return true;
    }

    private string ReadToken(RefreshRequest? request, bool browser)
    {
        if (browser && request?.RefreshToken is not null)
            throw ApiException.Validation("refreshToken", "Browser refresh tokens must be sent using the HttpOnly cookie.");
        return (browser ? Request.Cookies[RefreshCookie] : request?.RefreshToken) ?? throw ApiException.Unauthorized();
    }

    private AuthenticationResponse Respond(TokenPair pair, bool browser)
    {
        if (browser) Response.Cookies.Append(RefreshCookie, pair.RefreshToken,
            CookieOptions(new DateTimeOffset(pair.RefreshTokenExpiresAt)));
        return new(pair.AccessToken, "Bearer", pair.ExpiresIn, pair.RefreshTokenExpiresAt, browser ? null : pair.RefreshToken);
    }

    private static CookieOptions CookieOptions(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/auth",
        IsEssential = true,
        Expires = expires
    };

    public sealed record CsrfResponse(string RequestToken);
}
