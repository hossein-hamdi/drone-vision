using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Checker.API.Authentication;
using Checker.API.ExceptionHandling;
using Checker.API.OpenApi;
using Checker.Infrastructure;
using Checker.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCheckerServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IProblemDetailsWriter, ApiProblemDetailsWriter>();

builder.Services.AddHttpClient<IManagementSessionValidator, ManagementSessionValidator>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<ManagementSessionOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
        new ApiProblemResult(new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Failed",
            Detail = "One or more validation errors occurred."
        }));

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
    ApiProblems.Enrich(context.HttpContext, context.ProblemDetails));
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddAuthentication("BrowserOrBearer")
    .AddPolicyScheme("BrowserOrBearer", null, o => o.ForwardDefaultSelector = context =>
        context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? JwtBearerDefaults.AuthenticationScheme
            : BrowserSessionHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BrowserSessionHandler>(BrowserSessionHandler.SchemeName, _ => { })
    .AddJwtBearer();

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
            OnTokenValidated = context =>
            {
                var sub = context.Principal?.FindFirst("sub")?.Value;
                var sid = context.Principal?.FindFirst("sid")?.Value;
                var jti = context.Principal?.FindFirst("jti")?.Value;

                if (!int.TryParse(sub, out var userId) || userId <= 0 ||
                    !int.TryParse(sid, out var sessionId) || sessionId <= 0 ||
                    string.IsNullOrWhiteSpace(jti))
                {
                    context.Fail("Invalid token identity claims.");
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await ApiProblems.WriteAsync(context.HttpContext, new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "A valid access token or browser session is required."
                });
            },
            OnForbidden = context => ApiProblems.WriteAsync(context.HttpContext, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "You do not have permission to perform this action."
            })
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddRateLimiter(o =>
{
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }

        var limit = Math.Clamp(
            context.RequestServices.GetRequiredService<IConfiguration>()
                .GetValue("RateLimiting:LookupRequestsPerMinute", 60),
            1,
            10000);

        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    o.OnRejected = (context, _) => new ValueTask(ApiProblems.WriteAsync(context.HttpContext, new ProblemDetails
    {
        Status = StatusCodes.Status429TooManyRequests,
        Title = "Too Many Requests",
        Detail = "Rate limit exceeded. Try again shortly."
    }));
});

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DroneVision Network Checker API",
        Version = "v1",
        Description = "Identifies drone wireless stations by BSSID/SSID and scans nearby Wi-Fi networks to return only drone stations."
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, "Checker.API.xml");
    if (File.Exists(xmlPath))
    {
        o.IncludeXmlComments(xmlPath);
    }

    o.AddSecurityDefinition("BrowserSession", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = BrowserSessionHandler.CookieName,
        Description = "Browser login sets this Secure, HttpOnly cookie automatically on same-origin requests."
    });

    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Provide a Management-issued JWT access token (Authorization: Bearer <token>), or authenticate via browser session cookie."
    });
    o.OperationFilter<SecurityOperationFilter>();
});

var app = builder.Build();

// Trigger startup validation eagerly even before first request.
_ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
_ = app.Services.GetRequiredService<IOptions<ManagementSessionOptions>>().Value;

var gatewayPathBase = builder.Configuration["Gateway:PathBase"];
if (!string.IsNullOrEmpty(gatewayPathBase))
{
    app.UsePathBase(gatewayPathBase);
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
    await ApiProblems.WriteAsync(
        context.HttpContext,
        new ProblemDetails { Status = context.HttpContext.Response.StatusCode }));

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next(context);
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(o => o.PreSerializeFilters.Add((document, request) =>
        document.Servers = [new OpenApiServer { Url = request.PathBase.HasValue ? request.PathBase.Value! : "/" }]));
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("v1/swagger.json", "Checker v1");
        o.UseRequestInterceptor("""
        (request) => {
            const url = new URL(request.url, window.location.origin);
            if (url.origin === window.location.origin) {
                request.credentials = 'same-origin';
            }
            return request;
        }
        """.ReplaceLineEndings(" "));
    });
}
else
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    });
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = static (context, report) =>
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { status = report.Status.ToString() }));
    }
}).AllowAnonymous().DisableRateLimiting();

app.MapControllers();

await app.RunAsync();

public partial class Program;
