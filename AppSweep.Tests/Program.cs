using AppSweep;

static class Program
{
    private static int Main()
    {
        var failures = new List<string>();

        var exportRequest = CommandLineOptions.Parse(new[] { "--export", "out.csv" });
        AssertTrue(exportRequest.HasExport, "--export should be detected", failures);
        AssertEqual("out.csv", exportRequest.ExportPath, "--export should accept the next argument as the path", failures);

        var removeRequest = CommandLineOptions.Parse(new[] { "--remove", "Adobe*" });
        AssertTrue(removeRequest.RemoveRequested, "--remove should be detected", failures);
        AssertEqual("Adobe*", removeRequest.RemovePattern, "--remove should accept the next argument as the pattern", failures);

        var equalsRequest = CommandLineOptions.Parse(new[] { "--remove=Adobe*" });
        AssertTrue(equalsRequest.RemoveRequested, "--remove= should be detected", failures);
        AssertEqual("Adobe*", equalsRequest.RemovePattern, "--remove should accept = syntax", failures);

        AssertTrue(ProductMatcher.Matches("Adobe*", "Adobe Acrobat Reader"), "Wildcard pattern should match matching product names", failures);
        AssertTrue(ProductMatcher.Matches("Adobe", "Adobe Acrobat Reader"), "Plain pattern should match by substring", failures);
        AssertFalse(ProductMatcher.Matches("Adobe*", "Google Chrome"), "Wildcard pattern should not match unrelated names", failures);

        if (failures.Count > 0)
        {
            foreach (var failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            return 1;
        }

        Console.WriteLine("All tests passed.");
        return 0;
    }

    private static void AssertEqual<T>(T expected, T actual, string message, ICollection<string> failures)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            failures.Add($"FAIL: {message}. Expected '{expected}', got '{actual}'.");
        }
    }

    private static void AssertTrue(bool condition, string message, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add($"FAIL: {message}.");
        }
    }

    private static void AssertFalse(bool condition, string message, ICollection<string> failures)
    {
        if (condition)
        {
            failures.Add($"FAIL: {message}.");
        }
    }
}
