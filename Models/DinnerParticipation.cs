namespace FoodyBackend.Models;

public class DinnerParticipation
{
    public int Id { get; set; }
    public int DinnerId { get; set; }
    public Dinner? Dinner { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string Attending { get; set; } = DinnerAttendanceValues.Unknown;
    public string? Q1Choice { get; set; }
    public string? Q2Choice { get; set; }
    public string? Q3Choice { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
