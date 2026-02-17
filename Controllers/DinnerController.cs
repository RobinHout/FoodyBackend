using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FoodyBackend.Models;

namespace FoodyBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DinnerController : ControllerBase
    {
        private readonly DatabaseContext _context;

        public DinnerController(DatabaseContext context)
        {
            _context = context;
        }

        // GET: api/Dinner
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Dinner>>> GetDinners()
        {
            return await _context.Dinners
                .Include(d => d.Group)
                .ToListAsync();
        }

        // GET: api/Dinner/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Dinner>> GetDinner(int id)
        {
            var dinner = await _context.Dinners
                .Include(d => d.Group)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (dinner == null)
            {
                return NotFound();
            }

            return dinner;
        }

        // PUT: api/Dinner/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDinner(int id, Dinner dinner)
        {
            if (id != dinner.Id)
            {
                return BadRequest();
            }

            _context.Entry(dinner).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DinnerExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Dinner
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Dinner>> PostDinner(Dinner dinner)
        {

            if (dinner.Group != null)
            {
                 // Check if group exists to prevent duplicate key or attachment issues if needed
                 var existingGroup = await _context.Groups.FindAsync(dinner.Group.Id);
                 if (existingGroup != null)
                 {
                     dinner.Group = existingGroup;
                 }
            }

            _context.Dinners.Add(dinner);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetDinner", new { id = dinner.Id }, dinner);
        }

        // DELETE: api/Dinner/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDinner(int id)
        {
            var dinner = await _context.Dinners.FindAsync(id);
            if (dinner == null)
            {
                return NotFound();
            }

            _context.Dinners.Remove(dinner);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool DinnerExists(int id)
        {
            return _context.Dinners.Any(e => e.Id == id);
        }
    }
}
