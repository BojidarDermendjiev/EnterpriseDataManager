namespace EnterpriseDataManager.IntegrationTests.Security;

using System.Net;
using System.Text;
using System.Text.Json;
using EnterpriseDataManager.IntegrationTests.Helpers;
using FluentAssertions;

public class InputValidationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public InputValidationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public static IEnumerable<object[]> SqlInjectionPayloads => new[]
    {
        new object[] { "' OR 1=1--" },
        new object[] { "'; DROP TABLE ArchivePlans;--" },
        new object[] { "1 UNION SELECT * FROM Users--" },
        new object[] { "admin'--" },
        new object[] { "' OR 'x'='x" },
        new object[] { "1; SELECT SLEEP(5)--" }
    };

    public static IEnumerable<object[]> XssPayloads => new[]
    {
        new object[] { "<script>alert(1)</script>" },
        new object[] { "<img src=x onerror=alert(1)>" },
        new object[] { "javascript:alert(1)" },
        new object[] { "<svg onload=alert(1)>" },
        new object[] { "'\"><script>alert(1)</script>" }
    };

    public static IEnumerable<object[]> PathTraversalPayloads => new[]
    {
        new object[] { "../../../etc/passwd" },
        new object[] { "..%2F..%2Fetc%2Fpasswd" },
        new object[] { "....//....//etc/passwd" },
        new object[] { "%2e%2e%2f%2e%2e%2fetc%2fpasswd" }
    };

    [Theory]
    [MemberData(nameof(SqlInjectionPayloads))]
    public async Task SqlInjection_InQueryParameter_DoesNotReturn500(string payload)
    {
        var client = _factory.GetAuthenticatedClient();

        var encodedPayload = Uri.EscapeDataString(payload);
        var response = await client.GetAsync($"/api/v1/archive-plans?search={encodedPayload}");

        ((int)response.StatusCode).Should().NotBe(500,
            $"SQL injection payload in query param should not cause an unhandled server error. Payload: {payload}");
    }

    [Theory]
    [MemberData(nameof(SqlInjectionPayloads))]
    public async Task SqlInjection_InJsonBody_DoesNotReturn500(string payload)
    {
        var client = _factory.GetAuthenticatedClient();

        var body = JsonSerializer.Serialize(new
        {
            name = payload,
            sourcePath = "/tmp"
        });

        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/archive-plans", content);

        ((int)response.StatusCode).Should().NotBe(500,
            $"SQL injection payload in JSON body should not cause an unhandled server error. Payload: {payload}");
    }

    [Theory]
    [MemberData(nameof(XssPayloads))]
    public async Task XssPayload_InJsonBody_DoesNotReturn500(string payload)
    {
        var client = _factory.GetAuthenticatedClient();

        var body = JsonSerializer.Serialize(new
        {
            name = payload,
            sourcePath = "/tmp"
        });

        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/archive-plans", content);

        ((int)response.StatusCode).Should().NotBe(500,
            $"XSS payload in JSON body should not cause an unhandled server error. Payload: {payload}");
    }

    [Theory]
    [MemberData(nameof(PathTraversalPayloads))]
    public async Task PathTraversal_InRouteParam_DoesNotReturn500(string payload)
    {
        var client = _factory.GetAuthenticatedClient();

        // The route is /api/v1/share/{archiveItemId}/grant — we pass traversal payload as route segment
        var encodedPayload = Uri.EscapeDataString(payload);
        var response = await client.GetAsync($"/api/v1/share/{encodedPayload}/permissions");

        ((int)response.StatusCode).Should().NotBe(500,
            $"Path traversal payload in route param should not cause an unhandled server error. Payload: {payload}");
    }
}
