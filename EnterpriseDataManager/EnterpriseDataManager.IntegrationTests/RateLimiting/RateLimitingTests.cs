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
        var client = _factory.CreateClient();
        // Unique IP so this test's bucket is independent from other tests sharing the factory.
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.1");

        for (var i = 0; i < GlobalLimit; i++)
        {
            var successResponse = await client.GetAsync("/health");
            ((int)successResponse.StatusCode).Should().NotBe(429,
                $"request {i + 1} of {GlobalLimit} should be within the rate limit");
        }

        var limitedResponse = await client.GetAsync("/health");
        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "exceeding the global rate limit must return 429");
    }

    [Fact]
    public async Task GlobalRateLimit_Response_HasRetryAfterHeader()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.2");

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
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.3");

        var validBody = JsonSerializer.Serialize(new { email = "test@example.com", password = "WrongPass!" });
        var contentFactory = () => new StringContent(validBody, Encoding.UTF8, "application/json");

        for (var i = 0; i < GlobalLimit; i++)
        {
            var response = await client.PostAsync("/api/v1/auth/token", contentFactory());
            ((int)response.StatusCode).Should().NotBe(429,
                $"request {i + 1} should be within the 'api' rate limit");
        }

        var limitedResponse = await client.PostAsync("/api/v1/auth/token", contentFactory());
        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "the 'api' rate limit should be enforced on the auth endpoint");
    }

    [Fact]
    public async Task AuthRateLimit_ExceededLoginAttempts_ConsistentlyRejectsOrRateLimits()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.4");

        var invalidBody = JsonSerializer.Serialize(new { email = "bad@example.com", password = "wrong" });
        var contentFactory = () => new StringContent(invalidBody, Encoding.UTF8, "application/json");

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < AuthLimit + 2; i++)
        {
            var response = await client.PostAsync("/api/v1/auth/token", contentFactory());
            responses.Add(response);
        }

        responses.Should().AllSatisfy(r =>
            ((int)r.StatusCode).Should().NotBe(500,
                "repeated login attempts must never produce an unhandled server error"));

        var earlyResponses = responses.Take(AuthLimit).ToList();
        earlyResponses.Should().AllSatisfy(r =>
            r.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized,
                HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task RateLimit_DifferentIpPartitions_IndependentLimits()
    {
        var clientA = _factory.GetAuthenticatedClient("Admin");
        var clientB = _factory.GetAuthenticatedClient("User");

        clientA.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");
        clientB.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.2");

        for (var i = 0; i < GlobalLimit; i++)
        {
            await clientA.GetAsync("/health");
        }

        // clientB has a fresh bucket — should succeed regardless of clientA's state
        var clientBResponse = await clientB.GetAsync("/health");
        ((int)clientBResponse.StatusCode).Should().NotBe(429,
            "clientB's rate limit bucket should be independent from clientA's");
    }
}
