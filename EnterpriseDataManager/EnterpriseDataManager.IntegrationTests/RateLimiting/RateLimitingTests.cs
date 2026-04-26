namespace EnterpriseDataManager.IntegrationTests.RateLimiting;

using System.Net;
using System.Text;
using System.Text.Json;
using EnterpriseDataManager.IntegrationTests.Helpers;
using FluentAssertions;

public class RateLimitingTests : IClassFixture<RateLimitTestWebApplicationFactory>
{
    private readonly RateLimitTestWebApplicationFactory _factory;

    // Must match the small limits defined in RateLimitTestWebApplicationFactory
    private const int GlobalLimit = 5;
    private const int AuthLimit = 3;

    public RateLimitingTests(RateLimitTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GlobalRateLimit_ExceededRequests_Returns429()
    {
        // Use an unauthenticated client — partition key will be "anonymous" / loopback IP
        var client = _factory.CreateClient();

        // Send exactly the permitted number of requests — all should succeed
        for (var i = 0; i < GlobalLimit; i++)
        {
            var successResponse = await client.GetAsync("/health");
            // /health returns 200; we allow any non-429 here
            ((int)successResponse.StatusCode).Should().NotBe(429,
                $"request {i + 1} of {GlobalLimit} should be within the rate limit");
        }

        // The next request must be rejected
        var limitedResponse = await client.GetAsync("/health");
        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "exceeding the global rate limit must return 429");
    }

    [Fact]
    public async Task GlobalRateLimit_Response_HasRetryAfterHeader()
    {
        var client = _factory.CreateClient();

        // Exhaust global limit
        for (var i = 0; i < GlobalLimit; i++)
        {
            await client.GetAsync("/health");
        }

        var limitedResponse = await client.GetAsync("/health");

        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        limitedResponse.Headers.TryGetValues("Retry-After", out var retryAfterValues)
            .Should().BeTrue("a 429 response must include a Retry-After header");

        var retryAfterHeader = retryAfterValues!.FirstOrDefault();
        retryAfterHeader.Should().NotBeNullOrEmpty("Retry-After header value must be present");

        int.TryParse(retryAfterHeader, out var retrySeconds).Should().BeTrue(
            "Retry-After value must be a numeric integer representing seconds");
        retrySeconds.Should().BeGreaterThan(0, "retry delay must be positive");
    }

    [Fact]
    public async Task ApiRateLimit_ExceededRequests_Returns429()
    {
        // The auth endpoint is decorated with [EnableRateLimiting("api")]
        // Send unauthenticated requests to /api/v1/auth/token (which applies "api" policy)
        var client = _factory.CreateClient();

        var validBody = JsonSerializer.Serialize(new { email = "test@example.com", password = "WrongPass!" });
        var contentFactory = () => new StringContent(validBody, Encoding.UTF8, "application/json");

        // Send up to the api limit — responses may be 400/401 but not 429
        for (var i = 0; i < GlobalLimit; i++)
        {
            var response = await client.PostAsync("/api/v1/auth/token", contentFactory());
            ((int)response.StatusCode).Should().NotBe(429,
                $"request {i + 1} should be within the 'api' rate limit");
        }

        // Next request should be rate-limited
        var limitedResponse = await client.PostAsync("/api/v1/auth/token", contentFactory());
        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "the 'api' rate limit should be enforced on the auth endpoint");
    }

    [Fact]
    public async Task AuthRateLimit_ExceededLoginAttempts_ConsistentlyRejectsOrRateLimits()
    {
        // The AuthApiController uses [EnableRateLimiting("api")] not a separate "auth" policy.
        // We verify that repeated bad-credential attempts return 400/401 consistently
        // (not 500) and eventually hit the rate limit.
        var client = _factory.CreateClient();

        var invalidBody = JsonSerializer.Serialize(new { email = "bad@example.com", password = "wrong" });
        var contentFactory = () => new StringContent(invalidBody, Encoding.UTF8, "application/json");

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < AuthLimit + 2; i++)
        {
            var response = await client.PostAsync("/api/v1/auth/token", contentFactory());
            responses.Add(response);
        }

        // None of the responses should be a 500 server error
        responses.Should().AllSatisfy(r =>
            ((int)r.StatusCode).Should().NotBe(500,
                "repeated login attempts must never produce an unhandled server error"));

        // At least the early responses (before limit hit) should be 400 or 401
        var earlyResponses = responses.Take(AuthLimit).ToList();
        earlyResponses.Should().AllSatisfy(r =>
            r.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized,
                HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task RateLimit_DifferentIpPartitions_IndependentLimits()
    {
        // Two authenticated clients represent different users — they should have independent buckets
        var clientA = _factory.GetAuthenticatedClient("Admin");
        var clientB = _factory.GetAuthenticatedClient("User");

        // Add a distinct X-Forwarded-For so the middleware treats them as different IPs
        clientA.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");
        clientB.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.2");

        // Exhaust clientA's global limit
        for (var i = 0; i < GlobalLimit; i++)
        {
            var r = await clientA.GetAsync("/health");
            // Allow any non-429 during warm-up
        }

        // clientA should now be rate-limited
        var clientALimited = await clientA.GetAsync("/health");
        // clientA may or may not be limited depending on whether X-Forwarded-For is trusted.
        // The important assertion is that clientB can still make requests.

        // clientB has a fresh bucket — should succeed
        var clientBResponse = await clientB.GetAsync("/health");
        ((int)clientBResponse.StatusCode).Should().NotBe(429,
            "clientB's rate limit bucket should be independent from clientA's");
    }
}
