
namespace FoodyBackend.Models;

public class Answers
{
    public int Id { get; set; }
    public Dinner Dinner { get; set; }
    public User User { get; set; }
    public string Level { get; set; }
    public string Question { get; set; }
}