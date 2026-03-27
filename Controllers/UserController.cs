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
public class UserController(
    DatabaseContext context,
    IPasswordHasher<User> passwordHasher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await context.Users
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => new UserResponse(user.Id, user.Username))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponse>> GetUser(int id, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new UserResponse(item.Id, item.Username))
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> PutUser(
        int id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
        {
            return BadRequest();
        }

        var currentUserId = User.GetCurrentUserId();
        if (currentUserId != id)
        {
            return Forbid();
        }

        var validationError = ValidateUsername(request.Username);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var user = await context.Users.FindAsync([id], cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var username = request.Username.Trim();
        var usernameInUse = await context.Users.AnyAsync(
            item => item.Id != id && item.Username == username,
            cancellationToken);
        if (usernameInUse)
        {
            return Conflict("A user with this username already exists.");
        }

        user.Username = username;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> PostUser(
        CreateUserRequest request,
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

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user.ToResponse());
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        if (currentUserId != id)
        {
            return Forbid();
        }

        var user = await context.Users.FindAsync([id], cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        context.Users.Remove(user);
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static string? ValidateUsername(string username)
    {
        return string.IsNullOrWhiteSpace(username)
            ? "Username is required."
            : null;
    }

    private static string? ValidateCredentials(string username, string password)
    {
        var usernameError = ValidateUsername(username);
        if (usernameError is not null)
        {
            return usernameError;
        }

        return string.IsNullOrWhiteSpace(password)
            ? "Password is required."
            : null;
    }
}
