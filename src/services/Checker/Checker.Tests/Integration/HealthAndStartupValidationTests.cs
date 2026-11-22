using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Checker.Tests.Integration;

public sealed class HealthAndStartupValidationTests
{
    [Fact]
    public async Task HealthEndpoint_ShouldAllowAnonymousAndBypassRateLimiter()
    {
        using var factory = new CheckerApiFactory(rateLimitPerMinute: 1);
        using var client = factory.CreateClient();

        for (var i = 0; i < 4; i++)
        {
            var response = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.Equal("Healthy", doc!.RootElement.GetProperty("status").GetString());
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("not-base64-!!!")]
    public void Startup_WithWeakOrMissingJwtSigningKey_ShouldFailFast(string invalidKey)
    {
        using var factory = new CheckerApiFactory(signingKeyBase64: invalidKey);

        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}
