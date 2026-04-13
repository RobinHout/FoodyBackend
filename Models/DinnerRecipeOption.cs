namespace FoodyBackend.Models;

public class DinnerRecipeOption
{
    public int Id { get; set; }
    public int DinnerId { get; set; }
    public Dinner? Dinner { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int Rank { get; set; }
    public int Score { get; set; }
    public int TierOneScore { get; set; }
    public int TierTwoScore { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
