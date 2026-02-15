
namespace FoodyBackend.Models;

public class RecipeLabel
{
    public int Id { get; set; }
    public Recipe Recipe { get; set; }
    public Label Label { get; set; }
}