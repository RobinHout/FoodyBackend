using System.Security.Claims;

namespace FoodyBackend.Auth;

public static class AuthConstants
{
    public const string Scheme = "Bearer";
    public const string SessionIdClaimType = "foody:session_id";
    public const string AccessTokenExpiresAtClaimType = "foody:access_token_expires_at";
    public const string UserIdClaimType = ClaimTypes.NameIdentifier;

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
}
