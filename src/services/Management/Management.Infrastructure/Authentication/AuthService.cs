using Management.Application.Abstractions;
using Management.Application.Abstractions.Authentication;
using Management.Application.Common;
using Management.Application.DTOs;
using Management.Infrastructure.Identity;
using Management.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Management.Infrastructure.Authentication;

public sealed class AuthService(ManagementDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, IUserService userService, JwtTokenService tokens,
    IOptions<JwtOptions> options, TimeProvider clock) : IAuthenticationService
{
    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var user = new ApplicationUser { FullName = request.FullName.Trim(), UserName = request.UserName.Trim() };
        IdentityErrors.EnsureSuccess(await users.CreateAsync(user, request.Password));
        return await userService.GetAsync(user.Id, ct);
    }

    public async Task<TokenPair> LoginAsync(LoginRequest request, bool browser, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(request.UserName.Trim());
        if (user is null || !(await signIn.CheckPasswordSignInAsync(user, request.Password, true)).Succeeded)
            throw ApiException.Unauthorized();
        var now = clock.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = new ApplicationUserToken
        {
            LoginProvider = IdentityTokenProviders.Session,
            Name = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            IsBrowserSession = browser,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(options.Value.RefreshTokenDays)
        };
        db.UserTokens.Add(session);
        await db.SaveChangesAsync(ct);
        var raw = JwtTokenService.CreateRefreshToken();
        db.UserTokens.Add(new ApplicationUserToken
        {
            LoginProvider = IdentityTokenProviders.Refresh,
            Name = JwtTokenService.Hash(raw),
            UserId = user.Id,
            SessionId = session.Id,
            CreatedAtUtc = now,
            ExpiresAtUtc = session.ExpiresAtUtc
        });
        await db.SaveChangesAsync(ct);
        var result = Pair(user.Id, session.Id, raw, session.ExpiresAtUtc.Value);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<TokenPair> RefreshAsync(string token, bool browser, CancellationToken ct)
    {
        var hash = ValidateAndHash(token);
        var sessionId = await db.UserTokens.AsNoTracking()
            .Where(x => x.LoginProvider == IdentityTokenProviders.Refresh && x.Name == hash)
            .Select(x => x.SessionId).SingleOrDefaultAsync(ct) ?? throw ApiException.Unauthorized();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // All rotation/logout writers acquire this lock before reading a token. It works across API instances.
        var session = await db.UserTokens.FromSqlInterpolated(
            $"SELECT * FROM [AspNetUserTokens] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {sessionId} AND [LoginProvider] = {IdentityTokenProviders.Session}")
            .SingleOrDefaultAsync(ct) ?? throw ApiException.Unauthorized();
        var now = clock.GetUtcNow().UtcDateTime;
        if (session.IsBrowserSession != browser || session.RevokedAtUtc is not null || session.ExpiresAtUtc is null || session.ExpiresAtUtc <= now)
            throw ApiException.Unauthorized();
        var previous = await db.UserTokens.SingleAsync(x => x.LoginProvider == IdentityTokenProviders.Refresh &&
            x.Name == hash && x.SessionId == session.Id && x.UserId == session.UserId, ct);
        if (previous.RevokedAtUtc is not null)
        {
            await RevokeSessionAsync(session.Id, now, ct);
            await transaction.CommitAsync(ct); // Persist replay revocation before returning 401.
            throw ApiException.Unauthorized();
        }
        if (previous.ExpiresAtUtc is null || previous.ExpiresAtUtc <= now) throw ApiException.Unauthorized();
        var user = await users.FindByIdAsync(session.UserId.ToString());
        if (user is null || await users.IsLockedOutAsync(user)) throw ApiException.Unauthorized();

        var raw = JwtTokenService.CreateRefreshToken();
        previous.RevokedAtUtc = now;
        db.UserTokens.Add(new ApplicationUserToken
        {
            LoginProvider = IdentityTokenProviders.Refresh,
            Name = JwtTokenService.Hash(raw),
            UserId = session.UserId,
            SessionId = session.Id,
            CreatedAtUtc = now,
            ExpiresAtUtc = session.ExpiresAtUtc
        });
        await db.SaveChangesAsync(ct);
        var result = Pair(session.UserId, session.Id, raw, session.ExpiresAtUtc.Value);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task LogoutAsync(string token, bool browser, CancellationToken ct)
    {
        var hash = ValidateAndHash(token);
        var sessionId = await db.UserTokens.AsNoTracking()
            .Where(x => x.LoginProvider == IdentityTokenProviders.Refresh && x.Name == hash)
            .Select(x => x.SessionId).SingleOrDefaultAsync(ct) ?? throw ApiException.Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await db.UserTokens.FromSqlInterpolated(
            $"SELECT * FROM [AspNetUserTokens] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {sessionId} AND [LoginProvider] = {IdentityTokenProviders.Session}")
            .SingleOrDefaultAsync(ct) ?? throw ApiException.Unauthorized();
        var now = clock.GetUtcNow().UtcDateTime;
        var current = await db.UserTokens.AsNoTracking().SingleAsync(x => x.LoginProvider == IdentityTokenProviders.Refresh &&
            x.Name == hash && x.SessionId == session.Id && x.UserId == session.UserId, ct);
        if (session.IsBrowserSession != browser || session.RevokedAtUtc is not null ||
            session.ExpiresAtUtc is null || session.ExpiresAtUtc <= now || current.ExpiresAtUtc is null || current.ExpiresAtUtc <= now)
            throw ApiException.Unauthorized();
        await RevokeSessionAsync(sessionId, now, ct);
        await transaction.CommitAsync(ct);
        if (current.RevokedAtUtc is not null) throw ApiException.Unauthorized();
    }

    private Task<int> RevokeSessionAsync(int sessionId, DateTime now, CancellationToken ct) =>
        db.UserTokens.Where(x => x.Id == sessionId && x.LoginProvider == IdentityTokenProviders.Session)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now), ct);

    private TokenPair Pair(int userId, int sessionId, string raw, DateTime expiry)
    {
        var access = tokens.CreateAccessToken(userId, sessionId);
        return new(access.Value, access.ExpiresIn, raw, DateTime.SpecifyKind(expiry, DateTimeKind.Utc));
    }

    private static string ValidateAndHash(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 86 || token.Any(c =>
            !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) throw ApiException.Unauthorized();
        return JwtTokenService.Hash(token);
    }
}
