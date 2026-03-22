using FoodyBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserGroupController(DatabaseContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserGroupResponse>>> GetUserGroups()
    {
        return Ok(await BuildQuery().ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserGroupResponse>> GetUserGroup(int id)
    {
        var userGroup = await BuildQuery().FirstOrDefaultAsync(link => link.Id == id);
        return userGroup is null ? NotFound() : Ok(userGroup);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<UserGroupResponse>>> GetUserGroupsByUser(int userId)
    {
        if (!await context.Users.AnyAsync(user => user.Id == userId))
        {
            return NotFound($"User with id {userId} was not found.");
        }

        return Ok(await BuildQuery()
            .Where(link => link.UserId == userId)
            .ToListAsync());
    }

    [HttpGet("group/{groupId}")]
    public async Task<ActionResult<IEnumerable<UserGroupResponse>>> GetUserGroupsByGroup(int groupId)
    {
        if (!await context.Groups.AnyAsync(group => group.Id == groupId))
        {
            return NotFound($"Group with id {groupId} was not found.");
        }

        return Ok(await BuildQuery()
            .Where(link => link.GroupId == groupId)
            .ToListAsync());
    }

    [HttpPost]
    public async Task<ActionResult<UserGroupResponse>> PostUserGroup(UserGroup userGroup)
    {
        if (userGroup.UserId <= 0 || userGroup.GroupId <= 0)
        {
            return BadRequest("UserId and GroupId are required.");
        }

        if (!await context.Users.AnyAsync(user => user.Id == userGroup.UserId))
        {
            return BadRequest($"User with id {userGroup.UserId} was not found.");
        }

        if (!await context.Groups.AnyAsync(group => group.Id == userGroup.GroupId))
        {
            return BadRequest($"Group with id {userGroup.GroupId} was not found.");
        }

        var alreadyExists = await context.UserGroups.AnyAsync(link =>
            link.UserId == userGroup.UserId && link.GroupId == userGroup.GroupId);
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
        await context.SaveChangesAsync();

        var created = await BuildQuery().FirstAsync(item => item.Id == link.Id);
        return CreatedAtAction(nameof(GetUserGroup), new { id = link.Id }, created);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUserGroup(int id)
    {
        var userGroup = await context.UserGroups.FindAsync(id);
        if (userGroup == null)
        {
            return NotFound();
        }

        context.UserGroups.Remove(userGroup);
        await context.SaveChangesAsync();

        return NoContent();
    }

    private IQueryable<UserGroupResponse> BuildQuery()
    {
        return context.UserGroups
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

    public sealed record UserGroupResponse(
        int Id,
        int UserId,
        string Username,
        int GroupId,
        string GroupName,
        string GroupDescription);
}
