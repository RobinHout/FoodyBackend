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
        private readonly string _csvPath = "Data/dinners_100.csv";

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
    }
}