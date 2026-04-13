namespace FoodyBackend.Contracts;

public sealed record LabelSummaryDto(int Id, string Name, string Description);

public sealed record UserLabelSelectionsResponse(
    IReadOnlyCollection<LabelSummaryDto> Allergies,
    IReadOnlyCollection<LabelSummaryDto> Preferences);

public sealed record ReplaceUserLabelsRequest(
    IReadOnlyCollection<int>? Allergies,
    IReadOnlyCollection<int>? Preferences);
