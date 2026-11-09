using System.Security.Claims;
using UniConnect.Application.Common;

namespace UniConnect.Api.Security;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? accessor.HttpContext?.User.FindFirstValue("sub"), out var id) ? id : null;
    public bool IsInRole(string role) => accessor.HttpContext?.User.IsInRole(role) ?? false;
}
