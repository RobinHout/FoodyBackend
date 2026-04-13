using FoodyBackend.Auth;
using FoodyBackend.Contracts;
using FoodyBackend.Models;
using FoodyBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UserLabelController(
    DatabaseContext context,
    IDinnerRecommendationService recommendationService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserLabelSelectionsResponse>> GetCurrentUserLabels(CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        return Ok(await BuildResponseAsync(currentUserId.Value, cancellationToken));
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserLabelSelectionsResponse>> PutCurrentUserLabels(
        ReplaceUserLabelsRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        var allergyIds = request.Allergies?
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? [];
        var preferenceIds = request.Preferences?
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? [];

        var overlap = allergyIds.Intersect(preferenceIds).ToList();
        if (overlap.Count > 0)
        {
            return BadRequest("A label cannot be both an allergy and a preference.");
        }

        var allIds = allergyIds.Concat(preferenceIds).Distinct().ToList();
        if (allIds.Count > 0)
        {
            var existingIds = await context.Labels
                .AsNoTracking()
                .Where(label => allIds.Contains(label.Id))
                .Select(label => label.Id)
                .ToListAsync(cancellationToken);

            var missingIds = allIds.Except(existingIds).OrderBy(id => id).ToList();
            if (missingIds.Count > 0)
            {
                return BadRequest($"Unknown label ids: {string.Join(", ", missingIds)}.");
            }
        }

        await context.UserLabels
            .Where(link => link.UserId == currentUserId.Value)
            .ExecuteDeleteAsync(cancellationToken);

        context.UserLabels.AddRange(allergyIds.Select(labelId => new UserLabel
        {
            UserId = currentUserId.Value,
            LabelId = labelId,
            Category = UserLabelCategories.Allergy
        }));
        context.UserLabels.AddRange(preferenceIds.Select(labelId => new UserLabel
        {
            UserId = currentUserId.Value,
            LabelId = labelId,
            Category = UserLabelCategories.Preference
        }));

        await context.SaveChangesAsync(cancellationToken);

        var groupIds = await context.UserGroups
            .AsNoTracking()
            .Where(link => link.UserId == currentUserId.Value)
            .Select(link => link.GroupId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var groupId in groupIds)
        {
            await recommendationService.RefreshGroupDinnerRecommendationsAsync(groupId, cancellationToken);
        }

        return Ok(await BuildResponseAsync(currentUserId.Value, cancellationToken));
    }

    private async Task<UserLabelSelectionsResponse> BuildResponseAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var labels = await context.UserLabels
            .AsNoTracking()
            .Where(link => link.UserId == userId)
            .Select(link => new
            {
                link.Category,
                Label = new LabelSummaryDto(
                    link.LabelId,
                    link.Label!.Name,
                    link.Label.Description)
            })
            .ToListAsync(cancellationToken);

        var allergies = labels
            .Where(item => item.Category == UserLabelCategories.Allergy)
            .Select(item => item.Label)
            .DistinctBy(label => label.Id)
            .OrderBy(label => label.Name)
            .ToList();
        var preferences = labels
            .Where(item => item.Category == UserLabelCategories.Preference)
            .Select(item => item.Label)
            .DistinctBy(label => label.Id)
            .OrderBy(label => label.Name)
            .ToList();

        return new UserLabelSelectionsResponse(allergies, preferences);
    }
}
