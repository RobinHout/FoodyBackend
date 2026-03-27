using FoodyBackend.Auth;
using FoodyBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DinnerController(DatabaseContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Dinner>>> GetDinners(CancellationToken cancellationToken)
    {
        return Ok(await context.Dinners
            .AsNoTracking()
            .Include(dinner => dinner.Group)
            .OrderBy(dinner => dinner.Id)
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Dinner>> GetDinner(int id, CancellationToken cancellationToken)
    {
        var dinner = await context.Dinners
            .AsNoTracking()
            .Include(item => item.Group)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return dinner is null ? NotFound() : Ok(dinner);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> PutDinner(int id, Dinner dinner, CancellationToken cancellationToken)
    {
        if (id != dinner.Id)
        {
            return BadRequest();
        }

        var existingDinner = await context.Dinners.FirstOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);
        if (existingDinner is null)
        {
            return NotFound();
        }

        var targetGroupId = ResolveGroupId(dinner);
        if (targetGroupId <= 0)
        {
            return BadRequest("GroupId is required.");
        }

        if (!await context.Groups.AnyAsync(group => group.Id == targetGroupId, cancellationToken))
        {
            return BadRequest($"Group with id {targetGroupId} was not found.");
        }

        var canEditExistingGroup = await IsCurrentUserMemberOfGroupAsync(existingDinner.GroupId, cancellationToken);
        var canEditTargetGroup = await IsCurrentUserMemberOfGroupAsync(targetGroupId, cancellationToken);
        if (!canEditExistingGroup || !canEditTargetGroup)
        {
            return Forbid();
        }

        existingDinner.GroupId = targetGroupId;
        existingDinner.Description = dinner.Description;
        existingDinner.Date = dinner.Date;

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Dinner>> PostDinner(Dinner dinner, CancellationToken cancellationToken)
    {
        var groupId = ResolveGroupId(dinner);
        if (groupId <= 0)
        {
            return BadRequest("GroupId is required.");
        }

        if (!await context.Groups.AnyAsync(group => group.Id == groupId, cancellationToken))
        {
            return BadRequest($"Group with id {groupId} was not found.");
        }

        if (!await IsCurrentUserMemberOfGroupAsync(groupId, cancellationToken))
        {
            return Forbid();
        }

        var newDinner = new Dinner
        {
            GroupId = groupId,
            Description = dinner.Description,
            Date = dinner.Date
        };

        context.Dinners.Add(newDinner);
        await context.SaveChangesAsync(cancellationToken);

        var createdDinner = await context.Dinners
            .AsNoTracking()
            .Include(item => item.Group)
            .FirstAsync(item => item.Id == newDinner.Id, cancellationToken);

        return CreatedAtAction(nameof(GetDinner), new { id = newDinner.Id }, createdDinner);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDinner(int id, CancellationToken cancellationToken)
    {
        var dinner = await context.Dinners.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (dinner is null)
        {
            return NotFound();
        }

        if (!await IsCurrentUserMemberOfGroupAsync(dinner.GroupId, cancellationToken))
        {
            return Forbid();
        }

        context.Dinners.Remove(dinner);
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

    private static int ResolveGroupId(Dinner dinner)
    {
        if (dinner.GroupId > 0)
        {
            return dinner.GroupId;
        }

        return dinner.Group?.Id ?? 0;
    }
}
