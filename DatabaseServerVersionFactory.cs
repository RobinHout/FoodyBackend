using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FoodyBackend;

public static class DatabaseServerVersionFactory
{
    private static readonly Version DefaultVersion = new(8, 0, 36);

    public static ServerVersion Build(IConfiguration configuration)
    {
        var configuredVersion = configuration["Database:ServerVersion"];
        if (Version.TryParse(configuredVersion, out var parsedVersion))
        {
            return new MySqlServerVersion(parsedVersion);
        }

        return new MySqlServerVersion(DefaultVersion);
    }
}
