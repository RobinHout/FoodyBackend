using System.Text.Json.Serialization;

namespace FoodyBackend.Contracts;

public record class RecipeExportDto
{
    public string Recipe { get; init; } = string.Empty;
    public string Ingredients { get; init; } = string.Empty;
    public string Directions { get; init; } = string.Empty;
    public string Link { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Labels { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ner { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Data { get; init; }
}

public sealed record class RecommendedRecipeItemDto : RecipeExportDto
{
    public int RecipeId { get; init; }
    public int Score { get; init; }
    public int TierOneScore { get; init; }
    public int TierTwoScore { get; init; }
    public IReadOnlyCollection<string> TierOneMatches { get; init; } = [];
    public IReadOnlyCollection<string> TierTwoMatches { get; init; } = [];
}

public sealed record RecommendedLabelTierItemDto(
    int LabelId,
    string Name,
    string Description,
    int MatchCount);

public sealed record DinnerRecipeRecommendationsResponse(
    int DinnerId,
    int GroupId,
    IReadOnlyCollection<RecommendedLabelTierItemDto> TierOneLabels,
    IReadOnlyCollection<RecommendedLabelTierItemDto> TierTwoLabels,
    IReadOnlyCollection<RecommendedRecipeItemDto> Recipes);
