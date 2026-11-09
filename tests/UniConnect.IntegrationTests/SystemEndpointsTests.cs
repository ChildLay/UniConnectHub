using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UniConnect.IntegrationTests;

public sealed class SystemEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SystemEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_returns_success()
    {
        using var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task System_info_returns_expected_values()
    {
        using var response = await _client.GetAsync("/api/v1/system/info");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var info = await response.Content.ReadFromJsonAsync<SystemInfoResponse>();
        Assert.NotNull(info);
        Assert.Equal("UniConnect Hub", info.Application);
        Assert.Equal("v1", info.ApiVersion);
        Assert.Equal("Running", info.Status);
    }

    private sealed record SystemInfoResponse(string Application, string ApiVersion, string Status);
}
