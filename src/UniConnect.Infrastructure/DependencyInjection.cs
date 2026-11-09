using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniConnect.Application.Common;
using UniConnect.Infrastructure.Security;
using UniConnect.Infrastructure.Auth;
using UniConnect.Application.Auth;
using Microsoft.AspNetCore.Identity;
using UniConnect.Domain.Users;
using UniConnect.Infrastructure.Persistence;

namespace UniConnect.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
            services.AddDbContext<UniConnectDbContext>(options => options.UseSqlServer(connectionString));
        else
            services.AddDbContext<UniConnectDbContext>(options => options.UseSqlServer("Server=localhost;Database=UniConnectHub;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True"));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<UniConnectDbContext>());
        services.AddScoped<IAuthStore, AuthStore>();
        services.AddScoped<IPasswordHashing, PasswordHashing>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<ITokenService, TokenService>();
        var emailProvider = configuration["Email:Provider"] ?? "Console";
        if (emailProvider.Equals("Gmail", StringComparison.OrdinalIgnoreCase))
        {
            var gmail = configuration.GetSection("Email:Gmail");
            if (string.IsNullOrWhiteSpace(gmail["Username"]) || string.IsNullOrWhiteSpace(gmail["AppPassword"]) || string.IsNullOrWhiteSpace(gmail["FromEmail"]))
                throw new InvalidOperationException("Email:Provider is Gmail, but Email:Gmail:Username, AppPassword, or FromEmail is missing.");
            services.AddScoped<IEmailService, GmailSmtpEmailService>();
        }
        else if (emailProvider.Equals("Brevo", StringComparison.OrdinalIgnoreCase))
        {
            var brevo = configuration.GetSection("Email:Brevo");
            if (string.IsNullOrWhiteSpace(brevo["Username"]) || string.IsNullOrWhiteSpace(brevo["SmtpKey"]) || string.IsNullOrWhiteSpace(brevo["FromEmail"]))
                throw new InvalidOperationException("Email:Provider is Brevo, but Email:Brevo:Username, SmtpKey, or FromEmail is missing.");
            services.AddScoped<IEmailService, BrevoSmtpEmailService>();
        }
        else if (emailProvider.Equals("Console", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailService, ConsoleEmailService>();
        }
        else
        {
            throw new InvalidOperationException($"Unsupported email provider '{emailProvider}'. Use Console, Gmail, or Brevo.");
        }
        services.AddScoped<IDevelopmentDataSeeder, DevelopmentDataSeeder>();
        return services;
    }
}

public sealed class PasswordHashing : IPasswordHashing
{
    private readonly PasswordHasher<User> _hasher = new();
    public string Hash(string password) => _hasher.HashPassword(null!, password);
    public bool Verify(string passwordHash, string password) => _hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
