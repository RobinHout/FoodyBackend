namespace FoodyBackend.Models;

public class UserLabel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int LabelId { get; set; }
    public Label? Label { get; set; }
    public string Category { get; set; } = UserLabelCategories.Preference;
}
