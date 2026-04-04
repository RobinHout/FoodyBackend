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
    private const int RecommendedRecipeCount = 3;
    private const int TierOneWeight = 100;
    private const int TierTwoWeight = 10;

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
    [HttpGet("{id}/recommended-recipes")]
    public async Task<ActionResult<DinnerRecipeRecommendationsResponse>> GetRecommendedRecipes(
        int id,
        CancellationToken cancellationToken)
    {
        var dinner = await context.Dinners
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (dinner is null)
        {
            return NotFound();
        }

        if (!await IsCurrentUserMemberOfGroupAsync(dinner.GroupId, cancellationToken))
        {
            return Forbid();
        }

        var tierOneLabels = await GetTierOneLabelsAsync(dinner.GroupId, cancellationToken);
        var tierOneLabelIds = tierOneLabels
            .Select(label => label.LabelId)
            .ToHashSet();
        var tierTwoLabels = await GetTierTwoLabelsAsync(id, tierOneLabelIds, cancellationToken);
        var recipes = await GetRecommendedRecipesAsync(tierOneLabels, tierTwoLabels, cancellationToken);

        return Ok(new DinnerRecipeRecommendationsResponse(
            dinner.Id,
            dinner.GroupId,
            tierOneLabels,
            tierTwoLabels,
            recipes));
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

    private async Task<List<RecommendedLabelTierItem>> GetTierOneLabelsAsync(
        int groupId,
        CancellationToken cancellationToken)
    {
        var groupUserIds = await context.UserGroups
            .AsNoTracking()
            .Where(link => link.GroupId == groupId)
            .Select(link => link.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (groupUserIds.Count == 0)
        {
            return [];
        }

        var userLabels = await context.UserLabels
            .AsNoTracking()
            .Where(link => groupUserIds.Contains(link.UserId))
            .Select(link => new
            {
                link.UserId,
                link.LabelId,
                LabelName = link.Label!.Name,
                LabelDescription = link.Label.Description
            })
            .ToListAsync(cancellationToken);

        return userLabels
            .GroupBy(link => new { link.LabelId, link.LabelName, link.LabelDescription })
            .Select(group => new RecommendedLabelTierItem(
                group.Key.LabelId,
                group.Key.LabelName,
                group.Key.LabelDescription,
                group.Select(item => item.UserId).Distinct().Count()))
            .OrderByDescending(label => label.MatchCount)
            .ThenBy(label => label.Name)
            .ToList();
    }

    private async Task<List<RecommendedLabelTierItem>> GetTierTwoLabelsAsync(
        int dinnerId,
        ISet<int> tierOneLabelIds,
        CancellationToken cancellationToken)
    {
        var answers = await context.Answers
            .AsNoTracking()
            .Where(answer => answer.DinnerId == dinnerId)
            .Select(answer => new
            {
                answer.Id,
                answer.Question,
                answer.Level
            })
            .ToListAsync(cancellationToken);

        if (answers.Count == 0)
        {
            return [];
        }

        var labels = await context.Labels
            .AsNoTracking()
            .Select(label => new
            {
                label.Id,
                label.Name,
                label.Description,
                NormalizedName = NormalizeForMatching(label.Name)
            })
            .ToListAsync(cancellationToken);

        var matches = new List<(int AnswerId, int LabelId, string LabelName, string LabelDescription)>();

        foreach (var answer in answers)
        {
            var answerTokens = TokenizeForMatching($"{answer.Question} {answer.Level}");
            if (answerTokens.Count == 0)
            {
                continue;
            }

            foreach (var label in labels)
            {
                if (tierOneLabelIds.Contains(label.Id) ||
                    !DoesAnswerMatchLabel(answerTokens, label.NormalizedName))
                {
                    continue;
                }

                matches.Add((answer.Id, label.Id, label.Name, label.Description));
            }
        }

        return matches
            .GroupBy(match => new { match.LabelId, match.LabelName, match.LabelDescription })
            .Select(group => new RecommendedLabelTierItem(
                group.Key.LabelId,
                group.Key.LabelName,
                group.Key.LabelDescription,
                group.Select(item => item.AnswerId).Distinct().Count()))
            .OrderByDescending(label => label.MatchCount)
            .ThenBy(label => label.Name)
            .ToList();
    }

    private async Task<List<RecommendedRecipeItem>> GetRecommendedRecipesAsync(
        IReadOnlyCollection<RecommendedLabelTierItem> tierOneLabels,
        IReadOnlyCollection<RecommendedLabelTierItem> tierTwoLabels,
        CancellationToken cancellationToken)
    {
        var tierOneLookup = tierOneLabels.ToDictionary(label => label.LabelId, label => label);
        var tierTwoLookup = tierTwoLabels.ToDictionary(label => label.LabelId, label => label);
        var allLabelIds = tierOneLookup.Keys
            .Concat(tierTwoLookup.Keys)
            .Distinct()
            .ToList();

        if (allLabelIds.Count == 0)
        {
            return [];
        }

        var recipeLabels = await context.RecipeLabels
            .AsNoTracking()
            .Where(link => allLabelIds.Contains(link.LabelId))
            .Select(link => new
            {
                link.RecipeId,
                link.LabelId,
                LabelName = link.Label!.Name,
                RecipeTitle = link.Recipe!.Title,
                RecipeIngredients = link.Recipe.Ingredients,
                RecipeDirections = link.Recipe.Directions,
                RecipeLink = link.Recipe.Link,
                RecipeSource = link.Recipe.Source
            })
            .ToListAsync(cancellationToken);

        return recipeLabels
            .GroupBy(link => new
            {
                link.RecipeId,
                link.RecipeTitle,
                link.RecipeIngredients,
                link.RecipeDirections,
                link.RecipeLink,
                link.RecipeSource
            })
            .Select(group =>
            {
                var matchedLabels = group
                    .GroupBy(item => new { item.LabelId, item.LabelName })
                    .Select(item => item.Key)
                    .ToList();

                var tierOneMatches = matchedLabels
                    .Where(item => tierOneLookup.ContainsKey(item.LabelId))
                    .Select(item => item.LabelName)
                    .OrderBy(name => name)
                    .ToList();
                var tierTwoMatches = matchedLabels
                    .Where(item => tierTwoLookup.ContainsKey(item.LabelId))
                    .Select(item => item.LabelName)
                    .OrderBy(name => name)
                    .ToList();

                var tierOneScore = matchedLabels
                    .Where(item => tierOneLookup.ContainsKey(item.LabelId))
                    .Sum(item => tierOneLookup[item.LabelId].MatchCount * TierOneWeight);
                var tierTwoScore = matchedLabels
                    .Where(item => tierTwoLookup.ContainsKey(item.LabelId))
                    .Sum(item => tierTwoLookup[item.LabelId].MatchCount * TierTwoWeight);

                return new RecommendedRecipeItem(
                    group.Key.RecipeId,
                    group.Key.RecipeTitle,
                    group.Key.RecipeIngredients,
                    group.Key.RecipeDirections,
                    NormalizeLink(group.Key.RecipeLink),
                    group.Key.RecipeSource,
                    tierOneScore + tierTwoScore,
                    tierOneScore,
                    tierTwoScore,
                    tierOneMatches,
                    tierTwoMatches);
            })
            .Where(recipe => recipe.Score > 0)
            .OrderByDescending(recipe => recipe.TierOneScore)
            .ThenByDescending(recipe => recipe.TierTwoScore)
            .ThenByDescending(recipe => recipe.TierOneMatches.Count)
            .ThenByDescending(recipe => recipe.TierTwoMatches.Count)
            .ThenBy(recipe => recipe.Recipe)
            .Take(RecommendedRecipeCount)
            .ToList();
    }

    private static int ResolveGroupId(Dinner dinner)
    {
        if (dinner.GroupId > 0)
        {
            return dinner.GroupId;
        }

        return dinner.Group?.Id ?? 0;
    }

    private static HashSet<string> TokenizeForMatching(string? text)
    {
        return NormalizeForMatching(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string NormalizeForMatching(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var characters = text
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();

        return string.Join(' ', new string(characters)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool DoesAnswerMatchLabel(IReadOnlySet<string> answerTokens, string normalizedLabelName)
    {
        if (string.IsNullOrWhiteSpace(normalizedLabelName))
        {
            return false;
        }

        var labelTokens = normalizedLabelName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return labelTokens.Length > 0 && labelTokens.All(answerTokens.Contains);
    }

    private static string NormalizeLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return string.Empty;
        }

        return link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               link.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? link
            : $"https://{link}";
    }

    public sealed record DinnerRecipeRecommendationsResponse(
        int DinnerId,
        int GroupId,
        IReadOnlyCollection<RecommendedLabelTierItem> TierOneLabels,
        IReadOnlyCollection<RecommendedLabelTierItem> TierTwoLabels,
        IReadOnlyCollection<RecommendedRecipeItem> Recipes);

    public sealed record RecommendedLabelTierItem(
        int LabelId,
        string Name,
        string Description,
        int MatchCount);

    public sealed record RecommendedRecipeItem(
        int RecipeId,
        string Recipe,
        string Ingredients,
        string Directions,
        string Link,
        string Source,
        int Score,
        int TierOneScore,
        int TierTwoScore,
        IReadOnlyCollection<string> TierOneMatches,
        IReadOnlyCollection<string> TierTwoMatches);
}
