using System.Text.RegularExpressions;

namespace GBV_Shield.Services;

public static class StorySafety
{
    public const int MinimumLength = 20;
    public const int MaximumLength = 2000;
    private static readonly Regex Email = new(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase);
    private static readonly Regex WebLink = new(@"\b(?:https?://|www\.)\S+", RegexOptions.IgnoreCase);
    private static readonly Regex Phone = new(@"(?<!\d)(?:\+?\d[\s().-]*){7,15}(?!\d)");

    public static string? Validate(string? content)
    {
        var value = content?.Trim() ?? string.Empty;
        if (value.Length < MinimumLength) return $"Please write at least {MinimumLength} characters.";
        if (value.Length > MaximumLength) return $"Please keep your story under {MaximumLength:N0} characters.";
        if (Email.IsMatch(value)) return "Please remove email addresses before sharing.";
        if (WebLink.IsMatch(value)) return "Please remove website links before sharing.";
        if (Phone.IsMatch(value)) return "Please remove phone numbers before sharing.";
        return null;
    }
}
