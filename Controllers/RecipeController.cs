using FoodyBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace FoodyBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecipeController(
    DatabaseContext context,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ControllerBase
{
    private readonly string _csvPath = ResolveCsvPath(
        configuration["Database:RecipesCsvPath"] ?? "Data/foodnetwork_recipes.csv",
        environment.ContentRootPath);

    [HttpGet("random")]
    public async Task<IActionResult> GetRandomRecipe(CancellationToken cancellationToken)
    {
        var recipe = await GetRandomDatabaseRecipeAsync(cancellationToken);
        if (recipe != null)
        {
            return Ok(ToRecipeDetails(recipe));
        }

        var csvRecipe = GetRandomCsvRecipe();
        return csvRecipe is null ? NotFound("No recipes found") : Ok(csvRecipe);
    }

    [HttpGet("by-ingredient")]
    public async Task<IActionResult> GetRecipesByIngredient([FromQuery] string ingredient, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ingredient))
        {
            return BadRequest("Ingredient is required");
        }

        var matches = await context.Recipes
            .AsNoTracking()
            .Where(recipe => EF.Functions.Like(recipe.Ingredients, $"%{ingredient}%"))
            .OrderBy(recipe => recipe.Id)
            .Take(50)
            .Select(recipe => new
            {
                recipe = recipe.Title,
                ingredients = recipe.Ingredients,
                directions = recipe.Directions,
                link = recipe.Link,
                source = recipe.Source
            })
            .ToListAsync(cancellationToken);

        if (matches.Count > 0)
        {
            return Ok(matches);
        }

        var csvMatches = GetCsvRecipesByIngredient(ingredient);
        return csvMatches.Count == 0
            ? NotFound($"No recipes found with ingredient '{ingredient}'")
            : Ok(csvMatches);
    }

    [HttpGet("by-number/{number:int}")]
    public async Task<IActionResult> GetRecipesByNumber(int number, CancellationToken cancellationToken)
    {
        if (number <= 0)
        {
            return BadRequest("Number must be >= 1");
        }

        var databaseRecipe = await context.Recipes
            .AsNoTracking()
            .OrderBy(recipe => recipe.Id)
            .Skip(number - 1)
            .FirstOrDefaultAsync(cancellationToken);

        if (databaseRecipe != null)
        {
            return Ok(ToRecipeDetails(databaseRecipe));
        }

        var csvRecipe = GetCsvRecipeByNumber(number);
        return csvRecipe is null
            ? NotFound($"No recipe found with number '{number}'")
            : Ok(csvRecipe);
    }

    [HttpGet("one-by-ingredient")]
    public async Task<IActionResult> GetOneRecipeByIngredient([FromQuery] string ingredient, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ingredient))
        {
            return BadRequest("Ingredient is required");
        }

        var recipe = await context.Recipes
            .AsNoTracking()
            .Where(recipe => EF.Functions.Like(recipe.Ingredients, $"%{ingredient}%"))
            .OrderBy(recipe => recipe.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (recipe != null)
        {
            return Ok(ToRecipeDetails(recipe));
        }

        var csvRecipe = GetCsvRecipesByIngredient(ingredient).FirstOrDefault();
        return csvRecipe is null
            ? NotFound($"No recipes found with ingredient '{ingredient}'")
            : Ok(csvRecipe);
    }

    [HttpPost("import-csv")]
    public async Task<IActionResult> ImportRecipesFromCsv([FromQuery] int? limit, CancellationToken cancellationToken)
    {
        if (limit is <= 0)
        {
            return BadRequest("Limit must be greater than 0 when it is provided.");
        }

        if (!System.IO.File.Exists(_csvPath))
        {
            return NotFound($"CSV file not found at '{_csvPath}'.");
        }

        if (await context.Recipes.AnyAsync(cancellationToken))
        {
            return Conflict("The Recipes table already contains data. Import is blocked to prevent duplicates.");
        }

        const int batchSize = 500;
        var batch = new List<Recipe>(batchSize);
        var imported = 0;

        using var streamReader = new StreamReader(_csvPath);
        using var parser = CreateParser(streamReader);

        if (!parser.EndOfData)
        {
            parser.ReadFields();
        }

        while (!parser.EndOfData && (!limit.HasValue || imported < limit.Value))
        {
            var fields = parser.ReadFields();
            if (fields == null || fields.Length < 5)
            {
                continue;
            }

            batch.Add(new Recipe
            {
                Title = fields[0],
                Ingredients = fields[1],
                Directions = fields.Length > 2 ? fields[2] : string.Empty,
                Link = fields.Length > 3 ? NormalizeLink(fields[3]) : string.Empty,
                Source = fields.Length > 4 ? fields[4] : string.Empty
            });
            imported++;

            if (batch.Count < batchSize)
            {
                continue;
            }

            context.Recipes.AddRange(batch);
            await context.SaveChangesAsync(cancellationToken);
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            context.Recipes.AddRange(batch);
            await context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            imported,
            source = Path.GetFileName(_csvPath)
        });
    }

    private async Task<Recipe?> GetRandomDatabaseRecipeAsync(CancellationToken cancellationToken)
    {
        var totalRecipes = await context.Recipes.CountAsync(cancellationToken);
        if (totalRecipes == 0)
        {
            return null;
        }

        var offset = Random.Shared.Next(totalRecipes);
        return await context.Recipes
            .AsNoTracking()
            .OrderBy(recipe => recipe.Id)
            .Skip(offset)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private object? GetRandomCsvRecipe()
    {
        if (!System.IO.File.Exists(_csvPath))
        {
            return null;
        }

        string[]? selectedFields = null;
        var seen = 0;

        using var streamReader = new StreamReader(_csvPath);
        using var parser = CreateParser(streamReader);

        if (!parser.EndOfData)
        {
            parser.ReadFields();
        }

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields == null || fields.Length < 2)
            {
                continue;
            }

            seen++;
            if (Random.Shared.Next(seen) == 0)
            {
                selectedFields = fields;
            }
        }

        return selectedFields == null ? null : ToRecipeDetails(selectedFields);
    }

    private List<object> GetCsvRecipesByIngredient(string ingredient)
    {
        var matches = new List<object>();
        if (!System.IO.File.Exists(_csvPath))
        {
            return matches;
        }

        using var streamReader = new StreamReader(_csvPath);
        using var parser = CreateParser(streamReader);

        if (!parser.EndOfData)
        {
            parser.ReadFields();
        }

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields == null || fields.Length < 2)
            {
                continue;
            }

            if (!fields[1].Contains(ingredient, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches.Add(ToRecipeDetails(fields));
            if (matches.Count == 50)
            {
                break;
            }
        }

        return matches;
    }

    private object? GetCsvRecipeByNumber(int number)
    {
        if (!System.IO.File.Exists(_csvPath))
        {
            return null;
        }

        var index = 0;

        using var streamReader = new StreamReader(_csvPath);
        using var parser = CreateParser(streamReader);

        if (!parser.EndOfData)
        {
            parser.ReadFields();
        }

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields == null || fields.Length < 2)
            {
                continue;
            }

            index++;
            if (index == number)
            {
                return ToRecipeDetails(fields);
            }
        }

        return null;
    }
    private static object ToRecipeDetails(Recipe recipe)
    {
        return new
        {
            recipe = recipe.Title,
            ingredients = recipe.Ingredients,
            directions = recipe.Directions,
            link = NormalizeLink(recipe.Link),
            source = recipe.Source,
            data = (string?)null
        };
    }

    private static object ToRecipeDetails(IReadOnlyList<string> fields)
    {
        return new
        {
            recipe = fields.ElementAtOrDefault(0),
            ingredients = fields.ElementAtOrDefault(1),
            directions = fields.ElementAtOrDefault(2),
            link = NormalizeLink(fields.ElementAtOrDefault(3)),
            source = fields.ElementAtOrDefault(4),
            ner = fields.ElementAtOrDefault(5),
            data = string.Join(",", fields)
        };
    }

    private static TextFieldParser CreateParser(TextReader reader)
    {
        var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true
        };
        parser.SetDelimiters(",");
        return parser;
    }

    private static string ResolveCsvPath(string configuredPath, string contentRootPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }

    private static string NormalizeLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return string.Empty;
        }

        return link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               link.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? link
            : $"https://{link}";
    }
}
