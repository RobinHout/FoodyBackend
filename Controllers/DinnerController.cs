using FoodyBackend.Auth;
using FoodyBackend.Contracts;
using FoodyBackend.Models;
using FoodyBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DinnerController(
    DatabaseContext context,
    IDinnerRecommendationService recommendationService) : ControllerBase
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
    [HttpGet("{id}/participation")]
    public async Task<ActionResult<IEnumerable<DinnerParticipationResponse>>> GetDinnerParticipation(
        int id,
        CancellationToken cancellationToken)
    {
        var dinner = await GetDinnerAccessProjectionAsync(id, cancellationToken);
        if (dinner is null)
        {
            return NotFound();
        }

        if (!await IsCurrentUserMemberOfGroupAsync(dinner.GroupId, cancellationToken))
        {
            return Forbid();
        }

        return Ok(await BuildDinnerParticipationResponsesAsync(dinner.Id, dinner.GroupId, cancellationToken));
    }

    [Authorize]
    [HttpGet("{id}/participation/me")]
    public async Task<ActionResult<DinnerParticipationResponse>> GetCurrentUserParticipation(
        int id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        var dinner = await GetDinnerAccessProjectionAsync(id, cancellationToken);
        if (dinner is null)
        {
            return NotFound();
        }

        if (!await IsCurrentUserMemberOfGroupAsync(dinner.GroupId, cancellationToken))
        {
            return Forbid();
        }

        var username = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == currentUserId.Value)
            .Select(user => user.Username)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(username))
        {
            return NotFound();
        }

        var participation = await context.DinnerParticipations
            .AsNoTracking()
            .Where(item => item.DinnerId == dinner.Id && item.UserId == currentUserId.Value)
            .Select(item => new DinnerParticipationResponse(
                item.DinnerId,
                item.UserId,
                username,
                item.Attending,
                item.Q1Choice,
                item.Q2Choice,
                item.Q3Choice))
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(participation ?? CreateDefaultParticipationResponse(dinner.Id, currentUserId.Value, username));
    }

    [Authorize]
    [HttpPut("{id}/participation/me")]
    public async Task<ActionResult<DinnerParticipationResponse>> PutCurrentUserParticipation(
        int id,
        UpdateDinnerParticipationRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        var validationError = ValidateParticipationRequest(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var dinner = await GetDinnerAccessProjectionAsync(id, cancellationToken);
        if (dinner is null)
        {
            return NotFound();
        }

        if (!await IsCurrentUserMemberOfGroupAsync(dinner.GroupId, cancellationToken))
        {
            return Forbid();
        }

        var username = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == currentUserId.Value)
            .Select(user => user.Username)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(username))
        {
            return NotFound();
        }

        var normalizedAttending = DinnerAttendanceValues.Normalize(request.Attending);
        var sourceDinnerIdToRefresh = default(int?);
        DinnerParticipation? sourceParticipation = null;
        string? sourceQ1Choice = null;
        string? sourceQ2Choice = null;
        string? sourceQ3Choice = null;

        if (request.SourceDinnerId.HasValue && request.SourceDinnerId.Value != id)
        {
            var sourceDinner = await GetDinnerAccessProjectionAsync(request.SourceDinnerId.Value, cancellationToken);
            if (sourceDinner is null)
            {
                return BadRequest($"Source dinner with id {request.SourceDinnerId.Value} was not found.");
            }

            if (!await IsCurrentUserMemberOfGroupAsync(sourceDinner.GroupId, cancellationToken))
            {
                return Forbid();
            }

            sourceDinnerIdToRefresh = sourceDinner.Id;
            sourceParticipation = await context.DinnerParticipations.FirstOrDefaultAsync(
                item => item.DinnerId == sourceDinner.Id && item.UserId == currentUserId.Value,
                cancellationToken);
            sourceQ1Choice = sourceParticipation?.Q1Choice;
            sourceQ2Choice = sourceParticipation?.Q2Choice;
            sourceQ3Choice = sourceParticipation?.Q3Choice;
        }

        var participation = await context.DinnerParticipations.FirstOrDefaultAsync(
            item => item.DinnerId == dinner.Id && item.UserId == currentUserId.Value,
            cancellationToken);
        if (participation is null)
        {
            participation = new DinnerParticipation
            {
                DinnerId = dinner.Id,
                UserId = currentUserId.Value
            };

            context.DinnerParticipations.Add(participation);
        }

        participation.Attending = normalizedAttending;
        participation.UpdatedAtUtc = DateTime.UtcNow;
        if (normalizedAttending == DinnerAttendanceValues.Yes)
        {
            participation.Q1Choice = ResolveChoice(request.Q1Choice, sourceQ1Choice, participation.Q1Choice);
            participation.Q2Choice = ResolveChoice(request.Q2Choice, sourceQ2Choice, participation.Q2Choice);
            participation.Q3Choice = ResolveChoice(request.Q3Choice, sourceQ3Choice, participation.Q3Choice);
        }
        else
        {
            participation.Q1Choice = null;
            participation.Q2Choice = null;
            participation.Q3Choice = null;
        }

        if (sourceParticipation is not null)
        {
            sourceParticipation.Attending = DinnerAttendanceValues.No;
            sourceParticipation.Q1Choice = null;
            sourceParticipation.Q2Choice = null;
            sourceParticipation.Q3Choice = null;
            sourceParticipation.UpdatedAtUtc = DateTime.UtcNow;
        }

        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await recommendationService.RefreshDinnerRecommendationsAsync(dinner.Id, cancellationToken);
        if (sourceDinnerIdToRefresh.HasValue)
        {
            await recommendationService.RefreshDinnerRecommendationsAsync(sourceDinnerIdToRefresh.Value, cancellationToken);
        }

        return Ok(new DinnerParticipationResponse(
            dinner.Id,
            currentUserId.Value,
            username,
            participation.Attending,
            participation.Q1Choice,
            participation.Q2Choice,
            participation.Q3Choice));
    }

    [Authorize]
    [HttpGet("{id}/recommended-recipes")]
    public async Task<ActionResult<DinnerRecipeRecommendationsResponse>> GetRecommendedRecipes(
        int id,
        CancellationToken cancellationToken)
    {
        var dinner = await context.Dinners
            .AsNoTracking()
            .Select(item => new { item.Id, item.GroupId })
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (dinner is null)
        {
            return NotFound();
        }

        if (!await IsCurrentUserMemberOfGroupAsync(dinner.GroupId, cancellationToken))
        {
            return Forbid();
        }

        var response = await recommendationService.GetDinnerRecommendationsAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
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
        await recommendationService.RefreshDinnerRecommendationsAsync(existingDinner.Id, cancellationToken);

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
        await recommendationService.RefreshDinnerRecommendationsAsync(newDinner.Id, cancellationToken);

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

    private async Task<List<DinnerParticipationResponse>> BuildDinnerParticipationResponsesAsync(
        int dinnerId,
        int groupId,
        CancellationToken cancellationToken)
    {
        var members = await context.UserGroups
            .AsNoTracking()
            .Where(link => link.GroupId == groupId)
            .Select(link => new GroupMemberProjection(link.UserId, link.User!.Username))
            .OrderBy(member => member.Username)
            .ToListAsync(cancellationToken);

        var participations = await context.DinnerParticipations
            .AsNoTracking()
            .Where(item => item.DinnerId == dinnerId)
            .Select(item => new DinnerParticipationProjection(
                item.UserId,
                item.Attending,
                item.Q1Choice,
                item.Q2Choice,
                item.Q3Choice))
            .ToListAsync(cancellationToken);

        var participationByUserId = participations.ToDictionary(item => item.UserId);

        return members
            .Select(member =>
            {
                if (participationByUserId.TryGetValue(member.UserId, out var participation))
                {
                    return new DinnerParticipationResponse(
                        dinnerId,
                        member.UserId,
                        member.Username,
                        participation.Attending,
                        participation.Q1Choice,
                        participation.Q2Choice,
                        participation.Q3Choice);
                }

                return CreateDefaultParticipationResponse(dinnerId, member.UserId, member.Username);
            })
            .ToList();
    }

    private static DinnerParticipationResponse CreateDefaultParticipationResponse(
        int dinnerId,
        int userId,
        string username)
    {
        return new DinnerParticipationResponse(
            dinnerId,
            userId,
            username,
            DinnerAttendanceValues.Unknown,
            null,
            null,
            null);
    }

    private async Task<DinnerAccessProjection?> GetDinnerAccessProjectionAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await context.Dinners
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new DinnerAccessProjection(item.Id, item.GroupId))
            .FirstOrDefaultAsync(cancellationToken);
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

    private static string? ValidateParticipationRequest(UpdateDinnerParticipationRequest request)
    {
        if (!DinnerAttendanceValues.IsValid(request.Attending))
        {
            return "Attending must be 'yes', 'no', or 'unknown'.";
        }

        return request.SourceDinnerId is <= 0
            ? "SourceDinnerId must be greater than 0 when it is provided."
            : null;
    }

    private static string? ResolveChoice(string? requestedValue, string? sourceValue, string? existingValue)
    {
        return NormalizeChoice(requestedValue) ?? NormalizeChoice(sourceValue) ?? NormalizeChoice(existingValue);
    }

    private static string? NormalizeChoice(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record DinnerAccessProjection(int Id, int GroupId);

    private sealed record GroupMemberProjection(int UserId, string Username);

    private sealed record DinnerParticipationProjection(
        int UserId,
        string Attending,
        string? Q1Choice,
        string? Q2Choice,
        string? Q3Choice);
}
