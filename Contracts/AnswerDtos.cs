namespace FoodyBackend.Contracts;

public sealed record CreateAnswerRequest(
    int DinnerId,
    int UserId,
    string Level,
    string Question);

public sealed record UpdateAnswerRequest(
    int Id,
    int DinnerId,
    int UserId,
    string Level,
    string Question);

public sealed record AnswerResponse(
    int Id,
    int DinnerId,
    int UserId,
    string Username,
    string Level,
    string Question);
