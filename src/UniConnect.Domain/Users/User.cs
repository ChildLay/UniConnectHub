using UniConnect.Domain.Common;
using UniConnect.Domain.Universities;

namespace UniConnect.Domain.Users;

public enum JourneyStage { NewStudent, CurrentStudent, Graduate, Alumni }

public sealed class User : Entity<Guid>, IAuditableEntity
{
    private User() : base(Guid.Empty) { }

    public User(string email, string passwordHash, string firstName, string lastName, JourneyStage journeyStage, University university, DateTime nowUtc)
        : base(Guid.NewGuid())
    {
        if (!university.IsActive) throw new InvalidOperationException("Registration requires an active university.");
        Email = University.Required(email, nameof(email), 320);
        NormalizedEmail = Email.ToUpperInvariant();
        PasswordHash = University.Required(passwordHash, nameof(passwordHash), 1000);
        FirstName = University.Required(firstName, nameof(firstName), 100);
        LastName = University.Required(lastName, nameof(lastName), 100);
        JourneyStage = journeyStage;
        University = university;
        UniversityId = university.Id;
        IsActive = true;
        CreatedAtUtc = UpdatedAtUtc = nowUtc;
    }

    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public JourneyStage JourneyStage { get; private set; }
    public Guid UniversityId { get; private set; }
    public University University { get; private set; } = null!;
    public bool EmailVerified { get; private set; }
    public bool IsActive { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void ChangePasswordHash(string passwordHash, DateTime nowUtc)
    {
        PasswordHash = University.Required(passwordHash, nameof(passwordHash), 1000);
        UpdatedAtUtc = nowUtc;
    }

    public void MarkEmailVerified(DateTime nowUtc) { EmailVerified = true; UpdatedAtUtc = nowUtc; }
    public void RecordSuccessfulLogin(DateTime nowUtc) { FailedLoginAttempts = 0; LockedUntilUtc = null; LastLoginAtUtc = nowUtc; UpdatedAtUtc = nowUtc; }
    public void RecordFailedLogin(DateTime nowUtc, int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttempts) LockedUntilUtc = nowUtc.Add(lockoutDuration);
        UpdatedAtUtc = nowUtc;
    }
    public bool IsLockedAt(DateTime nowUtc) => LockedUntilUtc is { } until && until > nowUtc;
    public void Deactivate(DateTime nowUtc) { IsActive = false; UpdatedAtUtc = nowUtc; }
    public void Activate(DateTime nowUtc) { IsActive = true; UpdatedAtUtc = nowUtc; }
    public void SetRole(UniConnect.Domain.Moderation.UserRole role, DateTime nowUtc) { Role = role; UpdatedAtUtc = nowUtc; }
    public UniConnect.Domain.Moderation.UserRole Role { get; private set; } = UniConnect.Domain.Moderation.UserRole.User;
}
