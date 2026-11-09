using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Auth;
using UniConnect.Domain.Profiles;
using UniConnect.Domain.Universities;
using UniConnect.Domain.Users;
using UniConnect.Infrastructure.Persistence;

namespace UniConnect.Infrastructure.Auth;

public sealed class AuthStore(UniConnectDbContext db) : IAuthStore
{
    public Task<User?> FindUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
    public Task<User?> FindUserAsync(Guid id, CancellationToken cancellationToken) => db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<bool> UniversityIsActiveAsync(Guid universityId, CancellationToken cancellationToken) => db.Universities.AnyAsync(x => x.Id == universityId && x.IsActive, cancellationToken);
    public async Task<User> CreateUserAsync(RegisterRequest request, string passwordHash, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var university = await db.Universities.SingleOrDefaultAsync(x => x.Id == request.UniversityId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("The selected university is unavailable.");
        return new User(request.Email, passwordHash, request.FirstName, request.LastName, request.JourneyStage, university, nowUtc);
    }
    public Task AddUserAsync(User user, CancellationToken cancellationToken) => db.Users.AddAsync(user, cancellationToken).AsTask();
    public Task AddProfileAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken) => db.Profiles.AddAsync(new UserProfile(userId, nowUtc), cancellationToken).AsTask();
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await db.SaveChangesAsync(cancellationToken);
    public async Task<RefreshTokenRecord?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var record = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        return record is null ? null : new RefreshTokenRecord(record.Id, record.UserId, record.TokenHash, record.ExpiresAtUtc, record.RevokedAtUtc);
    }
    public Task AddRefreshTokenAsync(RefreshTokenRecord token, CancellationToken cancellationToken) => db.RefreshTokens.AddAsync(new RefreshToken(token.Id, token.UserId, token.TokenHash, token.ExpiresAtUtc), cancellationToken).AsTask();
    public async Task RevokeRefreshTokenAsync(Guid id, DateTime revokedAtUtc, CancellationToken cancellationToken)
    {
        var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        token?.Revoke(revokedAtUtc);
    }
    public async Task RevokeAllRefreshTokensAsync(Guid userId, DateTime revokedAtUtc, CancellationToken cancellationToken)
    {
        var tokens = await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var token in tokens) token.Revoke(revokedAtUtc);
        await db.SaveChangesAsync(cancellationToken);
    }
    public Task AddOneTimeTokenAsync(OneTimeTokenRecord token, CancellationToken cancellationToken) => db.OneTimeTokens.AddAsync(new OneTimeToken(token.Id, token.UserId, token.Purpose, token.TokenHash, token.ExpiresAtUtc), cancellationToken).AsTask();
    public async Task<OneTimeTokenRecord?> FindOneTimeTokenAsync(string purpose, string tokenHash, CancellationToken cancellationToken)
    {
        var record = await db.OneTimeTokens.SingleOrDefaultAsync(x => x.Purpose == purpose && x.TokenHash == tokenHash, cancellationToken);
        return record is null ? null : new OneTimeTokenRecord(record.Id, record.UserId, record.Purpose, record.TokenHash, record.ExpiresAtUtc, record.UsedAtUtc);
    }
    public async Task MarkOneTimeTokenUsedAsync(Guid id, DateTime usedAtUtc, CancellationToken cancellationToken)
    {
        var token = await db.OneTimeTokens.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        token?.MarkUsed(usedAtUtc);
    }
    public async Task SetEmailVerifiedAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleAsync(x => x.Id == userId, cancellationToken);
        user.MarkEmailVerified(nowUtc);
    }
    public async Task SetPasswordHashAsync(Guid userId, string passwordHash, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleAsync(x => x.Id == userId, cancellationToken);
        user.ChangePasswordHash(passwordHash, nowUtc);
    }
}

public sealed class RefreshToken
{
    private RefreshToken() { }
    public RefreshToken(Guid id, Guid userId, string tokenHash, DateTime expiresAtUtc) { Id = id; UserId = userId; TokenHash = tokenHash; ExpiresAtUtc = expiresAtUtc; CreatedAtUtc = DateTime.UtcNow; }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public void Revoke(DateTime utcNow) => RevokedAtUtc ??= utcNow;
}

public sealed class OneTimeToken
{
    private OneTimeToken() { }
    public OneTimeToken(Guid id, Guid userId, string purpose, string tokenHash, DateTime expiresAtUtc) { Id = id; UserId = userId; Purpose = purpose; TokenHash = tokenHash; ExpiresAtUtc = expiresAtUtc; CreatedAtUtc = DateTime.UtcNow; }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }
    public void MarkUsed(DateTime utcNow) => UsedAtUtc ??= utcNow;
}
