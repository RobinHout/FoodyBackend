using FoodyBackend.Contracts;
using FoodyBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Services;

public sealed class DinnerRecommendationService(DatabaseContext context) : IDinnerRecommendationService
{
    private const int RecommendedRecipeCount = 3;
    private const int TierOneWeight = 100;
    private const int TierTwoWeight = 10;

    public async Task<DinnerRecipeRecommendationsResponse?> GetDinnerRecommendationsAsync(
        int dinnerId,
        CancellationToken cancellationToken)
    {
        var dinner = await context.Dinners
            .AsNoTracking()
            .Select(item => new { item.Id, item.GroupId })
            .FirstOrDefaultAsync(item => item.Id == dinnerId, cancellationToken);
        if (dinner is null)
        {
            return null;
        }

        var tierOneLabels = await GetTierOneLabelsAsync(dinner.GroupId, cancellationToken);
        var tierOneLookup = tierOneLabels.ToDictionary(label => label.LabelId);
        var tierTwoLabels = await GetTierTwoLabelsAsync(dinner.Id, tierOneLookup.Keys.ToHashSet(), cancellationToken);
        var tierTwoLookup = tierTwoLabels.ToDictionary(label => label.LabelId);

        var optionRows = await context.DinnerRecipeOptions
            .AsNoTracking()
            .Where(option => option.DinnerId == dinner.Id)
            .OrderBy(option => option.Rank)
            .Select(option => new DinnerRecipeOptionProjection(
                option.RecipeId,
                option.Recipe!.Title,
                option.Recipe.Ingredients,
                option.Recipe.IngredientItems,
                option.Recipe.Directions,
                option.Recipe.DirectionSteps,
                option.Recipe.Link,
                option.Recipe.Source,
                option.Score,
                option.TierOneScore,
                option.TierTwoScore))
            .ToListAsync(cancellationToken);

        var recipeLabelLookup = await GetRecipeLabelReferencesByRecipeIdAsync(
            optionRows.Select(option => option.RecipeId).ToList(),
            cancellationToken);

        var recipes = optionRows
            .Select(option =>
            {
                var labelRefs = recipeLabelLookup.GetValueOrDefault(option.RecipeId) ?? [];
                var labels = labelRefs
                    .Select(label => label.LabelName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList();
                var tierOneMatches = labelRefs
                    .Where(label => tierOneLookup.ContainsKey(label.LabelId))
                    .Select(label => label.LabelName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList();
                var tierTwoMatches = labelRefs
                    .Where(label => tierTwoLookup.ContainsKey(label.LabelId))
                    .Select(label => label.LabelName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList();

                return new RecommendedRecipeItemDto
                {
                    RecipeId = option.RecipeId,
                    Recipe = option.Title,
                    Ingredients = GetIngredients(option.Ingredients, option.IngredientItems),
                    Directions = GetDirections(option.Directions, option.DirectionSteps),
                    Link = NormalizeLink(option.Link),
                    Source = option.Source,
                    Labels = labels,
                    Score = option.Score,
                    TierOneScore = option.TierOneScore,
                    TierTwoScore = option.TierTwoScore,
                    TierOneMatches = tierOneMatches,
                    TierTwoMatches = tierTwoMatches
                };
            })
            .ToList();

        return new DinnerRecipeRecommendationsResponse(
            dinner.Id,
            dinner.GroupId,
            tierOneLabels,
            tierTwoLabels,
            recipes);
    }

    public async Task RefreshDinnerRecommendationsAsync(int dinnerId, CancellationToken cancellationToken)
    {
        var dinner = await context.Dinners
            .AsNoTracking()
            .Select(item => new { item.Id, item.GroupId })
            .FirstOrDefaultAsync(item => item.Id == dinnerId, cancellationToken);
        if (dinner is null)
        {
            return;
        }

        var tierOneLabels = await GetTierOneLabelsAsync(dinner.GroupId, cancellationToken);
        var tierTwoLabels = await GetTierTwoLabelsAsync(
            dinner.Id,
            tierOneLabels.Select(label => label.LabelId).ToHashSet(),
            cancellationToken);
        var rankedRecipes = await GetRankedRecipesAsync(tierOneLabels, tierTwoLabels, cancellationToken);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context.DinnerRecipeOptions
            .Where(option => option.DinnerId == dinner.Id)
            .ExecuteDeleteAsync(cancellationToken);

        if (rankedRecipes.Count > 0)
        {
            var updatedAtUtc = DateTime.UtcNow;
            context.DinnerRecipeOptions.AddRange(rankedRecipes
                .Select((recipe, index) => new DinnerRecipeOption
                {
                    DinnerId = dinner.Id,
                    RecipeId = recipe.RecipeId,
                    Rank = index + 1,
                    Score = recipe.Score,
                    TierOneScore = recipe.TierOneScore,
                    TierTwoScore = recipe.TierTwoScore,
                    UpdatedAtUtc = updatedAtUtc
                }));

            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RefreshGroupDinnerRecommendationsAsync(int groupId, CancellationToken cancellationToken)
    {
        var dinnerIds = await context.Dinners
            .AsNoTracking()
            .Where(dinner => dinner.GroupId == groupId)
            .OrderBy(dinner => dinner.Id)
            .Select(dinner => dinner.Id)
            .ToListAsync(cancellationToken);

        foreach (var dinnerId in dinnerIds)
        {
            await RefreshDinnerRecommendationsAsync(dinnerId, cancellationToken);
        }
    }

    public async Task RebuildAllDinnerRecommendationsAsync(CancellationToken cancellationToken)
    {
        var dinnerIds = await context.Dinners
            .AsNoTracking()
            .OrderBy(dinner => dinner.Id)
            .Select(dinner => dinner.Id)
            .ToListAsync(cancellationToken);

        foreach (var dinnerId in dinnerIds)
        {
            await RefreshDinnerRecommendationsAsync(dinnerId, cancellationToken);
        }
    }

    private async Task<List<RecommendedLabelTierItemDto>> GetTierOneLabelsAsync(
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
            .Select(group => new RecommendedLabelTierItemDto(
                group.Key.LabelId,
                group.Key.LabelName,
                group.Key.LabelDescription,
                group.Select(item => item.UserId).Distinct().Count()))
            .OrderByDescending(label => label.MatchCount)
            .ThenBy(label => label.Name)
            .ToList();
    }

    private async Task<List<RecommendedLabelTierItemDto>> GetTierTwoLabelsAsync(
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
            .Select(group => new RecommendedLabelTierItemDto(
                group.Key.LabelId,
                group.Key.LabelName,
                group.Key.LabelDescription,
                group.Select(item => item.AnswerId).Distinct().Count()))
            .OrderByDescending(label => label.MatchCount)
            .ThenBy(label => label.Name)
            .ToList();
    }

    private async Task<List<RankedRecipeCandidate>> GetRankedRecipesAsync(
        IReadOnlyCollection<RecommendedLabelTierItemDto> tierOneLabels,
        IReadOnlyCollection<RecommendedLabelTierItemDto> tierTwoLabels,
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
                RecipeTitle = link.Recipe!.Title
            })
            .ToListAsync(cancellationToken);

        return recipeLabels
            .GroupBy(link => new { link.RecipeId, link.RecipeTitle })
            .Select(group =>
            {
                var matchedLabelIds = group
                    .Select(item => item.LabelId)
                    .Distinct()
                    .ToList();

                var tierOneMatchCount = matchedLabelIds.Count(labelId => tierOneLookup.ContainsKey(labelId));
                var tierTwoMatchCount = matchedLabelIds.Count(labelId => tierTwoLookup.ContainsKey(labelId));
                var tierOneScore = matchedLabelIds
                    .Where(labelId => tierOneLookup.ContainsKey(labelId))
                    .Sum(labelId => tierOneLookup[labelId].MatchCount * TierOneWeight);
                var tierTwoScore = matchedLabelIds
                    .Where(labelId => tierTwoLookup.ContainsKey(labelId))
                    .Sum(labelId => tierTwoLookup[labelId].MatchCount * TierTwoWeight);

                return new RankedRecipeCandidate(
                    group.Key.RecipeId,
                    group.Key.RecipeTitle,
                    tierOneScore + tierTwoScore,
                    tierOneScore,
                    tierTwoScore,
                    tierOneMatchCount,
                    tierTwoMatchCount);
            })
            .Where(recipe => recipe.Score > 0)
            .OrderByDescending(recipe => recipe.TierOneScore)
            .ThenByDescending(recipe => recipe.TierTwoScore)
            .ThenByDescending(recipe => recipe.TierOneMatchCount)
            .ThenByDescending(recipe => recipe.TierTwoMatchCount)
            .ThenBy(recipe => recipe.RecipeTitle)
            .Take(RecommendedRecipeCount)
            .ToList();
    }

    private async Task<Dictionary<int, List<RecipeLabelReference>>> GetRecipeLabelReferencesByRecipeIdAsync(
        IReadOnlyCollection<int> recipeIds,
        CancellationToken cancellationToken)
    {
        if (recipeIds.Count == 0)
        {
            return [];
        }

        var labelRefs = await context.RecipeLabels
            .AsNoTracking()
            .Where(link => recipeIds.Contains(link.RecipeId))
            .Select(link => new RecipeLabelReference(
                link.RecipeId,
                link.LabelId,
                link.Label!.Name))
            .ToListAsync(cancellationToken);

        return labelRefs
            .GroupBy(link => link.RecipeId)
            .ToDictionary(group => group.Key, group => group.ToList());
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

    private static string GetIngredients(string ingredients, IReadOnlyList<string> ingredientItems)
    {
        return string.IsNullOrWhiteSpace(ingredients)
            ? string.Join(", ", ingredientItems)
            : ingredients;
    }

    private static string GetDirections(string directions, IReadOnlyList<string> directionSteps)
    {
        return string.IsNullOrWhiteSpace(directions)
            ? string.Join(Environment.NewLine, directionSteps)
            : directions;
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

    private sealed record DinnerRecipeOptionProjection(
        int RecipeId,
        string Title,
        string Ingredients,
        string[] IngredientItems,
        string Directions,
        string[] DirectionSteps,
        string Link,
        string Source,
        int Score,
        int TierOneScore,
        int TierTwoScore);

    private sealed record RecipeLabelReference(int RecipeId, int LabelId, string LabelName);

    private sealed record RankedRecipeCandidate(
        int RecipeId,
        string RecipeTitle,
        int Score,
        int TierOneScore,
        int TierTwoScore,
        int TierOneMatchCount,
        int TierTwoMatchCount);
}
