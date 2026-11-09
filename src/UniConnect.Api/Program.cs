using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;
using UniConnect.Application.Common;
using UniConnect.Application.Profiles;
using UniConnect.Application.Universities;
using UniConnect.Application.Connections;
using UniConnect.Application.Events;
using UniConnect.Application.Communities;
using UniConnect.Application.Posts;
using UniConnect.Application.Opportunities;
using UniConnect.Application.Discovery;
using UniConnect.Application.Mentoring;
using UniConnect.Application.Notifications;
using UniConnect.Application.Moderation;
using UniConnect.Domain.Moderation;
using UniConnect.Infrastructure;
using UniConnect.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddSingleton<UniConnect.Api.Media.IProfileMediaStorage, UniConnect.Api.Media.FileProfileMediaStorage>();
builder.Services.AddScoped<IUniversityService, UniversityService>();
builder.Services.AddScoped<IConnectionService, ConnectionService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<ICommunityService, CommunityService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<IOpportunityService, OpportunityService>();
builder.Services.AddScoped<IDiscoveryService, DiscoveryService>();
builder.Services.AddScoped<IMentoringService, MentoringService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IModerationService, ModerationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UniConnect Hub API",
        Version = "v1",
        Description = "Version 1 API for the UniConnect Hub student and alumni community platform."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "Enter a JWT access token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessCheck>("database", tags: ["ready"]);
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtKey = builder.Configuration["Jwt:SigningKey"];
if (!string.IsNullOrWhiteSpace(jwtIssuer) && !string.IsNullOrWhiteSpace(jwtAudience) && !string.IsNullOrWhiteSpace(jwtKey))
{
    if (Encoding.UTF8.GetByteCount(jwtKey) < 32 && !builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 bytes in non-development environments.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwtIssuer,
            ValidateAudience = true, ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
}
else
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("JWT issuer, audience, and signing key must be configured outside Development.");
    builder.Services.AddAuthentication();
}
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanModerateContent", policy => policy.RequireRole(UserRole.Admin.ToString(), UserRole.Moderator.ToString()));
    options.AddPolicy("CanManageCommunity", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("CanManageEvent", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("CanEditProfile", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("CanViewProfile", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("CanViewPost", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("CanComment", policy => policy.RequireAuthenticatedUser());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("sensitive", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 30), Window = TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:WindowMinutes", 1)), QueueLimit = 0, AutoReplenishment = true }));
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendClients", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0)
        {
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.SetIsOriginAllowed(_ => false);
        }
    });
});

var app = builder.Build();
app.Logger.LogInformation("Starting {ApplicationName} API in {EnvironmentName}", "UniConnect Hub", app.Environment.EnvironmentName);

if (app.Environment.IsDevelopment())
{
    await using var databaseScope = app.Services.CreateAsyncScope();
    var database = databaseScope.ServiceProvider.GetRequiredService<UniConnect.Infrastructure.Persistence.UniConnectDbContext>();
    app.Logger.LogInformation("Creating the development database schema if it does not exist.");
    await database.Database.EnsureCreatedAsync();
    await database.Database.ExecuteSqlRawAsync("IF COL_LENGTH(N'dbo.Profiles', N'CoverImageUrl') IS NULL ALTER TABLE [dbo].[Profiles] ADD [CoverImageUrl] nvarchar(1000) NULL;");
    var seeder = databaseScope.ServiceProvider.GetRequiredService<UniConnect.Infrastructure.Persistence.IDevelopmentDataSeeder>();
    await seeder.SeedAsync();
    app.Logger.LogInformation("Development sample data is ready.");
}

app.UseExceptionHandler();
app.UseRateLimiter();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options => options.RouteTemplate = "swagger/{documentName}/swagger.json");
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "UniConnect Hub API v1");
        options.RoutePrefix = "swagger";
    });
}
app.UseHttpsRedirection();
app.UseCors("FrontendClients");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

public partial class Program;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "The request is invalid."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "The requested resource was not found."),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication is required or invalid."),
            UniConnect.Application.Common.AccessDeniedException => (StatusCodes.Status403Forbidden, "The request is not allowed."),
            InvalidOperationException => (StatusCodes.Status409Conflict, "The request conflicts with the current resource state."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };
        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "An unhandled exception occurred while processing a request");
        else
            logger.LogInformation("Request rejected with {StatusCode}: {ExceptionType}", status, exception.GetType().Name);
        httpContext.Response.StatusCode = status;
        await Results.Problem(statusCode: status, title: title,
            detail: status == StatusCodes.Status500InternalServerError ? "The server could not complete the request." : exception.Message)
            .ExecuteAsync(httpContext);
        return true;
    }
}

internal sealed class DatabaseReadinessCheck(IServiceProvider services) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<UniConnect.Infrastructure.Persistence.UniConnectDbContext>();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Database connection succeeded.")
                : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Database connection failed.");
        }
        catch (Exception exception)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Database connection failed.", exception);
        }
    }
}

