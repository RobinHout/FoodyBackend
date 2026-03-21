using DotNetEnv;
using FoodyBackend;
using Microsoft.EntityFrameworkCore;

var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFilePath))
{
    Env.Load(envFilePath);
}

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var railwayPort = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(railwayPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{railwayPort}");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No MySQL connection string was found. Configure ConnectionStrings:DefaultConnection or the ConnectionStrings__DefaultConnection environment variable.");
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
    ["http://localhost:3000", "https://robinhout.github.io"];

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowConfiguredOrigins",
        corsBuilder =>
        {
            corsBuilder.WithOrigins(allowedOrigins)
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .AllowCredentials();
        });
});
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseMySql(
        connectionString,
        DatabaseServerVersionFactory.Build(builder.Configuration),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
var app = builder.Build();
await app.InitializeDatabaseAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowConfiguredOrigins");
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
// app.UseAuthentication();
// app.UseAuthorization();
app.MapGet("/", () => Results.Ok(new
{
    message = "FoodyBackend is running",
    database = "MySQL",
    environment = app.Environment.EnvironmentName
}));
app.MapControllers();

app.Run();
