using FoodyBackend.Auth;
using FoodyBackend.Contracts;
using FoodyBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(
    DatabaseContext context,
    IPasswordHasher<User> passwordHasher,
    IAuthSessionService authSessionService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthenticatedUserResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCredentials(request.Username, request.Password);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var username = request.Username.Trim();
        var alreadyExists = await context.Users.AnyAsync(
            user => user.Username == username,
            cancellationToken);

        if (alreadyExists)
        {
            return Conflict("A user with this username already exists.");
        }

        var user = new User
        {
            Username = username
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        var session = await authSessionService.CreateSessionAsync(user, cancellationToken);
        return Created($"/api/User/{user.Id}", new AuthenticatedUserResponse(user.ToResponse(), session));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthenticatedUserResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCredentials(request.Username, request.Password);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var username = request.Username.Trim();
        var user = await context.Users.FirstOrDefaultAsync(
            item => item.Username == username,
            cancellationToken);

        if (user is null)
        {
            return Unauthorized("Invalid username or password.");
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed &&
            !LegacyPasswordMigration.LooksHashed(user.PasswordHash) &&
            user.PasswordHash == request.Password)
        {
            verification = PasswordVerificationResult.SuccessRehashNeeded;
        }

        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized("Invalid username or password.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await context.SaveChangesAsync(cancellationToken);
        }

        var session = await authSessionService.CreateSessionAsync(user, cancellationToken);
        return Ok(new AuthenticatedUserResponse(user.ToResponse(), session));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.GetCurrentUserId();
        var sessionId = User.GetCurrentSessionId();
        if (!userId.HasValue || !sessionId.HasValue)
        {
            return Unauthorized();
        }

        var revoked = await authSessionService.RevokeSessionAsync(
            sessionId.Value,
            userId.Value,
            cancellationToken);

        return revoked ? NoContent() : NotFound();
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthTokenResponse>> RefreshToken(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest("RefreshToken is required.");
        }

        var session = await authSessionService.RefreshSessionAsync(
            request.RefreshToken,
            cancellationToken);

        return session is null
            ? Unauthorized("Invalid or expired refresh token.")
            : Ok(session);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = User.GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId.Value, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var groups = await context.UserGroups
            .AsNoTracking()
            .Where(link => link.UserId == userId.Value)
            .OrderBy(link => link.GroupId)
            .Select(link => new GroupSummary(
                link.GroupId,
                link.Group!.Name,
                link.Group.Description))
            .ToListAsync(cancellationToken);

        return Ok(new MeResponse(user.Id, user.Username, groups));
    }

    private static string? ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "Username is required.";
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return "Password is required.";
        }

        return null;
    }
}
