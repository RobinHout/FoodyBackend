using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FoodyBackend.Auth;

public sealed class BearerSessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly DatabaseContext _context;

    public BearerSessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DatabaseContext context)
        : base(options, logger, encoder)
    {
        _context = context;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationValues))
        {
            return AuthenticateResult.NoResult();
        }

        var authorizationHeader = authorizationValues.ToString();
        if (!authorizationHeader.StartsWith($"{AuthConstants.Scheme} ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorizationHeader[(AuthConstants.Scheme.Length + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.Fail("Missing bearer token.");
        }

        var tokenHash = TokenHasher.Hash(token);
        var now = DateTime.UtcNow;

        var session = await _context.AuthSessions
            .AsNoTracking()
            .Include(item => item.User)
            .FirstOrDefaultAsync(item =>
                item.AccessTokenHash == tokenHash &&
                item.RevokedAtUtc == null &&
                item.AccessTokenExpiresAtUtc > now);

        if (session?.User is null)
        {
            return AuthenticateResult.Fail("Invalid or expired access token.");
        }

        var claims = new List<Claim>
        {
            new(AuthConstants.UserIdClaimType, session.UserId.ToString()),
            new(ClaimTypes.Name, session.User.Username),
            new(AuthConstants.SessionIdClaimType, session.Id.ToString()),
            new(AuthConstants.AccessTokenExpiresAtClaimType, session.AccessTokenExpiresAtUtc.ToString("O"))
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
