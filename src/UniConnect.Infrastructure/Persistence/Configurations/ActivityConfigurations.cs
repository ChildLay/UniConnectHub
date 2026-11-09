using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniConnect.Domain.Content;
using UniConnect.Domain.Events;
using UniConnect.Domain.Mentoring;
using UniConnect.Domain.Moderation;
using UniConnect.Domain.Notifications;
using UniConnect.Domain.Opportunities;
using UniConnect.Domain.Users;

namespace UniConnect.Infrastructure.Persistence.Configurations;

public sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> b) { b.ToTable("Posts"); b.HasKey(x => x.Id); b.Property(x => x.Content).HasMaxLength(10000).IsRequired(); b.HasIndex(x => x.CreatedAtUtc); b.HasIndex(x => new { x.CommunityId, x.CreatedAtUtc }); b.HasIndex(x => new { x.UniversityId, x.CreatedAtUtc }); b.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b) { b.ToTable("Comments"); b.HasKey(x => x.Id); b.Property(x => x.Content).HasMaxLength(4000).IsRequired(); b.HasIndex(x => new { x.PostId, x.CreatedAtUtc }); b.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade); b.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class PostReactionConfiguration : IEntityTypeConfiguration<PostReaction>
{
    public void Configure(EntityTypeBuilder<PostReaction> b) { b.ToTable("PostReactions"); b.HasKey(x => new { x.PostId, x.UserId }); b.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class PostBookmarkConfiguration : IEntityTypeConfiguration<PostBookmark>
{
    public void Configure(EntityTypeBuilder<PostBookmark> b) { b.ToTable("PostBookmarks"); b.HasKey(x => new { x.PostId, x.UserId }); b.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class PostAudienceUserConfiguration : IEntityTypeConfiguration<PostAudienceUser>
{
    public void Configure(EntityTypeBuilder<PostAudienceUser> b) { b.ToTable("PostAudienceUsers"); b.HasKey(x => new { x.PostId, x.UserId }); b.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class EventConfiguration : IEntityTypeConfiguration<Domain.Events.Event>
{
    public void Configure(EntityTypeBuilder<Domain.Events.Event> b) { b.ToTable("Events"); b.HasKey(x => x.Id); b.Property(x => x.Title).HasMaxLength(200).IsRequired(); b.Property(x => x.Description).HasMaxLength(5000); b.HasIndex(x => new { x.UniversityId, x.StartUtc }); b.HasIndex(x => new { x.CommunityId, x.StartUtc }); b.HasOne<User>().WithMany().HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class EventRegistrationConfiguration : IEntityTypeConfiguration<EventRegistration>
{
    public void Configure(EntityTypeBuilder<EventRegistration> b) { b.ToTable("EventRegistrations"); b.HasKey(x => new { x.EventId, x.UserId }); b.HasOne<UniConnect.Domain.Events.Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class MentorProfileConfiguration : IEntityTypeConfiguration<MentorProfile>
{
    public void Configure(EntityTypeBuilder<MentorProfile> b) { b.ToTable("MentorProfiles"); b.HasKey(x => x.Id); b.Property(x => x.Bio).HasMaxLength(3000).IsRequired(); b.HasIndex(x => x.IsAvailable); b.HasOne<User>().WithOne().HasForeignKey<MentorProfile>(x => x.Id).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class MentorshipRequestConfiguration : IEntityTypeConfiguration<MentorshipRequest>
{
    public void Configure(EntityTypeBuilder<MentorshipRequest> b) { b.ToTable("MentorshipRequests"); b.HasKey(x => x.Id); b.Property(x => x.Message).HasMaxLength(2000).IsRequired(); b.HasIndex(x => new { x.MentorId, x.Status }); b.HasIndex(x => new { x.MenteeId, x.Status }); b.HasOne<User>().WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict); b.HasOne<User>().WithMany().HasForeignKey(x => x.MenteeId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> b) { b.ToTable("Opportunities"); b.HasKey(x => x.Id); b.Property(x => x.Title).HasMaxLength(200).IsRequired(); b.Property(x => x.Description).HasMaxLength(8000).IsRequired(); b.Property(x => x.ApplicationUrl).HasMaxLength(1000); b.HasIndex(x => new { x.Type, x.IsActive }); b.HasIndex(x => x.ClosingDateUtc); b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class SavedOpportunityConfiguration : IEntityTypeConfiguration<SavedOpportunity>
{
    public void Configure(EntityTypeBuilder<SavedOpportunity> b) { b.ToTable("SavedOpportunities"); b.HasKey(x => new { x.OpportunityId, x.UserId }); b.HasOne<Opportunity>().WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Cascade); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b) { b.ToTable("Notifications"); b.HasKey(x => x.Id); b.Property(x => x.Type).HasMaxLength(80).IsRequired(); b.Property(x => x.Title).HasMaxLength(160).IsRequired(); b.Property(x => x.Message).HasMaxLength(1000).IsRequired(); b.Property(x => x.ResourceType).HasMaxLength(80); b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAtUtc }); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> b) { b.ToTable("Reports"); b.HasKey(x => x.Id); b.Property(x => x.TargetType).HasMaxLength(80).IsRequired(); b.Property(x => x.Reason).HasMaxLength(300).IsRequired(); b.Property(x => x.Details).HasMaxLength(3000); b.Property(x => x.Resolution).HasMaxLength(2000); b.HasIndex(x => new { x.Status, x.CreatedAtUtc }); b.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict); b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b) { b.ToTable("AuditLogs"); b.HasKey(x => x.Id); b.Property(x => x.Action).HasMaxLength(100).IsRequired(); b.Property(x => x.EntityType).HasMaxLength(100).IsRequired(); b.Property(x => x.MetadataJson).HasMaxLength(4000); b.HasIndex(x => x.TimestampUtc); b.HasIndex(x => new { x.EntityType, x.EntityId }); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull); }
}
public sealed class ModerationActionConfiguration : IEntityTypeConfiguration<ModerationAction>
{
    public void Configure(EntityTypeBuilder<ModerationAction> b) { b.ToTable("ModerationActions"); b.HasKey(x => x.Id); b.Property(x => x.Action).HasMaxLength(80).IsRequired(); b.Property(x => x.TargetType).HasMaxLength(80).IsRequired(); b.Property(x => x.Details).HasMaxLength(2000); b.HasIndex(x => x.CreatedAtUtc); b.HasOne<User>().WithMany().HasForeignKey(x => x.ModeratorId).OnDelete(DeleteBehavior.Restrict); }
}
