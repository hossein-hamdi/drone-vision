using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Management.API.Authorization;
using Management.API.ExceptionHandling;
using Management.API.OpenApi;
using Management.Application.Authorization;
using Management.Infrastructure;
using Management.Infrastructure.Authentication;
using Management.Infrastructure.Persistence;
using Management.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IProblemDetailsWriter, ApiProblemDetailsWriter>();
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
}).ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
    new ApiProblemResult(new ValidationProblemDetails(context.ModelState)
    {
        Status = 400,
        Title = "Validation Failed",
        Detail = "One or more validation errors occurred."
    }));
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
    ApiProblems.Enrich(context.HttpContext, context.ProblemDetails));
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddExceptionHandler<DatabaseConflictExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "__Host-DroneVision.Csrf";
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.HttpOnly = true;
    o.Cookie.Path = "/";
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        var settings = jwt.Value;
        bearer.MapInboundClaims = false;
        bearer.SaveToken = false;
        bearer.IncludeErrorDetails = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha512],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub"
        };
        bearer.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (!int.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId) || userId <= 0 ||
                    !int.TryParse(context.Principal?.FindFirst("sid")?.Value, out var sessionId) || sessionId <= 0)
                {
                    context.Fail("Invalid subject or session.");
                    return;
                }
                var db = context.HttpContext.RequestServices.GetRequiredService<ManagementDbContext>();
                var clock = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
                var now = clock.GetUtcNow();
                if (!await db.UserTokens.AsNoTracking().AnyAsync(s => s.Id == sessionId && s.UserId == userId &&
                    s.LoginProvider == IdentityTokenProviders.Session && s.RevokedAtUtc == null && s.ExpiresAtUtc > now.UtcDateTime &&
                    db.Users.Any(user => user.Id == userId &&
                        (!user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= now)), context.HttpContext.RequestAborted))
                    context.Fail("The authentication session is no longer active.");
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await ApiProblems.WriteAsync(context.HttpContext, new ProblemDetails
                {
                    Status = 401,
                    Title = "Unauthorized",
                    Detail = "A valid access token is required."
                });
            },
            OnForbidden = context => ApiProblems.WriteAsync(context.HttpContext, new ProblemDetails
            {
                Status = 403,
                Title = "Forbidden",
                Detail = "You do not have permission to perform this action."
            })
        };
    });
builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    foreach (var permission in Permissions.All)
        o.AddPolicy(permission, p => p.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
});
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("credentials", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Clamp(context.RequestServices.GetRequiredService<IConfiguration>()
                .GetValue("RateLimiting:AuthenticationRequestsPerMinute", 20), 1, 10000),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    o.OnRejected = (context, _) => new ValueTask(ApiProblems.WriteAsync(context.HttpContext, new ProblemDetails
    {
        Status = 429,
        Title = "Too Many Requests",
        Detail = "Too many authentication attempts. Try again shortly."
    }));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "DroneVision Management API", Version = "v1" });
    o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Management.API.xml"));
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Sign in with POST /auth/login; Swagger automatically uses the access token for protected requests. Browser refresh tokens use an HttpOnly cookie."
    });
    o.OperationFilter<SecurityOperationFilter>();
});

var app = builder.Build();
var gatewayPathBase = builder.Configuration["Gateway:PathBase"];
if (!string.IsNullOrEmpty(gatewayPathBase)) app.UsePathBase(gatewayPathBase);
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
    await ApiProblems.WriteAsync(context.HttpContext, new ProblemDetails { Status = context.HttpContext.Response.StatusCode }));
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(o => o.PreSerializeFilters.Add((document, request) =>
        document.Servers = [new OpenApiServer { Url = request.PathBase.HasValue ? request.PathBase.Value! : "/" }]));
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("v1/swagger.json", "Management v1");
        // Swagger embeds interceptor bodies in JavaScript strings and parses them as synchronous functions.
        o.UseRequestInterceptor("""
        (request) => {
            const url = new URL(request.url, window.location.origin);
            const basePath = window.location.pathname.split('/swagger')[0];
            const path = url.pathname.slice(basePath.length);
            if (url.origin === window.location.origin &&
                ['/auth/csrf', '/auth/register', '/auth/login', '/auth/refresh', '/auth/logout'].includes(path)) {
                delete request.headers.Authorization;
                delete request.headers.authorization;
                if (request.method.toUpperCase() === 'POST' && !request.headers['X-Auth-Transport'] &&
                    ['/auth/login', '/auth/refresh', '/auth/logout'].includes(path)) {
                    return fetch(basePath + '/auth/csrf', { credentials: 'same-origin' })
                        .then(response => response.json())
                        .then(csrf => {
                            request.headers['X-CSRF-TOKEN'] = csrf.requestToken;
                            request.credentials = 'same-origin';
                            return request;
                        });
                }
            }
            return request;
        }
        """.ReplaceLineEndings(" "));
        o.UseResponseInterceptor("""
        (response) => {
            const url = new URL(response.url, window.location.origin);
            const basePath = window.location.pathname.split('/swagger')[0];
            const path = url.pathname.slice(basePath.length);
            if (url.origin !== window.location.origin) return response;
            const tokenResponse = ['/auth/login', '/auth/refresh'].includes(path);
            if (tokenResponse && response.status === 200 && response.obj && response.obj.accessToken) {
                window.ui.preauthorizeApiKey('Bearer', response.obj.accessToken);
            } else if (path === '/auth/logout' || (tokenResponse && response.status === 401)) {
                window.ui.authActions.logout(['Bearer']);
            }
            return response;
        }
        """.ReplaceLineEndings(" "));
    });
}
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<IdentityBootstrap>().InitializeAsync();
}
await app.RunAsync();

public partial class Program;
