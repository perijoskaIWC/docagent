using System.Text.RegularExpressions;

namespace DocAgent.Api;

/// <summary>Small text rules the endpoints share.</summary>
internal static partial class ApiText
{
    public static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Lower case, with every run of other characters turned into one dash: "System assessment: helio-full" →
    /// "system-assessment-helio-full". Letters in any script are kept. Empty when nothing usable is left.
    /// </summary>
    public static string Slug(string text, int maxLength = 80)
    {
        var slug = NotLetterOrDigit().Replace(text.ToLowerInvariant(), "-").Trim('-');
        return slug.Length <= maxLength ? slug : slug[..maxLength].TrimEnd('-');
    }

    /// <summary>
    /// Enum names only, in any case. Enum.TryParse on its own also accepts numbers ("3", "-1") and comma lists
    /// ("Pattern,Lesson"), which would let undefined values in.
    /// </summary>
    public static bool TryParseName<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        return value is { Length: > 0 } && value.All(char.IsLetter) && Enum.TryParse(value, ignoreCase: true, out result);
    }

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NotLetterOrDigit();
}
