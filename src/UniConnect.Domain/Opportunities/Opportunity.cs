using UniConnect.Domain.Common;
using UniConnect.Domain.Universities;

namespace UniConnect.Domain.Opportunities;

public enum OpportunityType { Internship, GraduateRole, Job, Volunteering, Workshop, Networking, Scholarship, ProfessionalDevelopment, CommunityActivity, Competition, Other }

public sealed class Opportunity : Entity<Guid>, IAuditableEntity
{
    private Opportunity() : base(Guid.Empty) { }
    public Opportunity(string title, string description, Guid creatorId, Guid universityId, OpportunityType type, DateTime? closingDateUtc, string? applicationUrl, DateTime nowUtc) : base(Guid.NewGuid())
    {
        Title = University.Required(title, nameof(title), 200); Description = University.Required(description, nameof(description), 8000);
        CreatorId = creatorId; UniversityId = universityId; Type = type; ClosingDateUtc = closingDateUtc; ApplicationUrl = University.Optional(applicationUrl, 1000);
        IsActive = true; CreatedAtUtc = UpdatedAtUtc = nowUtc;
    }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid CreatorId { get; private set; }
    public Guid UniversityId { get; private set; }
    public OpportunityType Type { get; private set; }
    public DateTime? ClosingDateUtc { get; private set; }
    public string? ApplicationUrl { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public void Close(DateTime nowUtc) { IsActive = false; UpdatedAtUtc = nowUtc; }
    public void Update(string title, string description, OpportunityType type, DateTime? closingDateUtc, string? applicationUrl, DateTime nowUtc)
    {
        if (!IsActive) throw new InvalidOperationException("Closed opportunities cannot be edited.");
        Title = University.Required(title, nameof(title), 200); Description = University.Required(description, nameof(description), 8000);
        Type = type; ClosingDateUtc = closingDateUtc; ApplicationUrl = University.Optional(applicationUrl, 1000); UpdatedAtUtc = nowUtc;
    }
}

public sealed class SavedOpportunity
{
    private SavedOpportunity() { }
    public SavedOpportunity(Guid opportunityId, Guid userId, DateTime savedAtUtc) { OpportunityId = opportunityId; UserId = userId; SavedAtUtc = savedAtUtc; }
    public Guid OpportunityId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime SavedAtUtc { get; private set; }
}
