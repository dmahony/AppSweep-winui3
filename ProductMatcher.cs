using System.Text.RegularExpressions;

namespace AppSweep;

public static class ProductMatcher
{
    private static readonly Regex WildcardRegexTemplate = new(@"[\*\?]", RegexOptions.Compiled);

    public static bool Matches(string pattern, string candidate)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var trimmedPattern = pattern.Trim();
        var trimmedCandidate = candidate.Trim();

        if (!WildcardRegexTemplate.IsMatch(trimmedPattern))
        {
            return trimmedCandidate.Contains(trimmedPattern, StringComparison.OrdinalIgnoreCase);
        }

        var regex = new Regex(
            "^" + Regex.Escape(trimmedPattern)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".") + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

        return regex.IsMatch(trimmedCandidate);
    }
}
