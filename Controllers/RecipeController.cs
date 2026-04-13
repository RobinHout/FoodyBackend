using System.Buffers;
using System.Text;
using System.Text.Json;
using FoodyBackend.Contracts;
using FoodyBackend.Models;
using FoodyBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace FoodyBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecipeController(
    DatabaseContext context,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    IDinnerRecommendationService recommendationService) : ControllerBase
{
    private const int ImportBatchSize = 250;
    private const string PrimaryJsonSourceKey = "primary";
    private const string SecondaryJsonSourceKey = "secondary";
    private static readonly JsonSerializerOptions ImportJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _csvPath = ResolveContentPath(
        configuration["Database:RecipesCsvPath"] ?? "Data/foodnetwork_recipes.csv",
        environment.ContentRootPath);
    private readonly string _primaryJsonPath = ResolveContentPath(
        configuration["Database:RecipesJsonPrimaryPath"] ?? "Data/labeledFoodnetwork.json",
        environment.ContentRootPath);
    private readonly string _secondaryJsonPath = ResolveContentPath(
        configuration["Database:RecipesJsonSecondaryPath"] ?? "Data/labeledFoodnetwork2.json",
        environment.ContentRootPath);

    [HttpGet("random")]
    public async Task<IActionResult> GetRandomRecipe(CancellationToken cancellationToken)
    {
        var recipe = await GetRandomDatabaseRecipeAsync(cancellationToken);
        if (recipe != null)
        {
            return Ok(await ToRecipeDetailsAsync(recipe, cancellationToken));
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
            .ToListAsync(cancellationToken);

        if (matches.Count > 0)
        {
            return Ok(await ToRecipeDetailsAsync(matches, cancellationToken));
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
            return Ok(await ToRecipeDetailsAsync(databaseRecipe, cancellationToken));
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
            return Ok(await ToRecipeDetailsAsync(recipe, cancellationToken));
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

        await recommendationService.RebuildAllDinnerRecommendationsAsync(cancellationToken);

        return Ok(new
        {
            imported,
            source = Path.GetFileName(_csvPath)
        });
    }

    [HttpPost("import-json")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportRecipesFromJson(
        IFormFile? file,
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

        try
        {
            using var stream = file.OpenReadStream();
            var summary = ImportRecipesFromJson(stream, limit, cancellationToken);

            if (summary.ParsedRecipes == 0)
            {
                return BadRequest("The uploaded JSON file does not contain any recipes.");
            }

            await recommendationService.RebuildAllDinnerRecommendationsAsync(cancellationToken);

            return Ok(new
            {
                imported = summary.Imported,
                skipped = summary.Skipped,
                labelsCreated = summary.LabelsCreated,
                recipeLabelsCreated = summary.RecipeLabelsCreated,
                source = file.FileName
            });
        }
        catch (JsonException exception)
        {
            context.ChangeTracker.Clear();
            return BadRequest($"Invalid JSON file: {exception.Message}");
        }
    }

    [HttpPost("import-json-source")]
    public async Task<IActionResult> ImportRecipesFromJsonSource(
        [FromQuery, BindRequired] string? source,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (limit is <= 0)
        {
            return BadRequest("Limit must be greater than 0 when it is provided.");
        }

        if (!TryResolveJsonImportSource(source, out var sourceKey, out var sourcePath))
        {
            return BadRequest("Source must be either 'primary' or 'secondary'.");
        }

        if (!System.IO.File.Exists(sourcePath))
        {
            return NotFound($"JSON source file not found at '{sourcePath}'.");
        }

        if (await context.Recipes.AnyAsync(cancellationToken))
        {
            return Conflict("The Recipes table already contains data. Import is blocked to prevent duplicates.");
        }

        try
        {
            using var stream = OpenJsonImportSourceStream(sourceKey, sourcePath);
            var summary = ImportRecipesFromJson(stream, limit, cancellationToken);

            if (summary.ParsedRecipes == 0)
            {
                return BadRequest("The selected JSON source does not contain any recipes.");
            }

            await recommendationService.RebuildAllDinnerRecommendationsAsync(cancellationToken);

            var sourceFile = Path.GetFileName(sourcePath);
            return Ok(new
            {
                imported = summary.Imported,
                skipped = summary.Skipped,
                labelsCreated = summary.LabelsCreated,
                recipeLabelsCreated = summary.RecipeLabelsCreated,
                source = sourceFile,
                sourceKey,
                sourceFile
            });
        }
        catch (JsonException exception)
        {
            context.ChangeTracker.Clear();
            return BadRequest($"Invalid JSON source: {exception.Message}");
        }
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

    private void SaveImportBatch(
        IDictionary<string, int> labelIdsByName,
        IDictionary<string, Label> pendingLabelsByName)
    {
        context.SaveChanges();

        foreach (var pendingLabel in pendingLabelsByName)
        {
            labelIdsByName[pendingLabel.Key] = pendingLabel.Value.Id;
        }

        pendingLabelsByName.Clear();
        context.ChangeTracker.Clear();
    }

    private JsonImportSummary ImportRecipesFromJson(
        Stream stream,
        int? limit,
        CancellationToken cancellationToken)
    {
        var labelRows = context.Labels
            .AsNoTracking()
            .ToList();
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

        var parsedRecipes = ParseImportedRecipes(stream, limit, importedRecipe =>
        {
            var title = importedRecipe.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                skipped++;
                return;
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
                return;
            }

            SaveImportBatch(labelIdsByName, pendingLabelsByName);
            batchCount = 0;
        }, cancellationToken);

        if (batchCount > 0)
        {
            SaveImportBatch(labelIdsByName, pendingLabelsByName);
        }

        return new JsonImportSummary(
            parsedRecipes,
            imported,
            skipped,
            labelsCreated,
            recipeLabelsCreated);
    }

    private bool TryResolveJsonImportSource(
        string? source,
        out string sourceKey,
        out string sourcePath)
    {
        if (string.Equals(source, PrimaryJsonSourceKey, StringComparison.OrdinalIgnoreCase))
        {
            sourceKey = PrimaryJsonSourceKey;
            sourcePath = _primaryJsonPath;
            return true;
        }

        if (string.Equals(source, SecondaryJsonSourceKey, StringComparison.OrdinalIgnoreCase))
        {
            sourceKey = SecondaryJsonSourceKey;
            sourcePath = _secondaryJsonPath;
            return true;
        }

        sourceKey = string.Empty;
        sourcePath = string.Empty;
        return false;
    }

    private static Stream OpenJsonImportSourceStream(string sourceKey, string sourcePath)
    {
        var fileStream = System.IO.File.OpenRead(sourcePath);
        if (!string.Equals(sourceKey, SecondaryJsonSourceKey, StringComparison.Ordinal))
        {
            return fileStream;
        }

        var prefixStream = new MemoryStream(Encoding.UTF8.GetBytes("{\"recipes\":["), writable: false);
        return new CombinedReadStream(prefixStream, fileStream);
    }

    private static int ParseImportedRecipes(
        Stream stream,
        int? limit,
        Action<ImportedRecipe> onRecipe,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
        var bytesInBuffer = 0;
        var state = new JsonReaderState(new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        var rootKind = ImportJsonRootKind.Unknown;
        var currentRootPropertyName = string.Empty;
        var insideRecipesArray = false;
        var recipesArrayDepth = -1;
        var parsedRecipeCount = 0;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                buffer = EnsureBufferCapacity(buffer, bytesInBuffer);

                var bytesRead = stream.Read(buffer, bytesInBuffer, buffer.Length - bytesInBuffer);
                var isFinalBlock = bytesRead == 0;
                bytesInBuffer += bytesRead;

                var reader = new Utf8JsonReader(
                    new ReadOnlySpan<byte>(buffer, 0, bytesInBuffer),
                    isFinalBlock,
                    state);

                var consumedBytes = 0;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var bytesBeforeRead = (int)reader.BytesConsumed;
                    var stateBeforeRead = reader.CurrentState;

                    if (!reader.Read())
                    {
                        consumedBytes = (int)reader.BytesConsumed;
                        state = reader.CurrentState;
                        break;
                    }

                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject:
                            if (rootKind == ImportJsonRootKind.Unknown && reader.CurrentDepth == 0)
                            {
                                rootKind = ImportJsonRootKind.Object;
                                break;
                            }

                            if (!insideRecipesArray || reader.CurrentDepth != recipesArrayDepth + 1)
                            {
                                break;
                            }

                            var recipeReader = reader;
                            if (!JsonDocument.TryParseValue(ref recipeReader, out var recipeDocument))
                            {
                                consumedBytes = bytesBeforeRead;
                                state = stateBeforeRead;
                                goto CompactBuffer;
                            }

                            reader = recipeReader;
                            consumedBytes = (int)reader.BytesConsumed;
                            state = reader.CurrentState;

                            ImportedRecipe? importedRecipe;
                            using (recipeDocument)
                            {
                                importedRecipe = recipeDocument.RootElement.Deserialize<ImportedRecipe>(ImportJsonSerializerOptions);
                            }

                            if (importedRecipe is null)
                            {
                                break;
                            }

                            parsedRecipeCount++;
                            onRecipe(importedRecipe);

                            if (limit.HasValue && parsedRecipeCount >= limit.Value)
                            {
                                return parsedRecipeCount;
                            }

                            break;

                        case JsonTokenType.StartArray:
                            if (rootKind == ImportJsonRootKind.Unknown && reader.CurrentDepth == 0)
                            {
                                rootKind = ImportJsonRootKind.Array;
                                insideRecipesArray = true;
                                recipesArrayDepth = 0;
                                break;
                            }

                            if (rootKind == ImportJsonRootKind.Object &&
                                !insideRecipesArray &&
                                reader.CurrentDepth == 1 &&
                                string.Equals(currentRootPropertyName, "recipes", StringComparison.OrdinalIgnoreCase))
                            {
                                insideRecipesArray = true;
                                recipesArrayDepth = 1;
                                currentRootPropertyName = string.Empty;
                            }

                            break;

                        case JsonTokenType.EndArray:
                            if (insideRecipesArray && reader.CurrentDepth == recipesArrayDepth)
                            {
                                return parsedRecipeCount;
                            }

                            break;

                        case JsonTokenType.PropertyName:
                            if (rootKind == ImportJsonRootKind.Object &&
                                !insideRecipesArray &&
                                reader.CurrentDepth == 1)
                            {
                                currentRootPropertyName = reader.GetString() ?? string.Empty;
                            }

                            break;
                    }

                    consumedBytes = (int)reader.BytesConsumed;
                    state = reader.CurrentState;
                }

CompactBuffer:
                if (consumedBytes > 0)
                {
                    Buffer.BlockCopy(buffer, consumedBytes, buffer, 0, bytesInBuffer - consumedBytes);
                    bytesInBuffer -= consumedBytes;
                }

                if (isFinalBlock)
                {
                    break;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        throw new JsonException("Expected a JSON array or an object with a 'recipes' array.");
    }

    private static byte[] EnsureBufferCapacity(byte[] buffer, int bytesInBuffer)
    {
        if (bytesInBuffer < buffer.Length)
        {
            return buffer;
        }

        var expandedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
        Buffer.BlockCopy(buffer, 0, expandedBuffer, 0, bytesInBuffer);
        ArrayPool<byte>.Shared.Return(buffer);
        return expandedBuffer;
    }

    private async Task<RecipeExportDto> ToRecipeDetailsAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        var labelsByRecipeId = await GetLabelsByRecipeIdAsync([recipe.Id], cancellationToken);
        return ToRecipeDetails(recipe, labelsByRecipeId.GetValueOrDefault(recipe.Id) ?? []);
    }

    private async Task<IReadOnlyList<RecipeExportDto>> ToRecipeDetailsAsync(
        IReadOnlyCollection<Recipe> recipes,
        CancellationToken cancellationToken)
    {
        if (recipes.Count == 0)
        {
            return [];
        }

        var orderedRecipes = recipes
            .OrderBy(recipe => recipe.Id)
            .ToList();
        var labelsByRecipeId = await GetLabelsByRecipeIdAsync(
            orderedRecipes.Select(recipe => recipe.Id).ToList(),
            cancellationToken);

        return orderedRecipes
            .Select(recipe => ToRecipeDetails(recipe, labelsByRecipeId.GetValueOrDefault(recipe.Id) ?? []))
            .ToList();
    }

    private async Task<Dictionary<int, List<string>>> GetLabelsByRecipeIdAsync(
        IReadOnlyCollection<int> recipeIds,
        CancellationToken cancellationToken)
    {
        if (recipeIds.Count == 0)
        {
            return [];
        }

        var labels = await context.RecipeLabels
            .AsNoTracking()
            .Where(link => recipeIds.Contains(link.RecipeId))
            .Select(link => new
            {
                link.RecipeId,
                LabelName = link.Label!.Name
            })
            .ToListAsync(cancellationToken);

        return labels
            .GroupBy(link => link.RecipeId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.LabelName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList());
    }

    private static RecipeExportDto ToRecipeDetails(Recipe recipe, IReadOnlyCollection<string> labels)
    {
        return new RecipeExportDto
        {
            Recipe = recipe.Title,
            Ingredients = string.IsNullOrWhiteSpace(recipe.Ingredients)
                ? FlattenIngredients(recipe.IngredientItems)
                : recipe.Ingredients,
            Directions = string.IsNullOrWhiteSpace(recipe.Directions)
                ? FlattenDirections(recipe.DirectionSteps)
                : recipe.Directions,
            Link = NormalizeLink(recipe.Link),
            Source = recipe.Source,
            Labels = labels
        };
    }

    private static RecipeExportDto ToRecipeDetails(IReadOnlyList<string> fields)
    {
        return new RecipeExportDto
        {
            Recipe = fields.ElementAtOrDefault(0) ?? string.Empty,
            Ingredients = fields.ElementAtOrDefault(1) ?? string.Empty,
            Directions = fields.ElementAtOrDefault(2) ?? string.Empty,
            Link = NormalizeLink(fields.ElementAtOrDefault(3)),
            Source = fields.ElementAtOrDefault(4) ?? string.Empty,
            Labels = [],
            Ner = fields.ElementAtOrDefault(5),
            Data = string.Join(",", fields)
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

    private static string ResolveContentPath(string configuredPath, string contentRootPath)
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

    private enum ImportJsonRootKind
    {
        Unknown,
        Array,
        Object
    }

    private sealed record JsonImportSummary(
        int ParsedRecipes,
        int Imported,
        int Skipped,
        int LabelsCreated,
        int RecipeLabelsCreated);

    private sealed class CombinedReadStream(Stream first, Stream second) : Stream
    {
        private readonly Stream[] _streams = [first, second];
        private int _streamIndex;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (_streamIndex < _streams.Length)
            {
                var bytesRead = _streams[_streamIndex].Read(buffer, offset, count);
                if (bytesRead > 0)
                {
                    return bytesRead;
                }

                _streamIndex++;
            }

            return 0;
        }

        public override int Read(Span<byte> buffer)
        {
            while (_streamIndex < _streams.Length)
            {
                var bytesRead = _streams[_streamIndex].Read(buffer);
                if (bytesRead > 0)
                {
                    return bytesRead;
                }

                _streamIndex++;
            }

            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var stream in _streams)
                {
                    stream.Dispose();
                }
            }

            base.Dispose(disposing);
        }
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

