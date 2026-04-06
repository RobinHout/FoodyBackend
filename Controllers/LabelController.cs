using FoodyBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LabelController(DatabaseContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Label>>> GetLabels(CancellationToken cancellationToken)
    {
        return Ok(await context.Labels
            .AsNoTracking()
            .OrderBy(label => label.Name)
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Label>> GetLabel(int id, CancellationToken cancellationToken)
    {
        var label = await context.Labels
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return label is null ? NotFound() : Ok(label);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Label>>> SearchLabels(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return BadRequest("Query is required.");
        }

        return Ok(await context.Labels
            .AsNoTracking()
            .Where(label => EF.Functions.ILike(label.Name, $"{normalizedQuery}%"))
            .OrderBy(label => label.Name)
            .ToListAsync(cancellationToken));
    }
}
