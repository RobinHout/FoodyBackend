
namespace FoodyBackend.Models;

public class DietaryRequirement
{
    public int Id { get; set; }
    public User User { get; set; }
    public string Ingredient { get; set; }
}