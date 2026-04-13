namespace FoodyBackend.Contracts;

public sealed record DinnerParticipationResponse(
    int DinnerId,
    int UserId,
    string Username,
    string Attending,
    string? Q1Choice,
    string? Q2Choice,
    string? Q3Choice);

public sealed record UpdateDinnerParticipationRequest(
    string Attending,
    string? Q1Choice,
    string? Q2Choice,
    string? Q3Choice,
    int? SourceDinnerId);
