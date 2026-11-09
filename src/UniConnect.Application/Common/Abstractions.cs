using Microsoft.EntityFrameworkCore;
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

namespace UniConnect.Application.Common;

public interface IApplicationDbContext
{
    DbSet<University> Universities { get; }
    DbSet<User> Users { get; }
    DbSet<UserProfile> Profiles { get; }
    DbSet<Skill> Skills { get; }
    DbSet<Interest> Interests { get; }
    DbSet<UserSkill> UserSkills { get; }
    DbSet<UserInterest> UserInterests { get; }
    DbSet<ConnectionRequest> ConnectionRequests { get; }
    DbSet<Connection> Connections { get; }
    DbSet<BlockedUser> BlockedUsers { get; }
    DbSet<Community> Communities { get; }
    DbSet<CommunityMember> CommunityMembers { get; }
    DbSet<Post> Posts { get; }
    DbSet<Comment> Comments { get; }
    DbSet<PostReaction> PostReactions { get; }
    DbSet<PostBookmark> PostBookmarks { get; }
    DbSet<PostAudienceUser> PostAudienceUsers { get; }
    DbSet<Event> Events { get; }
    DbSet<EventRegistration> EventRegistrations { get; }
    DbSet<MentorProfile> MentorProfiles { get; }
    DbSet<MentorshipRequest> MentorshipRequests { get; }
    DbSet<Opportunity> Opportunities { get; }
    DbSet<SavedOpportunity> SavedOpportunities { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Report> Reports { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ModerationAction> ModerationActions { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IClock { DateTime UtcNow { get; } }
public interface IEmailService { Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken); }
public interface ITokenService
{
    string CreateAccessToken(User user, IReadOnlyCollection<string> roles);
    string CreateSecureToken();
    string HashToken(string token);
}
public interface ICurrentUser { Guid? UserId { get; } bool IsInRole(string role); }
