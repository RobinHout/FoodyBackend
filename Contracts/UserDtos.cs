namespace FoodyBackend.Contracts;

public sealed record UserResponse(int Id, string Username);

public sealed record CreateUserRequest(string Username, string Password);

public sealed record UpdateUserRequest(int Id, string Username, string? Password);

public sealed record GroupSummary(int Id, string Name, string Description);

public sealed record MeResponse(int Id, string Username, IReadOnlyCollection<GroupSummary> Groups);
