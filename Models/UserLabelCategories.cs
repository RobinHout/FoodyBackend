namespace FoodyBackend.Models;

public static class UserLabelCategories
{
    public const string Allergy = "allergy";
    public const string Preference = "preference";

    public static bool IsValid(string? value)
    {
        return string.Equals(value, Allergy, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Preference, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string value)
    {
        return string.Equals(value, Allergy, StringComparison.OrdinalIgnoreCase)
            ? Allergy
            : Preference;
    }
}
