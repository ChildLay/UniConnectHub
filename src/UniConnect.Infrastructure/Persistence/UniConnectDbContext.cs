using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Common;
using UniConnect.Domain.Common;
using UniConnect.Domain.Communities;
using UniConnect.Domain.Connections;
using UniConnect.Domain.Content;
using UniConnect.Domain.Events;
using UniConnect.Domain.Mentoring;
using UniConnect.Domain.Moderation;
using UniConnect.Domain.Notifications;
using UniConnect.Domain.Opportunities;
using UniConnect.Domain.Profiles;
using UniConnect.Domain.Universities;
using UniConnect.Domain.Users;
using UniConnect.Infrastructure.Auth;

namespace UniConnect.Infrastructure.Persistence;

public sealed class UniConnectDbContext(DbContextOptions<UniConnectDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<University> Universities => Set<University>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<UserInterest> UserInterests => Set<UserInterest>();
    public DbSet<ConnectionRequest> ConnectionRequests => Set<ConnectionRequest>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<BlockedUser> BlockedUsers => Set<BlockedUser>();
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<CommunityMember> CommunityMembers => Set<CommunityMember>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<PostReaction> PostReactions => Set<PostReaction>();
    public DbSet<PostBookmark> PostBookmarks => Set<PostBookmark>();
    public DbSet<PostAudienceUser> PostAudienceUsers => Set<PostAudienceUser>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();
    public DbSet<MentorProfile> MentorProfiles => Set<MentorProfile>();
    public DbSet<MentorshipRequest> MentorshipRequests => Set<MentorshipRequest>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<SavedOpportunity> SavedOpportunities => Set<SavedOpportunity>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OneTimeToken> OneTimeTokens => Set<OneTimeToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UniConnectDbContext).Assembly);
        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.ToTable("RefreshTokens"); b.HasKey(x => x.Id); b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => new { x.UserId, x.RevokedAtUtc });
            b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OneTimeToken>(b =>
        {
            b.ToTable("OneTimeTokens"); b.HasKey(x => x.Id); b.Property(x => x.Purpose).HasMaxLength(40).IsRequired();
            b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired(); b.HasIndex(x => new { x.Purpose, x.TokenHash }).IsUnique();
            b.HasIndex(x => new { x.UserId, x.Purpose, x.UsedAtUtc });
            b.HasOne<UniConnect.Domain.Users.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Metadata.FindProperty(nameof(IAuditableEntity.CreatedAtUtc)) is { } created)
                    entry.Property(created.Name).CurrentValue = now;
            }
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                if (entry.Metadata.FindProperty(nameof(IAuditableEntity.UpdatedAtUtc)) is { } updated)
                    entry.Property(updated.Name).CurrentValue = now;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
