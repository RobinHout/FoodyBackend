using FoodyBackend.Contracts;
using FoodyBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Auth;

public interface IAuthSessionService
{
    Task<AuthTokenResponse> CreateSessionAsync(User user, CancellationToken cancellationToken = default);
    Task<AuthTokenResponse?> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> RevokeSessionAsync(int sessionId, int userId, CancellationToken cancellationToken = default);
}

public sealed class AuthSessionService(DatabaseContext context) : IAuthSessionService
{
    public async Task<AuthTokenResponse> CreateSessionAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var accessToken = TokenHasher.GenerateToken();
        var refreshToken = TokenHasher.GenerateToken();

        var session = new AuthSession
        {
            UserId = user.Id,
            AccessTokenHash = TokenHasher.Hash(accessToken),
            RefreshTokenHash = TokenHasher.Hash(refreshToken),
            AccessTokenExpiresAtUtc = now.Add(AuthConstants.AccessTokenLifetime),
            RefreshTokenExpiresAtUtc = now.Add(AuthConstants.RefreshTokenLifetime),
            CreatedAtUtc = now
        };

        context.AuthSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);

        return ToResponse(accessToken, refreshToken, session);
    }

    public async Task<AuthTokenResponse?> RefreshSessionAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var refreshTokenHash = TokenHasher.Hash(refreshToken);

        var existingSession = await context.AuthSessions.FirstOrDefaultAsync(
            session => session.RefreshTokenHash == refreshTokenHash,
            cancellationToken);

        if (existingSession is null ||
            existingSession.RevokedAtUtc is not null ||
            existingSession.RefreshTokenExpiresAtUtc <= now)
        {
            return null;
        }

        existingSession.RevokedAtUtc = now;
        existingSession.LastRefreshedAtUtc = now;

        var accessToken = TokenHasher.GenerateToken();
        var newRefreshToken = TokenHasher.GenerateToken();

        var replacementSession = new AuthSession
        {
            UserId = existingSession.UserId,
            AccessTokenHash = TokenHasher.Hash(accessToken),
            RefreshTokenHash = TokenHasher.Hash(newRefreshToken),
            AccessTokenExpiresAtUtc = now.Add(AuthConstants.AccessTokenLifetime),
            RefreshTokenExpiresAtUtc = now.Add(AuthConstants.RefreshTokenLifetime),
            CreatedAtUtc = now
        };

        context.AuthSessions.Add(replacementSession);
        await context.SaveChangesAsync(cancellationToken);

        return ToResponse(accessToken, newRefreshToken, replacementSession);
    }

    public async Task<bool> RevokeSessionAsync(
        int sessionId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var session = await context.AuthSessions.FirstOrDefaultAsync(
            item => item.Id == sessionId && item.UserId == userId,
            cancellationToken);

        if (session is null)
        {
            return false;
        }

        if (session.RevokedAtUtc is null)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static AuthTokenResponse ToResponse(
        string accessToken,
        string refreshToken,
        AuthSession session)
    {
        return new AuthTokenResponse(
            "Bearer",
            accessToken,
            session.AccessTokenExpiresAtUtc,
            refreshToken,
            session.RefreshTokenExpiresAtUtc);
    }
}
