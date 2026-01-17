
namespace FoodyBackend.Models;

public class Dinner
{
    public int Id { get; set; }
    public Group Group { get; set; }
    public string Description { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
}