using System.Security.Cryptography;
using System.Text;

namespace FoodyBackend.Auth;

public static class TokenHasher
{
    public static string GenerateToken(int bytesLength = 32)
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(bytesLength));
    }

    public static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
