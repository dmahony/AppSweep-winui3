namespace AppSweep;

public sealed record CommandLineOptions(bool HelpRequested, bool ExportRequested, string? ExportPath, bool RemoveRequested, string? RemovePattern)
{
    public bool HasHelp => HelpRequested;
    public bool HasExport => ExportRequested;
    public bool HasRemove => RemoveRequested;

    public static CommandLineOptions Parse(IEnumerable<string> args)
    {
        var values = args.ToArray();
        for (var i = 0; i < values.Length; i++)
        {
            var current = values[i];

            if (IsFlag(current, "--help"))
            {
                return new CommandLineOptions(true, false, null, false, null);
            }

            if (TryParseValue(current, "--export", values, ref i, out var exportPath))
            {
                return new CommandLineOptions(false, true, exportPath, false, null);
            }

            if (TryParseValue(current, "--remove", values, ref i, out var removePattern))
            {
                return new CommandLineOptions(false, false, null, true, removePattern);
            }
        }

        return new CommandLineOptions(false, false, null, false, null);
    }

    private static bool IsFlag(string current, string flagName) =>
        current.Equals(flagName, StringComparison.OrdinalIgnoreCase) ||
        current.StartsWith($"{flagName}=", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseValue(string current, string flagName, IReadOnlyList<string> values, ref int index, out string? value)
    {
        value = null;

        var exactFlag = current.Equals(flagName, StringComparison.OrdinalIgnoreCase);
        var equalsForm = current.StartsWith($"{flagName}=", StringComparison.OrdinalIgnoreCase);
        if (!exactFlag && !equalsForm)
        {
            return false;
        }

        var equalsIndex = current.IndexOf('=');
        if (equalsIndex >= 0)
        {
            value = NormalizeValue(current[(equalsIndex + 1)..]);
            return true;
        }

        if (index + 1 < values.Count && !values[index + 1].StartsWith("-", StringComparison.Ordinal))
        {
            value = NormalizeValue(values[++index]);
            return true;
        }

        value = null;
        return true;
    }

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
