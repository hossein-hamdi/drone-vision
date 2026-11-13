using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddOcelot(builder.Environment, MergeOcelotJson.ToMemory);
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();
var servicePrefixes = builder.Configuration.GetSection("Routes").GetChildren()
    .Select(route => route["UpstreamPathTemplate"]?.Split("/{", 2)[0])
    .Where(path => path?.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) == true)
    .Select(path => new PathString(path)).ToArray();
app.Use(async (context, next) =>
{
    var isApi = context.Request.Path.StartsWithSegments("/api");
    var servicePrefix = servicePrefixes.FirstOrDefault(prefix => context.Request.Path.StartsWithSegments(prefix));
    if (isApi && (!servicePrefix.HasValue ||
        (!app.Environment.IsDevelopment() &&
         context.Request.Path.StartsWithSegments(servicePrefix.Add("/swagger")))))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    // Replace client-supplied forwarding headers at the public boundary.
    context.Request.Headers["X-Forwarded-Proto"] = context.Request.Scheme;
    context.Request.Headers["X-Forwarded-For"] = context.Connection.RemoteIpAddress?.ToString();
    await next(context);
});
await app.UseOcelot();
await app.RunAsync();
