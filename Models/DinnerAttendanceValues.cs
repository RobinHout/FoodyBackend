namespace FoodyBackend.Models;

public static class DinnerAttendanceValues
{
    public const string Unknown = "unknown";
    public const string Yes = "yes";
    public const string No = "no";

    public static bool IsValid(string? value)
    {
        return string.Equals(value, Unknown, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Yes, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, No, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string value)
    {
        if (string.Equals(value, Yes, StringComparison.OrdinalIgnoreCase))
        {
            return Yes;
        }

        if (string.Equals(value, No, StringComparison.OrdinalIgnoreCase))
        {
            return No;
        }

        return Unknown;
    }
}
