using System.Data.Common;
using FoodyBackend;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var port = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
    builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No PostgreSQL connection string was found. Set the Railway variable ConnectionStrings__DefaultConnection or ConnectionStrings__Default to a full PostgreSQL connection string.");
}

var connectionStringBuilder = new DbConnectionStringBuilder
{
    ConnectionString = connectionString
};
var hasHost = connectionStringBuilder.TryGetValue("Host", out var host) &&
    !string.IsNullOrWhiteSpace(host?.ToString());
var hasDatabase = connectionStringBuilder.TryGetValue("Database", out var databaseName) &&
    !string.IsNullOrWhiteSpace(databaseName?.ToString());
var hasUsername = connectionStringBuilder.TryGetValue("Username", out var username) &&
    !string.IsNullOrWhiteSpace(username?.ToString());
var hasPassword = connectionStringBuilder.TryGetValue("Password", out var password) &&
    !string.IsNullOrWhiteSpace(password?.ToString());

if (!hasHost || !hasDatabase || !hasUsername || !hasPassword)
{
    throw new InvalidOperationException(
        "The PostgreSQL connection string must include Host, Database, Username, and Password.");
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
    ["http://localhost:3000", "https://robinhout.github.io"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(corsBuilder =>
    {
        if (allowedOrigins.Length == 0)
        {
            corsBuilder.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
            return;
        }

        corsBuilder.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    await database.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Ok(new
{
    message = "FoodyBackend is running",
    database = "PostgreSQL",
    environment = app.Environment.EnvironmentName
}));
app.MapControllers();

app.Run();
