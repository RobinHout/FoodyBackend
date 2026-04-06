using System.Text.Json;
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
    private const int ImportBatchSize = 250;
    private static readonly JsonSerializerOptions ImportJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

    [HttpPost("import-json")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportRecipesFromJson(
        [FromForm] IFormFile? file,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (limit is <= 0)
        {
            return BadRequest("Limit must be greater than 0 when it is provided.");
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("A JSON file is required.");
        }

        List<ImportedRecipe> importedRecipes;
        try
        {
            importedRecipes = await ParseImportedRecipesAsync(file, cancellationToken);
        }
        catch (JsonException exception)
        {
            return BadRequest($"Invalid JSON file: {exception.Message}");
        }

        if (importedRecipes.Count == 0)
        {
            return BadRequest("The uploaded JSON file does not contain any recipes.");
        }

        var labelRows = await context.Labels
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var labelIdsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in labelRows)
        {
            var key = NormalizeLabelKey(label.Name);
            if (string.IsNullOrWhiteSpace(key) || labelIdsByName.ContainsKey(key))
            {
                continue;
            }

            labelIdsByName[key] = label.Id;
        }

        var pendingLabelsByName = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        var skipped = 0;
        var labelsCreated = 0;
        var recipeLabelsCreated = 0;
        var batchCount = 0;

        foreach (var importedRecipe in importedRecipes.Take(limit ?? int.MaxValue))
        {
            var title = importedRecipe.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                skipped++;
                continue;
            }

            var ingredientItems = NormalizeList(importedRecipe.Ingredients);
            var directionSteps = NormalizeList(importedRecipe.Directions);
            var nerItems = NormalizeList(importedRecipe.Ner);

            var recipe = new Recipe
            {
                Title = title,
                Ingredients = FlattenIngredients(ingredientItems),
                IngredientItems = ingredientItems.ToArray(),
                Directions = FlattenDirections(directionSteps),
                DirectionSteps = directionSteps.ToArray(),
                Ner = nerItems.ToArray(),
                Link = NormalizeLink(importedRecipe.Link),
                Source = NormalizeValue(importedRecipe.Source)
            };

            context.Recipes.Add(recipe);

            foreach (var labelName in NormalizeLabels(importedRecipe.Labels))
            {
                var labelKey = NormalizeLabelKey(labelName);
                if (labelIdsByName.TryGetValue(labelKey, out var existingLabelId))
                {
                    context.RecipeLabels.Add(new RecipeLabel
                    {
                        Recipe = recipe,
                        LabelId = existingLabelId
                    });
                }
                else if (pendingLabelsByName.TryGetValue(labelKey, out var pendingLabel))
                {
                    context.RecipeLabels.Add(new RecipeLabel
                    {
                        Recipe = recipe,
                        Label = pendingLabel
                    });
                }
                else
                {
                    var newLabel = new Label
                    {
                        Name = labelName,
                        Description = string.Empty
                    };

                    context.Labels.Add(newLabel);
                    pendingLabelsByName[labelKey] = newLabel;
                    labelsCreated++;

                    context.RecipeLabels.Add(new RecipeLabel
                    {
                        Recipe = recipe,
                        Label = newLabel
                    });
                }

                recipeLabelsCreated++;
            }

            imported++;
            batchCount++;

            if (batchCount < ImportBatchSize)
            {
                continue;
            }

            await SaveImportBatchAsync(labelIdsByName, pendingLabelsByName, cancellationToken);
            batchCount = 0;
        }

        if (batchCount > 0)
        {
            await SaveImportBatchAsync(labelIdsByName, pendingLabelsByName, cancellationToken);
        }

        return Ok(new
        {
            imported,
            skipped,
            labelsCreated,
            recipeLabelsCreated,
            source = file.FileName
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

    private async Task SaveImportBatchAsync(
        IDictionary<string, int> labelIdsByName,
        IDictionary<string, Label> pendingLabelsByName,
        CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken);

        foreach (var pendingLabel in pendingLabelsByName)
        {
            labelIdsByName[pendingLabel.Key] = pendingLabel.Value.Id;
        }

        pendingLabelsByName.Clear();
        context.ChangeTracker.Clear();
    }

    private static async Task<List<ImportedRecipe>> ParseImportedRecipesAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        JsonElement? recipesElement = null;
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "recipes", StringComparison.OrdinalIgnoreCase))
                {
                    recipesElement = property.Value;
                    break;
                }
            }
        }

        return document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement.Deserialize<List<ImportedRecipe>>(ImportJsonSerializerOptions) ?? [],
            JsonValueKind.Object when recipesElement.HasValue && recipesElement.Value.ValueKind == JsonValueKind.Array =>
                recipesElement.Value.Deserialize<List<ImportedRecipe>>(ImportJsonSerializerOptions) ?? [],
            _ => throw new JsonException("Expected a JSON array or an object with a 'recipes' array.")
        };
    }

    private static object ToRecipeDetails(Recipe recipe)
    {
        return new
        {
            recipe = recipe.Title,
            ingredients = string.IsNullOrWhiteSpace(recipe.Ingredients)
                ? FlattenIngredients(recipe.IngredientItems)
                : recipe.Ingredients,
            directions = string.IsNullOrWhiteSpace(recipe.Directions)
                ? FlattenDirections(recipe.DirectionSteps)
                : recipe.Directions,
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

    private static List<string> NormalizeList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();
    }

    private static List<string> NormalizeLabels(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        var labels = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var trimmed = value?.Trim();
            var key = NormalizeLabelKey(trimmed);
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
            {
                continue;
            }

            labels.Add(trimmed!);
        }

        return labels;
    }

    private static string NormalizeLabelKey(string? label)
    {
        return string.IsNullOrWhiteSpace(label)
            ? string.Empty
            : label.Trim().ToLowerInvariant();
    }

    private static string NormalizeValue(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string FlattenIngredients(IEnumerable<string>? values)
    {
        return string.Join(", ", values ?? []);
    }

    private static string FlattenDirections(IEnumerable<string>? values)
    {
        return string.Join(Environment.NewLine, values ?? []);
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

    private sealed class ImportedRecipe
    {
        public string? Title { get; init; }
        public List<string>? Labels { get; init; }
        public List<string>? Ingredients { get; init; }
        public List<string>? Directions { get; init; }
        public List<string>? Ner { get; init; }
        public string? Link { get; init; }
        public string? Source { get; init; }
    }
}
