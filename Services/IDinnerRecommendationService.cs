using FoodyBackend.Contracts;

namespace FoodyBackend.Services;

public interface IDinnerRecommendationService
{
    Task<DinnerRecipeRecommendationsResponse?> GetDinnerRecommendationsAsync(
        int dinnerId,
        CancellationToken cancellationToken);

    Task RefreshDinnerRecommendationsAsync(
        int dinnerId,
        CancellationToken cancellationToken);

    Task RefreshGroupDinnerRecommendationsAsync(
        int groupId,
        CancellationToken cancellationToken);

    Task RebuildAllDinnerRecommendationsAsync(CancellationToken cancellationToken);
}
