
namespace FoodyBackend.Models;

public class Answers
{
    public int Id { get; set; }
    public int DinnerId { get; set; }
    public Dinner? Dinner { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
}
