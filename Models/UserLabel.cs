
namespace FoodyBackend.Models;

public class UserLabel
{
    public int Id { get; set; }
    public User User { get; set; }
    public Label Label { get; set; }
}