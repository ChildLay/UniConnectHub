using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Auth;
using UniConnect.Application.Common;
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

namespace UniConnect.Infrastructure.Persistence;

public interface IDevelopmentDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

/// <summary>Seeds repeatable, local-only data for development and API exploration.</summary>
public sealed class DevelopmentDataSeeder(UniConnectDbContext db, IPasswordHashing passwordHashing, IClock clock) : IDevelopmentDataSeeder
{
    private const string DemoPassword = "DemoPass123!";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var university = await db.Universities.SingleOrDefaultAsync(x => x.NormalizedName == "UNICONNECT DEMO UNIVERSITY", cancellationToken);
        if (university is null)
        {
            university = new University("UniConnect Demo University", "UDU", "New Zealand", "Auckland", "https://example.edu", now);
            await db.Universities.AddAsync(university, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var admin = await GetOrCreateUserAsync("admin@demo.uniconnecthub.local", "Avery", "Admin", JourneyStage.Graduate, UserRole.Admin, university, now, cancellationToken);
        var student = await GetOrCreateUserAsync("student@demo.uniconnecthub.local", "Sam", "Student", JourneyStage.CurrentStudent, UserRole.User, university, now, cancellationToken);
        var alumni = await GetOrCreateUserAsync("alumni@demo.uniconnecthub.local", "Alex", "Alumni", JourneyStage.Alumni, UserRole.User, university, now, cancellationToken);
        var graduate = await GetOrCreateUserAsync("graduate@demo.uniconnecthub.local", "Priya", "Sharma", JourneyStage.Graduate, UserRole.User, university, now, cancellationToken);
        var currentStudent = await GetOrCreateUserAsync("student2@demo.uniconnecthub.local", "Jordan", "Lee", JourneyStage.CurrentStudent, UserRole.User, university, now, cancellationToken);
        var newStudent = await GetOrCreateUserAsync("student3@demo.uniconnecthub.local", "Casey", "Morgan", JourneyStage.NewStudent, UserRole.User, university, now, cancellationToken);
        var alumnus = await GetOrCreateUserAsync("alumni2@demo.uniconnecthub.local", "Taylor", "Ngata", JourneyStage.Alumni, UserRole.User, university, now, cancellationToken);
        var graduate2 = await GetOrCreateUserAsync("graduate2@demo.uniconnecthub.local", "Jamie", "Patel", JourneyStage.Graduate, UserRole.User, university, now, cancellationToken);
        var reportTester1 = await GetOrCreateUserAsync("reporter1@demo.uniconnecthub.local", "Morgan", "ReportTester", JourneyStage.CurrentStudent, UserRole.User, university, now, cancellationToken);
        var reportTester2 = await GetOrCreateUserAsync("reporter2@demo.uniconnecthub.local", "Riley", "ReportTester", JourneyStage.Alumni, UserRole.User, university, now, cancellationToken);

        await EnsureProfileAsync(admin, "Computer Science", "Software Engineering", "Building secure community platforms", now, cancellationToken);
        await EnsureProfileAsync(student, "Information Systems", "Data Science", "Looking for a data internship", now, cancellationToken);
        await EnsureProfileAsync(alumni, "Business", "Product Management", "Mentoring graduates and new founders", now, cancellationToken);
        await EnsureProfileAsync(graduate, "Software Engineering", "Cloud Computing", "Building accessible digital products", now, cancellationToken);
        await EnsureProfileAsync(currentStudent, "Information Technology", "Cybersecurity", "Finding a security internship", now, cancellationToken);
        await EnsureProfileAsync(newStudent, "Environmental Science", "Sustainability", "Exploring sustainability research", now, cancellationToken);
        await EnsureProfileAsync(alumnus, "Communications", "Community Development", "Supporting early career graduates", now, cancellationToken);
        await EnsureProfileAsync(graduate2, "Design", "Human-centred Design", "Growing a product design portfolio", now, cancellationToken);
        await EnsureProfileAsync(reportTester1, "Information Technology", "Community Safety", "Learning how to keep online communities welcoming", now, cancellationToken);
        await EnsureProfileAsync(reportTester2, "Communications", "Digital Communities", "Helping communities build respectful spaces", now, cancellationToken);
        await EnsureTaxonomyAsync(admin.Id, ["C#", "ASP.NET Core", "SQL Server"], ["Mentoring", "Open source"], cancellationToken);
        await EnsureTaxonomyAsync(student.Id, ["C#", "Python", "SQL Server"], ["Data science", "Open source"], cancellationToken);
        await EnsureTaxonomyAsync(alumni.Id, ["Product strategy", "Leadership"], ["Mentoring", "Startups"], cancellationToken);
        await EnsureTaxonomyAsync(graduate.Id, ["C#", "Azure", "Cloud"], ["Open source", "Mentoring"], cancellationToken);
        await EnsureTaxonomyAsync(currentStudent.Id, ["Python", "Security", "Networking"], ["Cybersecurity", "Volunteering"], cancellationToken);
        await EnsureTaxonomyAsync(newStudent.Id, ["Research", "Communication"], ["Sustainability", "Campus life"], cancellationToken);
        await EnsureTaxonomyAsync(alumnus.Id, ["Communications", "Leadership"], ["Community", "Mentoring"], cancellationToken);
        await EnsureTaxonomyAsync(graduate2.Id, ["Figma", "Research", "Prototyping"], ["Design", "Startups"], cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var usersToConnect = new Connection(admin.Id, student.Id, now);
        if (!await db.Connections.AnyAsync(x => (x.UserId == usersToConnect.UserId && x.ConnectedUserId == usersToConnect.ConnectedUserId), cancellationToken))
            await db.Connections.AddAsync(usersToConnect, cancellationToken);

        await EnsureConnectionAsync(admin.Id, graduate.Id, now, cancellationToken);
        await EnsureConnectionAsync(student.Id, alumni.Id, now, cancellationToken);
        await EnsureConnectionRequestAsync(currentStudent.Id, admin.Id, now, cancellationToken);
        await EnsureConnectionRequestAsync(admin.Id, alumnus.Id, now, cancellationToken);

        var techCommunity = await EnsureCommunityAsync(university.Id, "Auckland Tech Community",
            "A place for students and alumni to share projects, events, and advice.", admin.Id, CommunityVisibility.Public,
            [(admin.Id, CommunityMemberRole.Admin), (student.Id, CommunityMemberRole.Member), (alumni.Id, CommunityMemberRole.Member)], now, cancellationToken);
        await EnsureCommunityAsync(university.Id, "Data Science Society",
            "Study groups, data projects, and peer learning for the university community.", student.Id, CommunityVisibility.UniversityOnly,
            [(student.Id, CommunityMemberRole.Admin), (admin.Id, CommunityMemberRole.Member)], now, cancellationToken);
        await EnsureCommunityAsync(university.Id, "Alumni Career Mentoring",
            "A small private space for alumni mentoring conversations and career support.", alumni.Id, CommunityVisibility.Private,
            [(alumni.Id, CommunityMemberRole.Admin), (admin.Id, CommunityMemberRole.Member)], now, cancellationToken);
        await EnsureCommunityAsync(university.Id, "Student Founders Network",
            "Connect with student founders, share early ideas, and find collaborators.", student.Id, CommunityVisibility.Public,
            [(student.Id, CommunityMemberRole.Admin), (alumni.Id, CommunityMemberRole.Moderator)], now, cancellationToken);
        await EnsureCommunityAsync(university.Id, "Campus Events and Volunteering",
            "Discover campus activities, volunteer opportunities, and community events.", admin.Id, CommunityVisibility.UniversityOnly,
            [(admin.Id, CommunityMemberRole.Admin), (student.Id, CommunityMemberRole.Member), (alumni.Id, CommunityMemberRole.Member)], now, cancellationToken);

        var communityPostText = "Our Auckland Tech Community is open. Introduce yourself and share what you are building.";
        var communityPost = await db.Posts.SingleOrDefaultAsync(x => x.AuthorId == admin.Id && x.Content == communityPostText, cancellationToken);
        if (communityPost is null)
            await db.Posts.AddAsync(new Post(admin.Id, university.Id, communityPostText, PostAudience.CommunityMembers, now, techCommunity.Id), cancellationToken);

        var post = await db.Posts.SingleOrDefaultAsync(x => x.AuthorId == admin.Id && x.Content == "Welcome to UniConnect Hub! Share what you are learning and connect with your university community.", cancellationToken);
        if (post is null)
        {
            post = new Post(admin.Id, university.Id, "Welcome to UniConnect Hub! Share what you are learning and connect with your university community.", PostAudience.UniversityCommunity, now);
            await db.Posts.AddAsync(post, cancellationToken);
        }
        if (!await db.Comments.AnyAsync(x => x.PostId == post.Id && x.AuthorId == student.Id, cancellationToken))
            await db.Comments.AddAsync(new Comment(post.Id, student.Id, "Excited to meet other people working on community projects!", now), cancellationToken);
        if (!await db.PostReactions.AnyAsync(x => x.PostId == post.Id && x.UserId == student.Id, cancellationToken))
            await db.PostReactions.AddAsync(new PostReaction(post.Id, student.Id, ReactionType.Celebrate, now), cancellationToken);

        var eventEntity = await db.Events.SingleOrDefaultAsync(x => x.HostId == admin.Id && x.Title == "UniConnect Community Welcome Meetup", cancellationToken);
        if (eventEntity is null)
        {
            eventEntity = new Event("UniConnect Community Welcome Meetup", "Meet students and alumni, share current projects, and learn how to use UniConnect Hub.", admin.Id, university.Id,
                now.AddDays(7), now.AddDays(7).AddHours(2), 40, EventType.Hybrid, now, techCommunity.Id);
            eventEntity.Publish(now);
            await db.Events.AddAsync(eventEntity, cancellationToken);
        }
        if (!await db.EventRegistrations.AnyAsync(x => x.EventId == eventEntity.Id && x.UserId == student.Id, cancellationToken))
            await db.EventRegistrations.AddAsync(new EventRegistration(eventEntity.Id, student.Id, EventRegistrationStatus.Registered, now), cancellationToken);

        await EnsureMentorAsync(alumni.Id, now, cancellationToken);
        if (!await db.MentorshipRequests.AnyAsync(x => x.MentorId == alumni.Id && x.MenteeId == student.Id, cancellationToken))
            await db.MentorshipRequests.AddAsync(new MentorshipRequest(alumni.Id, student.Id, "I would appreciate advice on starting a career in product management.", now), cancellationToken);

        var opportunity = await db.Opportunities.SingleOrDefaultAsync(x => x.CreatorId == admin.Id && x.Title == "Graduate Software Developer (Demo)", cancellationToken);
        if (opportunity is null)
        {
            opportunity = new Opportunity("Graduate Software Developer (Demo)", "A sample graduate role for exploring the opportunity and save workflows.", admin.Id, university.Id,
                OpportunityType.GraduateRole, now.AddMonths(2), "https://example.edu/careers", now);
            await db.Opportunities.AddAsync(opportunity, cancellationToken);
        }
        if (!await db.SavedOpportunities.AnyAsync(x => x.OpportunityId == opportunity.Id && x.UserId == student.Id, cancellationToken))
            await db.SavedOpportunities.AddAsync(new SavedOpportunity(opportunity.Id, student.Id, now), cancellationToken);

        var demoOpportunities = new[]
        {
            (Title: "Summer Data Analyst Internship (Demo)", Description: "Join a student-friendly analytics team to explore datasets, build dashboards, and present useful insights. Suitable for students developing SQL and Python skills.", Type: OpportunityType.Internship, Days: 45, Url: "https://careers.example.edu/data-internship"),
            (Title: "Graduate UX Designer (Demo)", Description: "Help a product team improve digital services through user research, prototyping, accessibility reviews, and thoughtful collaboration with engineers.", Type: OpportunityType.GraduateRole, Days: 70, Url: "https://careers.example.edu/graduate-ux"),
            (Title: "Part-time Campus IT Assistant (Demo)", Description: "Support students and staff with everyday technology questions, device setup, and clear how-to documentation. Flexible hours during semester.", Type: OpportunityType.Job, Days: 35, Url: "https://careers.example.edu/campus-it"),
            (Title: "Community Garden Volunteer Team (Demo)", Description: "Work with students and neighbours on a welcoming campus garden. No experience is needed; learn practical growing and community engagement skills.", Type: OpportunityType.Volunteering, Days: 90, Url: "https://careers.example.edu/garden-volunteers"),
            (Title: "Responsible AI for Students Workshop (Demo)", Description: "A practical introduction to responsible AI, privacy, and checking generated information. Includes guided activities and time for questions.", Type: OpportunityType.Workshop, Days: 55, Url: "https://careers.example.edu/responsible-ai-workshop"),
            (Title: "Auckland Alumni Networking Evening (Demo)", Description: "Meet graduates across technology, business, design, and public service. Bring a question, learn about different career paths, and make a few new connections.", Type: OpportunityType.Networking, Days: 30, Url: "https://careers.example.edu/alumni-networking"),
            (Title: "Student Research Support Scholarship (Demo)", Description: "A sample scholarship for undergraduate students contributing to a university research project. Applicants should include a short statement of interest.", Type: OpportunityType.Scholarship, Days: 60, Url: "https://careers.example.edu/research-scholarship"),
            (Title: "Career Ready: Interview Skills (Demo)", Description: "Build confidence with structured interview practice, feedback, and preparation tips led by career advisers and alumni volunteers.", Type: OpportunityType.ProfessionalDevelopment, Days: 50, Url: "https://careers.example.edu/interview-skills"),
            (Title: "Campus Sustainability Challenge (Demo)", Description: "Join a small team to propose a practical idea that makes campus life more sustainable. Teams receive mentoring and present their ideas at a showcase.", Type: OpportunityType.Competition, Days: 75, Url: "https://careers.example.edu/sustainability-challenge")
        };
        foreach (var item in demoOpportunities)
        {
            if (!await db.Opportunities.AnyAsync(x => x.CreatorId == admin.Id && x.Title == item.Title, cancellationToken))
                await db.Opportunities.AddAsync(new Opportunity(item.Title, item.Description, admin.Id, university.Id, item.Type, now.AddDays(item.Days), item.Url, now), cancellationToken);
        }

        if (!await db.Notifications.AnyAsync(x => x.UserId == student.Id && x.Type == "Welcome", cancellationToken))
            await db.Notifications.AddAsync(new Notification(student.Id, "Welcome", "Welcome to UniConnect Hub", "Your demo account is ready. Explore your university community and upcoming event.", "Community", techCommunity.Id, now), cancellationToken);
        if (!await db.Reports.AnyAsync(x => x.ReporterId == student.Id && x.TargetType == "Post" && x.TargetId == post.Id, cancellationToken))
            await db.Reports.AddAsync(new Report(student.Id, "Post", post.Id, "Demo report for moderation workflow", "This is sample data for local API testing.", now), cancellationToken);
        var demoUserReports = new[]
        {
            (ReporterId: reportTester1.Id, Reason: "Other", Details: "Demo seed only: sample user report for testing the moderation workflow. No real issue is being alleged."),
            (ReporterId: reportTester2.Id, Reason: "Other", Details: "Demo seed only: a second reporter account for testing user reports. No real issue is being alleged.")
        };
        foreach (var report in demoUserReports)
        {
            if (!await db.Reports.AnyAsync(x => x.ReporterId == report.ReporterId && x.TargetType == "User" && x.TargetId == student.Id, cancellationToken))
                await db.Reports.AddAsync(new Report(report.ReporterId, "User", student.Id, report.Reason, report.Details, now), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureConnectionAsync(Guid firstUserId, Guid secondUserId, DateTime now, CancellationToken ct)
    {
        var connection = new Connection(firstUserId, secondUserId, now);
        if (!await db.Connections.AnyAsync(x => x.UserId == connection.UserId && x.ConnectedUserId == connection.ConnectedUserId, ct))
            await db.Connections.AddAsync(connection, ct);
    }

    private async Task EnsureConnectionRequestAsync(Guid senderId, Guid recipientId, DateTime now, CancellationToken ct)
    {
        if (!await db.ConnectionRequests.AnyAsync(x => x.SenderId == senderId && x.RecipientId == recipientId, ct))
            await db.ConnectionRequests.AddAsync(new ConnectionRequest(senderId, recipientId, now), ct);
    }

    private async Task<User> GetOrCreateUserAsync(string email, string firstName, string lastName, JourneyStage stage, UserRole role,
        University university, DateTime now, CancellationToken ct)
    {
        var normalizedEmail = email.ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);
        if (user is not null) return user;
        user = new User(email, passwordHashing.Hash(DemoPassword), firstName, lastName, stage, university, now);
        user.MarkEmailVerified(now);
        user.SetRole(role, now);
        await db.Users.AddAsync(user, ct);
        await db.Profiles.AddAsync(new UserProfile(user.Id, now), ct);
        await db.SaveChangesAsync(ct);
        return user;
    }

    private async Task EnsureProfileAsync(User user, string programme, string studyArea, string careerGoals, DateTime now, CancellationToken ct)
    {
        var profile = await db.Profiles.SingleOrDefaultAsync(x => x.Id == user.Id, ct);
        if (profile is null)
        {
            profile = new UserProfile(user.Id, now);
            await db.Profiles.AddAsync(profile, ct);
        }
        profile.Update("UniConnect Hub demo member", "Sample profile for local API testing.", programme, studyArea, null,
            "Early career experience", careerGoals, "Student mentoring", "Meetups and software projects", "English", profile.ProfileImageUrl,
            ProfileVisibility.UniversityCommunity, true, now);
    }

    private async Task EnsureTaxonomyAsync(Guid userId, IReadOnlyCollection<string> skills, IReadOnlyCollection<string> interests, CancellationToken ct)
    {
        foreach (var name in skills)
        {
            var normalized = name.Trim().ToUpperInvariant();
            var skill = await db.Skills.SingleOrDefaultAsync(x => x.NormalizedName == normalized, ct);
            if (skill is null) { skill = new Skill(name); await db.Skills.AddAsync(skill, ct); }
            if (!await db.UserSkills.AnyAsync(x => x.UserId == userId && x.SkillId == skill.Id, ct)) await db.UserSkills.AddAsync(new UserSkill(userId, skill.Id), ct);
        }
        foreach (var name in interests)
        {
            var normalized = name.Trim().ToUpperInvariant();
            var interest = await db.Interests.SingleOrDefaultAsync(x => x.NormalizedName == normalized, ct);
            if (interest is null) { interest = new Interest(name); await db.Interests.AddAsync(interest, ct); }
            if (!await db.UserInterests.AnyAsync(x => x.UserId == userId && x.InterestId == interest.Id, ct)) await db.UserInterests.AddAsync(new UserInterest(userId, interest.Id), ct);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureMemberAsync(Guid communityId, Guid userId, CommunityMemberRole role, DateTime now, CancellationToken ct)
    {
        if (!await db.CommunityMembers.AnyAsync(x => x.CommunityId == communityId && x.UserId == userId, ct)) await db.CommunityMembers.AddAsync(new CommunityMember(communityId, userId, role, now), ct);
    }

    private async Task<Community> EnsureCommunityAsync(Guid universityId, string name, string description, Guid ownerId,
        CommunityVisibility visibility, IReadOnlyCollection<(Guid UserId, CommunityMemberRole Role)> members, DateTime now, CancellationToken ct)
    {
        var community = await db.Communities.SingleOrDefaultAsync(x => x.UniversityId == universityId && x.Name == name, ct);
        if (community is null)
        {
            community = new Community(name, description, universityId, ownerId, visibility, now);
            await db.Communities.AddAsync(community, ct);
            await db.SaveChangesAsync(ct);
        }
        foreach (var (userId, role) in members)
            await EnsureMemberAsync(community.Id, userId, role, now, ct);
        await db.SaveChangesAsync(ct);
        return community;
    }

    private async Task EnsureMentorAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        if (!await db.MentorProfiles.AnyAsync(x => x.Id == userId, ct)) await db.MentorProfiles.AddAsync(new MentorProfile(userId, "Product management alumni mentor. Happy to help with career planning and interviews.", 3, now), ct);
    }
}

