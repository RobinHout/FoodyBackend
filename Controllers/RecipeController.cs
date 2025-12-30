using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace FoodyBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecipeController : ControllerBase
    {
        // private string _csvPath = "Data/dinners_100.csv";
        private string _csvPath = "Data/foodnetwork_recipes.csv";

    

        [HttpGet("random")]
        public IActionResult GetRandomRecipe()
        {
            try
            {
                var lines = System.IO.File.ReadAllLines(_csvPath);
                if (lines.Length <= 1)
                    return NotFound("No recipes found");

                var random = new Random();
                var randomLine = lines[random.Next(1, lines.Length)];
                var parts = randomLine.Split(',');

                return Ok(new { recipe = parts[0], data = randomLine });
            }
            catch (FileNotFoundException)
            {
                return NotFound("CSV file not found");
            }
        }
        [HttpGet("by-ingredient")]
        public IActionResult GetRecipesByIngredient([FromQuery] string ingredient)
        {
            if (string.IsNullOrWhiteSpace(ingredient))
                return BadRequest("Ingredient is required");

            try
            {
                var lines = System.IO.File.ReadAllLines(_csvPath);

                if (lines.Length <= 1)
                    return NotFound("No recipes found");

                var matches = lines
                    .Skip(1) // skip header
                    .Select(line => line.Split(','))
                    .Where(parts =>
                        parts.Length > 1 &&
                        parts[1].Contains(ingredient, StringComparison.OrdinalIgnoreCase)
                    )
                    .Select(parts => new
                    {
                        recipe = parts[0],
                        ingredients = parts[1]
                    })
                    .ToList();

                if (!matches.Any())
                    return NotFound($"No recipes found with ingredient '{ingredient}'");

                return Ok(matches);
            }
            catch (FileNotFoundException)
            {
                return NotFound("CSV file not found");
            }
        }
    }
}