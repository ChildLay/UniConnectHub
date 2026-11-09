using UniConnect.Domain.Common;
using UniConnect.Domain.Universities;

namespace UniConnect.Domain.Profiles;

public sealed class Skill : Entity<Guid>
{
    private Skill() : base(Guid.Empty) { }
    public Skill(string name) : base(Guid.NewGuid()) { Name = University.Required(name, nameof(name), 100); NormalizedName = University.Normalize(Name); }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
}

public sealed class Interest : Entity<Guid>
{
    private Interest() : base(Guid.Empty) { }
    public Interest(string name) : base(Guid.NewGuid()) { Name = University.Required(name, nameof(name), 100); NormalizedName = University.Normalize(Name); }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
}

public sealed class UserSkill
{
    private UserSkill() { }
    public UserSkill(Guid userId, Guid skillId) { UserId = userId; SkillId = skillId; }
    public Guid UserId { get; private set; }
    public Guid SkillId { get; private set; }
}

public sealed class UserInterest
{
    private UserInterest() { }
    public UserInterest(Guid userId, Guid interestId) { UserId = userId; InterestId = interestId; }
    public Guid UserId { get; private set; }
    public Guid InterestId { get; private set; }
}
