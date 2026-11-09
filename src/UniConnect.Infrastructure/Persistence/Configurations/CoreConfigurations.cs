using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

namespace UniConnect.Infrastructure.Persistence.Configurations;

public sealed class UniversityConfiguration : IEntityTypeConfiguration<University>
{
    public void Configure(EntityTypeBuilder<University> b) { b.ToTable("Universities"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.Property(x => x.NormalizedName).HasMaxLength(200).IsRequired(); b.HasIndex(x => x.NormalizedName).IsUnique(); b.Property(x => x.Country).HasMaxLength(100).IsRequired(); b.Property(x => x.ShortName).HasMaxLength(50); b.Property(x => x.City).HasMaxLength(100); b.Property(x => x.WebsiteUrl).HasMaxLength(500); }
}

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b) { b.ToTable("Users"); b.HasKey(x => x.Id); b.Property(x => x.Email).HasMaxLength(320).IsRequired(); b.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired(); b.HasIndex(x => x.NormalizedEmail).IsUnique(); b.Property(x => x.PasswordHash).HasMaxLength(1000).IsRequired(); b.Property(x => x.FirstName).HasMaxLength(100).IsRequired(); b.Property(x => x.LastName).HasMaxLength(100).IsRequired(); b.Property(x => x.Role).HasConversion<string>().HasMaxLength(30).IsRequired(); b.HasIndex(x => x.UniversityId); b.HasOne(x => x.University).WithMany().HasForeignKey(x => x.UniversityId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => x.LockedUntilUtc); }
}

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b) { b.ToTable("Profiles"); b.HasKey(x => x.Id); b.Property(x => x.Headline).HasMaxLength(160); b.Property(x => x.Bio).HasMaxLength(3000); b.Property(x => x.Programme).HasMaxLength(200); b.Property(x => x.StudyArea).HasMaxLength(200); b.Property(x => x.IndustryExperience).HasMaxLength(1000); b.Property(x => x.CareerGoals).HasMaxLength(1000); b.Property(x => x.VolunteeringInterests).HasMaxLength(1000); b.Property(x => x.Hobbies).HasMaxLength(1000); b.Property(x => x.Languages).HasMaxLength(500); b.Property(x => x.ProfileImageUrl).HasMaxLength(1000); b.Property(x => x.CoverImageUrl).HasMaxLength(1000); }
}

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> b) { b.ToTable("Skills"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(100).IsRequired(); b.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired(); b.HasIndex(x => x.NormalizedName).IsUnique(); }
}
public sealed class InterestConfiguration : IEntityTypeConfiguration<Interest>
{
    public void Configure(EntityTypeBuilder<Interest> b) { b.ToTable("Interests"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(100).IsRequired(); b.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired(); b.HasIndex(x => x.NormalizedName).IsUnique(); }
}
public sealed class UserSkillConfiguration : IEntityTypeConfiguration<UserSkill>
{
    public void Configure(EntityTypeBuilder<UserSkill> b) { b.ToTable("UserSkills"); b.HasKey(x => new { x.UserId, x.SkillId }); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); b.HasOne<Skill>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class UserInterestConfiguration : IEntityTypeConfiguration<UserInterest>
{
    public void Configure(EntityTypeBuilder<UserInterest> b) { b.ToTable("UserInterests"); b.HasKey(x => new { x.UserId, x.InterestId }); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); b.HasOne<Interest>().WithMany().HasForeignKey(x => x.InterestId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class ConnectionRequestConfiguration : IEntityTypeConfiguration<ConnectionRequest>
{
    public void Configure(EntityTypeBuilder<ConnectionRequest> b) { b.ToTable("ConnectionRequests"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.SenderId, x.RecipientId, x.Status }); b.HasOne<User>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict); b.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class ConnectionConfiguration : IEntityTypeConfiguration<Connection>
{
    public void Configure(EntityTypeBuilder<Connection> b) { b.ToTable("Connections"); b.HasKey(x => new { x.UserId, x.ConnectedUserId }); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); b.HasOne<User>().WithMany().HasForeignKey(x => x.ConnectedUserId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class BlockedUserConfiguration : IEntityTypeConfiguration<BlockedUser>
{
    public void Configure(EntityTypeBuilder<BlockedUser> b) { b.ToTable("BlockedUsers"); b.HasKey(x => new { x.BlockerId, x.BlockedId }); b.HasOne<User>().WithMany().HasForeignKey(x => x.BlockerId).OnDelete(DeleteBehavior.Restrict); b.HasOne<User>().WithMany().HasForeignKey(x => x.BlockedId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CommunityConfiguration : IEntityTypeConfiguration<Community>
{
    public void Configure(EntityTypeBuilder<Community> b) { b.ToTable("Communities"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(160).IsRequired(); b.Property(x => x.Description).HasMaxLength(3000); b.HasIndex(x => new { x.UniversityId, x.Visibility, x.IsActive }); b.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class CommunityMemberConfiguration : IEntityTypeConfiguration<CommunityMember>
{
    public void Configure(EntityTypeBuilder<CommunityMember> b) { b.ToTable("CommunityMembers"); b.HasKey(x => new { x.CommunityId, x.UserId }); b.HasOne<Community>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Cascade); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); }
}
