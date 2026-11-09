using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using UniConnect.Application.Common;
using UniConnect.Application.Moderation;
using UniConnect.Application.Profiles;
using UniConnect.Domain.Moderation;
using UniConnect.Domain.Profiles;

namespace UniConnect.Api.Tests;

public sealed class JwtAuthorizationTests : IClassFixture<JwtApiFactory>
{
    private readonly HttpClient _client;
    public JwtAuthorizationTests(JwtApiFactory factory) => _client = factory.CreateClient();

    [Theory]
    [InlineData("GET", "/api/v1/profiles/me")]
    [InlineData("GET", "/api/v1/discovery/people")]
    [InlineData("GET", "/api/v1/connections")]
    [InlineData("GET", "/api/v1/communities")]
    [InlineData("GET", "/api/v1/posts")]
    [InlineData("PUT", "/api/v1/comments/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/v1/feed")]
    [InlineData("GET", "/api/v1/events")]
    [InlineData("GET", "/api/v1/mentoring/mentors")]
    [InlineData("GET", "/api/v1/opportunities")]
    [InlineData("GET", "/api/v1/notifications")]
    [InlineData("POST", "/api/v1/reports")]
    [InlineData("POST", "/api/v1/auth/logout")]
    public async Task Feature_routes_reject_requests_without_a_bearer_token(string method, string path)
    {
        using var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_bearer_token_is_rejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/me");
        request.Headers.Authorization = new("Bearer", "not-a-valid-jwt");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_bearer_token_is_accepted_by_a_protected_route()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/me");
        request.Headers.Authorization = new("Bearer", JwtApiFactory.CreateToken("User"));
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Public_lookup_and_health_routes_do_not_require_authentication()
    {
        using var info = await _client.GetAsync("/api/v1/system/info");
        using var version = await _client.GetAsync("/api/v1");
        using var health = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        Assert.Equal(HttpStatusCode.OK, version.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task Regular_user_cannot_access_admin_moderation_routes()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/moderation/reports");
        request.Headers.Authorization = new("Bearer", JwtApiFactory.CreateToken("User"));
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_token_can_access_admin_moderation_routes()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/moderation/reports");
        request.Headers.Authorization = new("Bearer", JwtApiFactory.CreateToken("Admin"));
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class JwtApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "api-tests-only-signing-key-32-bytes-long!";
    static JwtApiFactory()
    {
        // Minimal hosting reads environment configuration before ConfigureWebHost callbacks execute.
        Environment.SetEnvironmentVariable("Jwt__Issuer", "UniConnectHub");
        Environment.SetEnvironmentVariable("Jwt__Audience", "UniConnectHubClients");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", SigningKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "UniConnectHub",
            ["Jwt:Audience"] = "UniConnectHubClients",
            ["Jwt:SigningKey"] = SigningKey
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IModerationService>();
            services.AddSingleton<IModerationService, TestModerationService>();
            services.RemoveAll<IProfileService>();
            services.AddSingleton<IProfileService, TestProfileService>();
        });
    }

    public static string CreateToken(string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role) };
        var token = new JwtSecurityToken("UniConnectHub", "UniConnectHubClients", claims, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

internal sealed class TestModerationService : IModerationService
{
    public Task<PagedResult<ReportResponse>> ListAsync(ReportStatus? status, PagedRequest page, CancellationToken ct) => Task.FromResult(new PagedResult<ReportResponse>([], page.SafePageNumber, page.SafePageSize, 0));
    public Task<PagedResult<ManagedUserResponse>> ListUsersAsync(string? search, bool? active, PagedRequest page, CancellationToken ct) => Task.FromResult(new PagedResult<ManagedUserResponse>([], page.SafePageNumber, page.SafePageSize, 0));
    public Task<ReportResponse> GetAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    public Task ResolveAsync(Guid moderatorId, Guid reportId, ResolveReportRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task HideAsync(Guid moderatorId, string targetType, Guid targetId, CancellationToken ct) => throw new NotImplementedException();
    public Task SuspendAsync(Guid moderatorId, Guid userId, CancellationToken ct) => throw new NotImplementedException();
    public Task SetRoleAsync(Guid moderatorId, Guid userId, UserRole role, CancellationToken ct) => throw new NotImplementedException();
}

internal sealed class TestProfileService : IProfileService
{
    public Task<ProfileResponse> GetAsync(Guid viewerId, Guid subjectId, CancellationToken cancellationToken) => Task.FromResult(new ProfileResponse(subjectId, "Test", "User", Guid.Empty, UniConnect.Domain.Users.JourneyStage.CurrentStudent, null, null, null, null, null, null, null, null, null, null, null, null, ProfileVisibility.UniversityCommunity, true, [], []));
    public Task<ProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<ProfileResponse> SetProfileImageAsync(Guid userId, string? imageUrl, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<ProfileResponse> SetCoverImageAsync(Guid userId, string? imageUrl, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<ProfileResponse> SetSkillsAsync(Guid userId, IReadOnlyCollection<string> names, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<ProfileResponse> SetInterestsAsync(Guid userId, IReadOnlyCollection<string> names, CancellationToken cancellationToken) => throw new NotImplementedException();
}


