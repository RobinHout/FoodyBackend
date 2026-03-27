using System.Security.Claims;

namespace FoodyBackend.Auth;

public static class CurrentUserExtensions
{
    public static int? GetCurrentUserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(AuthConstants.UserIdClaimType);
        return int.TryParse(claim, out var userId) ? userId : null;
    }

    public static int? GetCurrentSessionId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(AuthConstants.SessionIdClaimType);
        return int.TryParse(claim, out var sessionId) ? sessionId : null;
    }
}
