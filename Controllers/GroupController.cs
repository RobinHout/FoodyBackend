using FoodyBackend.Auth;
using FoodyBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Controllers;

[Route("api/[controller]")]
[ApiController]
public class GroupController(DatabaseContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Group>>> GetGroups(CancellationToken cancellationToken)
    {
        return Ok(await context.Groups
            .AsNoTracking()
            .OrderBy(group => group.Id)
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Group>> GetGroup(int id, CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return group is null ? NotFound() : Ok(group);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> PutGroup(int id, Group group, CancellationToken cancellationToken)
    {
        if (id != group.Id)
        {
            return BadRequest();
        }

        var existingGroup = await context.Groups.FindAsync([id], cancellationToken);
        if (existingGroup is null)
        {
            return NotFound();
        }

        if (!await IsCurrentUserMemberOfGroupAsync(id, cancellationToken))
        {
            return Forbid();
        }

        existingGroup.Name = group.Name;
        existingGroup.Description = group.Description;

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Group>> PostGroup(Group group, CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        context.Groups.Add(group);
        await context.SaveChangesAsync(cancellationToken);

        context.UserGroups.Add(new UserGroup
        {
            UserId = currentUserId.Value,
            GroupId = group.Id
        });
        await context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, group);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGroup(int id, CancellationToken cancellationToken)
    {
        var group = await context.Groups.FindAsync([id], cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        if (!await IsCurrentUserMemberOfGroupAsync(id, cancellationToken))
        {
            return Forbid();
        }

        context.Groups.Remove(group);
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<bool> IsCurrentUserMemberOfGroupAsync(int groupId, CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        return currentUserId.HasValue && await context.UserGroups.AnyAsync(
            link => link.UserId == currentUserId.Value && link.GroupId == groupId,
            cancellationToken);
    }
}
