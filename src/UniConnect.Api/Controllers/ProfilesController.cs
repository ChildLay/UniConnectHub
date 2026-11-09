using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Profiles;
using UniConnect.Api.Media;

namespace UniConnect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/profiles")]
[Produces("application/json")]
public sealed class ProfilesController(IProfileService profiles, IProfileMediaStorage media) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    public Task<ProfileResponse> GetMe(CancellationToken cancellationToken) => profiles.GetAsync(GetUserId(), GetUserId(), cancellationToken);

    [HttpPut("me")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    public Task<ProfileResponse> UpdateMe(UpdateProfileRequest request, CancellationToken cancellationToken) => profiles.UpdateAsync(GetUserId(), request, cancellationToken);

    [HttpPost("me/media/{kind}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(FileProfileMediaStorage.MaxRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileProfileMediaStorage.MaxRequestSize)]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    public Task<ProfileResponse> UploadMedia([FromRoute] string kind, IFormFile file, CancellationToken cancellationToken) => UploadAsync(GetUserId(), kind, file, cancellationToken);

    [HttpDelete("me/media/{kind}")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    public async Task<ProfileResponse> DeleteMedia([FromRoute] string kind, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var current = await profiles.GetAsync(userId, userId, cancellationToken);
        var oldUrl = kind switch { "profile" => current.ProfileImageUrl, "cover" => current.CoverImageUrl, _ => throw new ArgumentException("Image kind must be profile or cover.") };
        var result = kind == "profile"
            ? await profiles.SetProfileImageAsync(userId, null, cancellationToken)
            : await profiles.SetCoverImageAsync(userId, null, cancellationToken);
        await media.DeleteAsync(userId, kind, oldUrl, cancellationToken);
        return result;
    }

    [HttpGet("{userId:guid}/media/{kind}/{fileName}")]
    [Produces("image/jpeg", "image/png", "image/webp")]
    public async Task<IActionResult> GetMedia(Guid userId, [FromRoute] string kind, [FromRoute] string fileName, CancellationToken cancellationToken)
    {
        var viewerId = GetUserId();
        var profile = await profiles.GetAsync(viewerId, userId, cancellationToken);
        var expectedUrl = kind switch { "profile" => profile.ProfileImageUrl, "cover" => profile.CoverImageUrl, _ => throw new ArgumentException("Image kind must be profile or cover.") };
        if (expectedUrl is null || !expectedUrl.EndsWith("/" + fileName, StringComparison.Ordinal)) throw new KeyNotFoundException("Image was not found.");
        var image = await media.OpenReadAsync(userId, kind, fileName, cancellationToken) ?? throw new KeyNotFoundException("Image was not found.");
        Response.Headers["Cache-Control"] = "private, no-store";
        return File(image.Stream, image.ContentType);
    }

    private async Task<ProfileResponse> UploadAsync(Guid userId, string kind, IFormFile file, CancellationToken cancellationToken)
    {
        if (kind is not ("profile" or "cover")) throw new ArgumentException("Image kind must be profile or cover.");
        var current = await profiles.GetAsync(userId, userId, cancellationToken);
        var oldUrl = kind == "profile" ? current.ProfileImageUrl : current.CoverImageUrl;
        var newUrl = await media.SaveAsync(userId, kind, file, cancellationToken);
        try
        {
            var result = kind == "profile"
                ? await profiles.SetProfileImageAsync(userId, newUrl, cancellationToken)
                : await profiles.SetCoverImageAsync(userId, newUrl, cancellationToken);
            await media.DeleteAsync(userId, kind, oldUrl, cancellationToken);
            return result;
        }
        catch
        {
            await media.DeleteAsync(userId, kind, newUrl, cancellationToken);
            throw;
        }
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    public Task<ProfileResponse> Get(Guid userId, CancellationToken cancellationToken) => profiles.GetAsync(GetUserId(), userId, cancellationToken);

    [HttpPut("me/skills")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    public Task<ProfileResponse> SetSkills(StringValuesRequest request, CancellationToken cancellationToken) => profiles.SetSkillsAsync(GetUserId(), request.Values, cancellationToken);

    [HttpPut("me/interests")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    public Task<ProfileResponse> SetInterests(StringValuesRequest request, CancellationToken cancellationToken) => profiles.SetInterestsAsync(GetUserId(), request.Values, cancellationToken);

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("User identity is missing.");
    }
}

public sealed record StringValuesRequest(IReadOnlyCollection<string> Values);

