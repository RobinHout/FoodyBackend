using Microsoft.Extensions.Configuration;

namespace FoodyBackend;

public static class DatabaseConnectionStringFactory
{
    public static string Build(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var urlConnection = FirstNonEmpty(
            configuration["DATABASE_URL"],
            configuration["MYSQL_URL"],
            configuration["MYSQL_PUBLIC_URL"],
            configuration["MYSQL_URL_PUBLIC"]);

        if (!string.IsNullOrWhiteSpace(urlConnection))
        {
            return BuildFromUrl(urlConnection);
        }

        var host = FirstNonEmpty(
            configuration["MYSQLHOST"],
            configuration["MYSQL_HOST"],
            configuration["Database:Host"]);
        var port = FirstNonEmpty(
            configuration["MYSQLPORT"],
            configuration["MYSQL_PORT"],
            configuration["Database:Port"]) ?? "3306";
        var database = FirstNonEmpty(
            configuration["MYSQLDATABASE"],
            configuration["MYSQL_DATABASE"],
            configuration["Database:Name"]);
        var user = FirstNonEmpty(
            configuration["MYSQLUSER"],
            configuration["MYSQL_USER"],
            configuration["Database:User"]);
        var password = FirstNonEmpty(
            configuration["MYSQLPASSWORD"],
            configuration["MYSQL_PASSWORD"],
            configuration["Database:Password"]);
        var sslMode = FirstNonEmpty(
            configuration["MYSQL_SSLMODE"],
            configuration["Database:SslMode"]) ?? "Preferred";

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(database) ||
            string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "No MySQL connection configuration was found. Configure ConnectionStrings:DefaultConnection or the Railway-style MYSQLHOST, MYSQLPORT, MYSQLDATABASE, MYSQLUSER and MYSQLPASSWORD variables.");
        }

        return $"Server={host};Port={port};Database={database};User={user};Password={password};SslMode={sslMode};AllowPublicKeyRetrieval=True";
    }

    public static string BuildSafeDescription(string connectionString)
    {
        var parts = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2)
            .ToDictionary(
                part => part[0].Trim(),
                part => part[1].Trim(),
                StringComparer.OrdinalIgnoreCase);

        parts.Remove("Password");
        parts.Remove("Pwd");

        return string.Join(
            ';',
            parts
                .Where(part => part.Key is "Server" or "Port" or "Database" or "User ID" or "User" or "Uid")
                .Select(part => $"{part.Key}={part.Value}"));
    }

    private static string BuildFromUrl(string connectionUrl)
    {
        if (!Uri.TryCreate(connectionUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "mysql", StringComparison.OrdinalIgnoreCase))
        {
            return connectionUrl;
        }

        var userInfoParts = uri.UserInfo.Split(':', 2);
        var user = userInfoParts.Length > 0 ? Uri.UnescapeDataString(userInfoParts[0]) : string.Empty;
        var password = userInfoParts.Length > 1 ? Uri.UnescapeDataString(userInfoParts[1]) : string.Empty;
        var database = uri.AbsolutePath.Trim('/');

        return $"Server={uri.Host};Port={uri.Port};Database={database};User={user};Password={password};SslMode=Preferred;AllowPublicKeyRetrieval=True";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
