namespace EnterpriseDataManager.IntegrationTests.Security;

using System.Net;
using System.Text;
using System.Text.Json;
using EnterpriseDataManager.IntegrationTests.Helpers;
using FluentAssertions;

public class JwtSecurityTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public JwtSecurityTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Request_WithNoToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/archive-plans");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithExpiredToken_Returns401()
    {
        var expiredToken = _factory.GenerateJwtToken(expires: DateTime.UtcNow.AddMinutes(-5));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync("/api/v1/archive-plans");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithTamperedPayload_Returns401()
    {
        // Generate a valid token then replace its payload with an attacker-controlled one
        var validToken = _factory.GenerateJwtToken();
        var parts = validToken.Split('.');

        parts.Length.Should().Be(3, "a JWT must have exactly 3 parts");

        // Craft a malicious payload claiming Admin role
        var maliciousPayload = new
        {
            sub = "attacker",
            role = "Admin",
            exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        };
        var maliciousJson = JsonSerializer.Serialize(maliciousPayload);
        var tamperedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(maliciousJson))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // Reconstruct with tampered payload but original header and signature
        var tamperedToken = $"{parts[0]}.{tamperedPayload}.{parts[2]}";

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tamperedToken);

        var response = await client.GetAsync("/api/v1/archive-plans");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a token with a tampered payload should be rejected because the signature no longer matches");
    }

    [Fact]
    public async Task Request_WithWrongSignature_Returns401()
    {
        var wrongKeyToken = _factory.GenerateJwtToken(
            signingKey: "wrong-signing-key-totally-different-32c");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", wrongKeyToken);

        var response = await client.GetAsync("/api/v1/archive-plans");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a token signed with an incorrect key must be rejected");
    }

    [Fact]
    public async Task Request_WithNoneAlgorithm_Returns401()
    {
        // Craft a JWT with alg=none — a classic JWT attack vector
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var payload = new
        {
            sub = "test-user-id",
            role = "Admin",
            iss = "EnterpriseDataManager",
            aud = "EnterpriseDataManager",
            exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        };
        var payloadJson = JsonSerializer.Serialize(payload);
        var encodedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // alg=none tokens have an empty signature segment
        var noneAlgToken = $"{header}.{encodedPayload}.";

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", noneAlgToken);

        var response = await client.GetAsync("/api/v1/archive-plans");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the server must reject tokens with alg=none regardless of payload claims");
    }

    [Fact]
    public async Task Request_WithValidToken_Returns200OrHigher_NotUnauthorized()
    {
        var client = _factory.GetAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/archive-plans");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "a valid, properly signed JWT should be accepted");
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "the Admin role should have access to archive-plans");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns400Or401()
    {
        var client = _factory.CreateClient();

        var payload = JsonSerializer.Serialize(new
        {
            email = "notexist@example.com",
            password = "WrongPassword123!"
        });

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/auth/token", content);

        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized },
            "invalid credentials must not produce a successful response");
    }
}
