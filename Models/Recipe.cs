
namespace FoodyBackend.Models;

public class Recipe
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Ingredients { get; set; } = string.Empty;
    public string[] IngredientItems { get; set; } = [];
    public string Directions { get; set; } = string.Empty;
    public string[] DirectionSteps { get; set; } = [];
    public string[] Ner { get; set; } = [];
    public string Link { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}


