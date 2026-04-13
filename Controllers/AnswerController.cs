using FoodyBackend.Auth;
using FoodyBackend.Contracts;
using FoodyBackend.Models;
using FoodyBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AnswerController(
    DatabaseContext context,
    IDinnerRecommendationService recommendationService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AnswerResponse>> PostAnswer(
        CreateAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request.DinnerId, request.UserId, request.Level, request.Question);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var dinner = await context.Dinners
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.DinnerId, cancellationToken);
        if (dinner is null)
        {
            return BadRequest($"Dinner with id {request.DinnerId} was not found.");
        }

        if (!await IsCurrentUserMemberOfGroupAsync(dinner.GroupId, cancellationToken))
        {
            return Forbid();
        }

        if (!await IsUserMemberOfGroupAsync(request.UserId, dinner.GroupId, cancellationToken))
        {
            return BadRequest($"User with id {request.UserId} is not a member of group {dinner.GroupId}.");
        }

        var answer = new Answers
        {
            DinnerId = request.DinnerId,
            UserId = request.UserId,
            Level = request.Level.Trim(),
            Question = request.Question.Trim()
        };

        context.Answers.Add(answer);
        await context.SaveChangesAsync(cancellationToken);
        await recommendationService.RefreshDinnerRecommendationsAsync(answer.DinnerId, cancellationToken);

        var created = await BuildQuery(context.Answers.Where(item => item.Id == answer.Id))
            .FirstAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutAnswer(
        int id,
        UpdateAnswerRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
        {
            return BadRequest();
        }

        var validationError = ValidateRequest(request.DinnerId, request.UserId, request.Level, request.Question);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var answer = await context.Answers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (answer is null)
        {
            return NotFound();
        }

        var targetDinner = await context.Dinners
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.DinnerId, cancellationToken);
        if (targetDinner is null)
        {
            return BadRequest($"Dinner with id {request.DinnerId} was not found.");
        }

        if (!await IsCurrentUserMemberOfGroupAsync(targetDinner.GroupId, cancellationToken))
        {
            return Forbid();
        }

        if (!await IsUserMemberOfGroupAsync(request.UserId, targetDinner.GroupId, cancellationToken))
        {
            return BadRequest($"User with id {request.UserId} is not a member of group {targetDinner.GroupId}.");
        }

        var dinnersToRefresh = new HashSet<int> { answer.DinnerId };

        answer.DinnerId = request.DinnerId;
        answer.UserId = request.UserId;
        answer.Level = request.Level.Trim();
        answer.Question = request.Question.Trim();

        dinnersToRefresh.Add(answer.DinnerId);

        await context.SaveChangesAsync(cancellationToken);

        foreach (var dinnerId in dinnersToRefresh)
        {
            await recommendationService.RefreshDinnerRecommendationsAsync(dinnerId, cancellationToken);
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAnswer(int id, CancellationToken cancellationToken)
    {
        var answer = await context.Answers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (answer is null)
        {
            return NotFound();
        }

        var dinnerGroupId = await context.Dinners
            .AsNoTracking()
            .Where(item => item.Id == answer.DinnerId)
            .Select(item => (int?)item.GroupId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!dinnerGroupId.HasValue)
        {
            return NotFound();
        }

        if (!await IsCurrentUserMemberOfGroupAsync(dinnerGroupId.Value, cancellationToken))
        {
            return Forbid();
        }

        var dinnerId = answer.DinnerId;
        context.Answers.Remove(answer);
        await context.SaveChangesAsync(cancellationToken);
        await recommendationService.RefreshDinnerRecommendationsAsync(dinnerId, cancellationToken);

        return NoContent();
    }

    private IQueryable<AnswerResponse> BuildQuery(IQueryable<Answers> source)
    {
        return source
            .AsNoTracking()
            .OrderBy(answer => answer.Id)
            .Select(answer => new AnswerResponse(
                answer.Id,
                answer.DinnerId,
                answer.UserId,
                answer.User!.Username,
                answer.Level,
                answer.Question));
    }

    private async Task<bool> IsCurrentUserMemberOfGroupAsync(int groupId, CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        return currentUserId.HasValue && await context.UserGroups.AnyAsync(
            link => link.UserId == currentUserId.Value && link.GroupId == groupId,
            cancellationToken);
    }

    private Task<bool> IsUserMemberOfGroupAsync(int userId, int groupId, CancellationToken cancellationToken)
    {
        return context.UserGroups.AnyAsync(
            link => link.UserId == userId && link.GroupId == groupId,
            cancellationToken);
    }

    private static string? ValidateRequest(int dinnerId, int userId, string level, string question)
    {
        if (dinnerId <= 0)
        {
            return "DinnerId is required.";
        }

        if (userId <= 0)
        {
            return "UserId is required.";
        }

        if (string.IsNullOrWhiteSpace(level))
        {
            return "Level is required.";
        }

        return string.IsNullOrWhiteSpace(question)
            ? "Question is required."
            : null;
    }
}
