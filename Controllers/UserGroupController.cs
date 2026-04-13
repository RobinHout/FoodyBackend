using FoodyBackend.Auth;
using FoodyBackend.Models;
using FoodyBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserGroupController(
    DatabaseContext context,
    IDinnerRecommendationService recommendationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserGroupResponse>>> GetUserGroups(CancellationToken cancellationToken)
    {
        return Ok(await BuildQuery(context.UserGroups)
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserGroupResponse>> GetUserGroup(int id, CancellationToken cancellationToken)
    {
        var userGroup = await BuildQuery(context.UserGroups
                .Where(link => link.Id == id))
            .FirstOrDefaultAsync(cancellationToken);
        return userGroup is null ? NotFound() : Ok(userGroup);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<UserGroupResponse>>> GetUserGroupsByUser(int userId, CancellationToken cancellationToken)
    {
        if (!await context.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return NotFound($"User with id {userId} was not found.");
        }

        return Ok(await BuildQuery(context.UserGroups
            .Where(link => link.UserId == userId))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("group/{groupId}")]
    public async Task<ActionResult<IEnumerable<UserGroupResponse>>> GetUserGroupsByGroup(int groupId, CancellationToken cancellationToken)
    {
        if (!await context.Groups.AnyAsync(group => group.Id == groupId, cancellationToken))
        {
            return NotFound($"Group with id {groupId} was not found.");
        }

        return Ok(await BuildQuery(context.UserGroups
            .Where(link => link.GroupId == groupId))
            .ToListAsync(cancellationToken));
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<UserGroupResponse>> PostUserGroup(UserGroup userGroup, CancellationToken cancellationToken)
    {
        if (userGroup.UserId <= 0 || userGroup.GroupId <= 0)
        {
            return BadRequest("UserId and GroupId are required.");
        }

        if (!await IsCurrentUserMemberOfGroupAsync(userGroup.GroupId, cancellationToken))
        {
            return Forbid();
        }

        if (!await context.Users.AnyAsync(user => user.Id == userGroup.UserId, cancellationToken))
        {
            return BadRequest($"User with id {userGroup.UserId} was not found.");
        }

        if (!await context.Groups.AnyAsync(group => group.Id == userGroup.GroupId, cancellationToken))
        {
            return BadRequest($"Group with id {userGroup.GroupId} was not found.");
        }

        var alreadyExists = await context.UserGroups.AnyAsync(link =>
            link.UserId == userGroup.UserId && link.GroupId == userGroup.GroupId,
            cancellationToken);
        if (alreadyExists)
        {
            return Conflict("This user is already connected to this group.");
        }

        var link = new UserGroup
        {
            UserId = userGroup.UserId,
            GroupId = userGroup.GroupId
        };

        context.UserGroups.Add(link);
        await context.SaveChangesAsync(cancellationToken);
        await recommendationService.RefreshGroupDinnerRecommendationsAsync(link.GroupId, cancellationToken);

        var created = await BuildQuery(context.UserGroups
                .Where(item => item.Id == link.Id))
            .FirstAsync(cancellationToken);
        return CreatedAtAction(nameof(GetUserGroup), new { id = link.Id }, created);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUserGroup(int id, CancellationToken cancellationToken)
    {
        var userGroup = await context.UserGroups.FirstOrDefaultAsync(link => link.Id == id, cancellationToken);
        if (userGroup == null)
        {
            return NotFound();
        }

        var currentUserId = User.GetCurrentUserId();
        var canRemove = currentUserId == userGroup.UserId ||
            await IsCurrentUserMemberOfGroupAsync(userGroup.GroupId, cancellationToken);
        if (!canRemove)
        {
            return Forbid();
        }

        var groupId = userGroup.GroupId;
        context.UserGroups.Remove(userGroup);
        await context.SaveChangesAsync(cancellationToken);
        await recommendationService.RefreshGroupDinnerRecommendationsAsync(groupId, cancellationToken);

        return NoContent();
    }

    private IQueryable<UserGroupResponse> BuildQuery(IQueryable<UserGroup> source)
    {
        return source
            .AsNoTracking()
            .OrderBy(link => link.Id)
            .Select(link => new UserGroupResponse(
                link.Id,
                link.UserId,
                link.User!.Username,
                link.GroupId,
                link.Group!.Name,
                link.Group.Description));
    }

    private async Task<bool> IsCurrentUserMemberOfGroupAsync(int groupId, CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        return currentUserId.HasValue && await context.UserGroups.AnyAsync(
            link => link.UserId == currentUserId.Value && link.GroupId == groupId,
            cancellationToken);
    }

    public sealed record UserGroupResponse(
        int Id,
        int UserId,
        string Username,
        int GroupId,
        string GroupName,
        string GroupDescription);
}
