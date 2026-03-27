namespace FoodyBackend.Models;

public class AuthSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string AccessTokenHash { get; set; } = string.Empty;
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? LastRefreshedAtUtc { get; set; }
}
