using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO; 
using System.Text;


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
       [HttpGet("by-number/{number:int}")]
public IActionResult GetRecipesByNumber(int number)
{
    if (number <= 0)
        return BadRequest("Number must be >= 1");

    try
    {
        var lines = System.IO.File.ReadAllLines(_csvPath);

        if (lines.Length <= 1)
            return NotFound("No recipes found");

        // number=1 should return first recipe line (lines[1])
        var lineIndex = number; // because lines[0] is header

        if (lineIndex >= lines.Length)
            return NotFound($"No recipe found with number '{number}'");

        var line = lines[lineIndex];

        // Parse CSV line safely (handles commas inside quotes)
        var fields = ParseCsvLine(line);

        if (fields.Length < 2)
            return BadRequest("CSV row malformed");

        return Ok(new
        {
            recipe = fields[0],
            ingredients = fields[1],
            directions = fields.Length > 2 ? fields[2] : null,
            link = fields.Length > 3 ? fields[3] : null,
            source = fields.Length > 4 ? fields[4] : null,
            ner = fields.Length > 5 ? fields[5] : null,
        });
    }
    catch (FileNotFoundException)
    {
        return NotFound("CSV file not found");
    }
}

private static string[] ParseCsvLine(string line)
{
    using var reader = new StringReader(line);
    using var parser = new TextFieldParser(reader)
    {
        TextFieldType = FieldType.Delimited,
        HasFieldsEnclosedInQuotes = true
    };
    parser.SetDelimiters(",");
    return parser.ReadFields() ?? Array.Empty<string>();
}
        [HttpGet("one-by-ingredient")]
        public IActionResult GetOneRecipeByIngredient([FromQuery] string ingredient)
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
                    .FirstOrDefault();

                if (matches == null)
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