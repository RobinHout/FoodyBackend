namespace FoodyBackend.Contracts;

public sealed record AuthTokenResponse(
    string TokenType,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

public sealed record AuthenticatedUserResponse(
    UserResponse User,
    AuthTokenResponse Session);

public sealed record RegisterRequest(string Username, string Password);

public sealed record LoginRequest(string Username, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);
