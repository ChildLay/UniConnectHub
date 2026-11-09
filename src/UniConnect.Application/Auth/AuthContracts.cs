using UniConnect.Application.Common;
using UniConnect.Domain.Users;

namespace UniConnect.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName, Guid UniversityId, JourneyStage JourneyStage);
public sealed record LoginRequest(string Email, string Password);
public sealed record TokenPair(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAtUtc);
public sealed record RefreshTokenRecord(Guid Id, Guid UserId, string TokenHash, DateTime ExpiresAtUtc, DateTime? RevokedAtUtc);
public sealed record OneTimeTokenRecord(Guid Id, Guid UserId, string Purpose, string TokenHash, DateTime ExpiresAtUtc, DateTime? UsedAtUtc);

public interface IPasswordHashing
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

public interface IAuthStore
{
    Task<User?> FindUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<User?> FindUserAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> UniversityIsActiveAsync(Guid universityId, CancellationToken cancellationToken);
    Task<User> CreateUserAsync(RegisterRequest request, string passwordHash, DateTime nowUtc, CancellationToken cancellationToken);
    Task AddUserAsync(User user, CancellationToken cancellationToken);
    Task AddProfileAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<RefreshTokenRecord?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task AddRefreshTokenAsync(RefreshTokenRecord token, CancellationToken cancellationToken);
    Task RevokeRefreshTokenAsync(Guid id, DateTime revokedAtUtc, CancellationToken cancellationToken);
    Task RevokeAllRefreshTokensAsync(Guid userId, DateTime revokedAtUtc, CancellationToken cancellationToken);
    Task AddOneTimeTokenAsync(OneTimeTokenRecord token, CancellationToken cancellationToken);
    Task<OneTimeTokenRecord?> FindOneTimeTokenAsync(string purpose, string tokenHash, CancellationToken cancellationToken);
    Task MarkOneTimeTokenUsedAsync(Guid id, DateTime usedAtUtc, CancellationToken cancellationToken);
    Task SetEmailVerifiedAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken);
    Task SetPasswordHashAsync(Guid userId, string passwordHash, DateTime nowUtc, CancellationToken cancellationToken);
}

public interface IAuthenticationService
{
    Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<TokenPair> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<TokenPair> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken);
    Task VerifyEmailAsync(string token, CancellationToken cancellationToken);
    Task ResendVerificationAsync(string email, CancellationToken cancellationToken);
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken);
    Task ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken);
}

public sealed class AuthenticationService(IAuthStore store, IPasswordHashing passwordHashing, ITokenService tokens, IClock clock, IEmailService email) : IAuthenticationService
{
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out var parsedEmail) || !string.Equals(parsedEmail.Address, request.Email.Trim(), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("A valid email address is required.");
        ValidatePassword(request.Password);
        if (!Enum.IsDefined(request.JourneyStage)) throw new ArgumentException("Journey stage is invalid.");
        if (await store.FindUserByEmailAsync(normalizedEmail, cancellationToken) is not null) throw new InvalidOperationException("An account with this email already exists.");
        if (!await store.UniversityIsActiveAsync(request.UniversityId, cancellationToken)) throw new InvalidOperationException("The selected university is unavailable.");
        var user = await store.CreateUserAsync(request, passwordHashing.Hash(request.Password), clock.UtcNow, cancellationToken);
        await store.AddUserAsync(user, cancellationToken);
        await store.AddProfileAsync(user.Id, clock.UtcNow, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        await SendOneTimeTokenAsync(user.Id, user.Email, "email-verification", "Verify your UniConnect Hub email", cancellationToken);
    }

    public async Task<TokenPair> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await store.FindUserByEmailAsync(request.Email.Trim().ToUpperInvariant(), cancellationToken);
        if (user is not null && user.IsLockedAt(clock.UtcNow)) throw new UnauthorizedAccessException("Your account is temporarily locked after several unsuccessful sign-in attempts. Please wait a little while and try again.");
        if (user is null || !passwordHashing.Verify(user.PasswordHash, request.Password))
        {
            if (user is not null) { user.RecordFailedLogin(clock.UtcNow, MaxFailedAttempts, LockoutDuration); await store.SaveChangesAsync(cancellationToken); }
            throw new UnauthorizedAccessException("Invalid email or password.");
        }
        if (!user.IsActive) throw new UnauthorizedAccessException("Your account has been suspended, so you can’t sign in. Please contact UniConnect support if you believe this is a mistake.");
        if (user.IsLockedAt(clock.UtcNow)) throw new UnauthorizedAccessException("Your account is temporarily locked after several unsuccessful sign-in attempts. Please wait a little while and try again.");
        if (!user.EmailVerified) throw new UnauthorizedAccessException("Email verification is required before signing in.");
        user.RecordSuccessfulLogin(clock.UtcNow);
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<TokenPair> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = tokens.HashToken(refreshToken);
        var stored = await store.FindRefreshTokenAsync(tokenHash, cancellationToken);
        if (stored is null || stored.RevokedAtUtc is not null || stored.ExpiresAtUtc <= clock.UtcNow)
        {
            if (stored is not null) await store.RevokeAllRefreshTokensAsync(stored.UserId, clock.UtcNow, cancellationToken);
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }
        await store.RevokeRefreshTokenAsync(stored.Id, clock.UtcNow, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        var user = await store.FindUserAsync(stored.UserId, cancellationToken) ?? throw new UnauthorizedAccessException("Account is unavailable.");
        if (!user.IsActive) throw new UnauthorizedAccessException("Account is unavailable.");
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken)
    {
        var stored = await store.FindRefreshTokenAsync(tokens.HashToken(refreshToken), cancellationToken);
        if (stored is not null && stored.UserId == userId) await store.RevokeRefreshTokenAsync(stored.Id, clock.UtcNow, cancellationToken);
    }

    public async Task VerifyEmailAsync(string token, CancellationToken cancellationToken)
    {
        var record = await store.FindOneTimeTokenAsync("email-verification", tokens.HashToken(token), cancellationToken);
        if (record is null || record.UsedAtUtc is not null || record.ExpiresAtUtc <= clock.UtcNow) throw new UnauthorizedAccessException("Verification token is invalid or expired.");
        await store.MarkOneTimeTokenUsedAsync(record.Id, clock.UtcNow, cancellationToken);
        await store.SetEmailVerifiedAsync(record.UserId, clock.UtcNow, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task ResendVerificationAsync(string emailAddress, CancellationToken cancellationToken)
    {
        var user = await store.FindUserByEmailAsync(emailAddress.Trim().ToUpperInvariant(), cancellationToken);
        if (user is null || user.EmailVerified) return;
        await SendOneTimeTokenAsync(user.Id, user.Email, "email-verification", "Verify your UniConnect Hub email", cancellationToken);
    }

    public async Task RequestPasswordResetAsync(string emailAddress, CancellationToken cancellationToken)
    {
        var user = await store.FindUserByEmailAsync(emailAddress.Trim().ToUpperInvariant(), cancellationToken);
        if (user is null || !user.IsActive) return;
        await SendOneTimeTokenAsync(user.Id, user.Email, "password-reset", "Reset your UniConnect Hub password", cancellationToken);
    }

    public async Task ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        ValidatePassword(newPassword);
        var record = await store.FindOneTimeTokenAsync("password-reset", tokens.HashToken(token), cancellationToken);
        if (record is null || record.UsedAtUtc is not null || record.ExpiresAtUtc <= clock.UtcNow) throw new UnauthorizedAccessException("Password reset token is invalid or expired.");
        await store.SetPasswordHashAsync(record.UserId, passwordHashing.Hash(newPassword), clock.UtcNow, cancellationToken);
        await store.MarkOneTimeTokenUsedAsync(record.Id, clock.UtcNow, cancellationToken);
        await store.RevokeAllRefreshTokensAsync(record.UserId, clock.UtcNow, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task SendOneTimeTokenAsync(Guid userId, string emailAddress, string purpose, string subject, CancellationToken cancellationToken)
    {
        var raw = tokens.CreateSecureToken();
        var lifetime = purpose == "email-verification" ? TimeSpan.FromHours(24) : TimeSpan.FromHours(1);
        await store.AddOneTimeTokenAsync(new OneTimeTokenRecord(Guid.NewGuid(), userId, purpose, tokens.HashToken(raw), clock.UtcNow.Add(lifetime), null), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        var purposeDescription = purpose == "email-verification" ? "verify your email address" : "reset your password";
        var body = $"Hello,\n\nUse this one-time code to {purposeDescription}:\n\n{raw}\n\nThis code expires in {(int)lifetime.TotalHours} hour(s). If you didn’t request this, you can ignore this email.\n\nUniConnect Hub";
        await email.SendAsync(emailAddress, subject, body, cancellationToken);
    }

    private async Task<TokenPair> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        await store.SaveChangesAsync(cancellationToken);
        var accessExpiry = clock.UtcNow.AddMinutes(15);
        var refresh = tokens.CreateSecureToken();
        await store.AddRefreshTokenAsync(new RefreshTokenRecord(Guid.NewGuid(), user.Id, tokens.HashToken(refresh), clock.UtcNow.Add(RefreshLifetime), null), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new TokenPair(tokens.CreateAccessToken(user, []), refresh, accessExpiry);
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 12 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(c => !char.IsLetterOrDigit(c)))
            throw new ArgumentException("Password must be at least 12 characters and include upper-case, lower-case, numeric, and special characters.");
    }
}
