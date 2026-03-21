
namespace FoodyBackend.Models;

public class RecipeLabel
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int LabelId { get; set; }
    public Label? Label { get; set; }
}
