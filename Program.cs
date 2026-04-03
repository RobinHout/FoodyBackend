using FoodyBackend;
using FoodyBackend.Auth;
using FoodyBackend.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var port = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var connectionString = ResolvePostgresConnectionString(builder.Configuration);
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No PostgreSQL connection string was found. Set the Railway variable DATABASE_URL.");
}

var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
if (string.IsNullOrWhiteSpace(connectionStringBuilder.Host) ||
    string.IsNullOrWhiteSpace(connectionStringBuilder.Database) ||
    string.IsNullOrWhiteSpace(connectionStringBuilder.Username) ||
    string.IsNullOrWhiteSpace(connectionStringBuilder.Password))
{
    throw new InvalidOperationException(
        "The PostgreSQL connection string must include Host, Database, Username, and Password.");
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(corsBuilder =>
    {
        corsBuilder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(connectionStringBuilder.ConnectionString));

builder.Services
    .AddAuthentication(AuthConstants.Scheme)
    .AddScheme<AuthenticationSchemeOptions, BearerSessionAuthenticationHandler>(
        AuthConstants.Scheme,
        _ => { });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthSessionService, AuthSessionService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(AuthConstants.Scheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "Foody access token",
        In = ParameterLocation.Header,
        Description = "Use `Bearer {accessToken}` for protected endpoints."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = AuthConstants.Scheme
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    await database.Database.MigrateAsync();

    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    await LegacyPasswordMigration.UpgradePlainTextPasswordsAsync(database, passwordHasher);
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
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

static string? ResolvePostgresConnectionString(ConfigurationManager configuration)
{
    var databaseUrl = configuration["DATABASE_URL"];
    if (string.IsNullOrWhiteSpace(databaseUrl))
    {
        return null;
    }

    return TryConvertDatabaseUrl(databaseUrl);
}

static string TryConvertDatabaseUrl(string databaseUrl)
{
    if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) ||
        !(string.Equals(uri.Scheme, "postgres", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(uri.Scheme, "postgresql", StringComparison.OrdinalIgnoreCase)))
    {
        return databaseUrl;
    }

    var userInfoParts = uri.UserInfo.Split(':', 2);
    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
        Username = userInfoParts.Length > 0 ? Uri.UnescapeDataString(userInfoParts[0]) : string.Empty,
        Password = userInfoParts.Length > 1 ? Uri.UnescapeDataString(userInfoParts[1]) : string.Empty
    };

    foreach (var segment in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var pair = segment.Split('=', 2);
        var key = Uri.UnescapeDataString(pair[0]);
        var value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty;

        if (string.Equals(key, "sslmode", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<SslMode>(value, true, out var sslMode))
        {
            builder.SslMode = sslMode;
            continue;
        }

    }

    return builder.ConnectionString;
}
