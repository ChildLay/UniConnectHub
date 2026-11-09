using UniConnect.Domain.Common;

namespace UniConnect.Domain.Universities;

public sealed class University : Entity<Guid>, IAuditableEntity
{
    private University() : base(Guid.Empty) { }

    public University(string name, string? shortName, string country, string? city, string? websiteUrl, DateTime nowUtc)
        : base(Guid.NewGuid())
    {
        Name = Required(name, nameof(name), 200);
        NormalizedName = Normalize(Name);
        ShortName = Optional(shortName, 50);
        Country = Required(country, nameof(country), 100);
        City = Optional(city, 100);
        WebsiteUrl = Optional(websiteUrl, 500);
        IsActive = true;
        CreatedAtUtc = UpdatedAtUtc = nowUtc;
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? ShortName { get; private set; }
    public string Country { get; private set; } = string.Empty;
    public string? City { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string name, string? shortName, string country, string? city, string? websiteUrl, DateTime nowUtc)
    {
        Name = Required(name, nameof(name), 200);
        NormalizedName = Normalize(Name);
        ShortName = Optional(shortName, 50);
        Country = Required(country, nameof(country), 100);
        City = Optional(city, 100);
        WebsiteUrl = Optional(websiteUrl, 500);
        UpdatedAtUtc = nowUtc;
    }

    public void SetActive(bool active, DateTime nowUtc)
    {
        IsActive = active;
        UpdatedAtUtc = nowUtc;
    }

    internal static string Required(string value, string field, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, field);
        var result = value.Trim();
        if (result.Length > maxLength) throw new ArgumentOutOfRangeException(field, $"Must be no longer than {maxLength} characters.");
        return result;
    }

    internal static string? Optional(string? value, int maxLength)
    {
        var result = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (result?.Length > maxLength) throw new ArgumentOutOfRangeException(nameof(value), $"Must be no longer than {maxLength} characters.");
        return result;
    }

    internal static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
