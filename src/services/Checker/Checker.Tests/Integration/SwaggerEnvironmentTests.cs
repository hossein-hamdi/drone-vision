using System.Net;

namespace Checker.Tests.Integration;

public sealed class SwaggerEnvironmentTests
{
    [Fact]
    public async Task Swagger_InDevelopment_ShouldServeUiAndOpenApiJsonWithBearerScheme()
    {
        using var factory = new CheckerApiFactory(environmentName: "Development", gatewayPathBase: "/api/checker");
        using var client = factory.CreateClient();

        var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);

        var spec = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, spec.StatusCode);
        var json = await spec.Content.ReadAsStringAsync();
        Assert.Contains("Bearer", json);
        Assert.Contains("/networks/check", json);
    }

    [Fact]
    public async Task Swagger_InProduction_ShouldReturn404()
    {
        using var factory = new CheckerApiFactory(environmentName: "Production");
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/index.html")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }
}
