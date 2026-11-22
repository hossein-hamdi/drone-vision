using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Checker.Tests.Integration;

public sealed class NetworkCheckAuthSecurityTests : IClassFixture<CheckerApiFactory>
{
    private readonly CheckerApiFactory _factory;

    public NetworkCheckAuthSecurityTests(CheckerApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Check_WithoutToken_ShouldReturn401ProblemDetailsWithBearerChallenge()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/networks/check?bssid=60:60:1F:12:34:56");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("IDX", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Check_WithExpiredTokenZeroClockSkew_ShouldReturn401()
    {
        using var client = _factory.CreateClient();
        var token = _factory.CreateToken(expiresUtc: DateTime.UtcNow.AddSeconds(-5));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/networks/check?bssid=60:60:1F:12:34:56");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Check_WithWrongIssuerOrAudience_ShouldReturn401()
    {
        using var client = _factory.CreateClient();

        var wrongIssuer = _factory.CreateToken(issuer: "EvilIssuer");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", wrongIssuer);
        var issuerResponse = await client.GetAsync("/networks/check?bssid=60:60:1F:12:34:56");
        Assert.Equal(HttpStatusCode.Unauthorized, issuerResponse.StatusCode);

        var wrongAudience = _factory.CreateToken(audience: "WrongAudience");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", wrongAudience);
        var audienceResponse = await client.GetAsync("/networks/check?bssid=60:60:1F:12:34:56");
        Assert.Equal(HttpStatusCode.Unauthorized, audienceResponse.StatusCode);
    }

    [Fact]
    public async Task Check_WithHs256OrWrongKeyOrAlgNone_ShouldReturn401()
    {
        using var client = _factory.CreateClient();

        var hs256 = _factory.CreateToken(algorithm: SecurityAlgorithms.HmacSha256);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hs256);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/networks/check?bssid=60:60:1F:12:34:56")).StatusCode);

        var otherKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var wrongSig = _factory.CreateToken(signingKeyBase64: otherKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", wrongSig);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/networks/check?bssid=60:60:1F:12:34:56")).StatusCode);

        var algNoneHeader = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new
        {
            sub = "1",
            sid = "1",
            jti = "abc",
            iss = CheckerApiFactory.DefaultIssuer,
            aud = CheckerApiFactory.DefaultAudience,
            exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()
        }, (JsonSerializerOptions?)null));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{algNoneHeader}.{payload}.");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/networks/check?bssid=60:60:1F:12:34:56")).StatusCode);
    }

    [Theory]
    [InlineData(null, "7", "jti-1")]
    [InlineData("42", null, "jti-1")]
    [InlineData("42", "7", null)]
    [InlineData("0", "7", "jti-1")]
    [InlineData("abc", "7", "jti-1")]
    [InlineData("42", "0", "jti-1")]
    public async Task Check_WithMissingOrInvalidIdentityClaims_ShouldReturn401(string? sub, string? sid, string? jti)
    {
        using var client = _factory.CreateClient();
        var token = _factory.CreateToken(sub: sub, sid: sid, jti: jti);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/networks/check?bssid=60:60:1F:12:34:56");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Check_WithValidManagementToken_ShouldSucceed()
    {
        using var client = _factory.CreateClient();
        var token = _factory.CreateToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/networks/check?bssid=60:60:1F:12:34:56");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
