using FoodyBackend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Auth;

public static class LegacyPasswordMigration
{
    private const string IdentityPasswordHashPrefix = "AQAAAA";

    public static async Task UpgradePlainTextPasswordsAsync(
        DatabaseContext context,
        IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken = default)
    {
        var users = await context.Users.ToListAsync(cancellationToken);
        var changed = false;

        foreach (var user in users)
        {
            if (string.IsNullOrWhiteSpace(user.PasswordHash) || LooksHashed(user.PasswordHash))
            {
                continue;
            }

            user.PasswordHash = passwordHasher.HashPassword(user, user.PasswordHash);
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public static bool LooksHashed(string passwordHash)
    {
        return passwordHash.StartsWith(IdentityPasswordHashPrefix, StringComparison.Ordinal);
    }
}
