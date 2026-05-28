using System.Windows;

namespace AppSweep;

internal enum AppTheme
{
    Light,
    Dark
}

internal static class ThemeService
{
    private static readonly Uri LightThemeUri = new("Themes/Light.xaml", UriKind.Relative);
    private static readonly Uri DarkThemeUri = new("Themes/Dark.xaml", UriKind.Relative);

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public static void Apply(AppTheme theme)
    {
        if (Application.Current is null)
        {
            CurrentTheme = theme;
            return;
        }

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existingThemeDictionary = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("Themes/", StringComparison.OrdinalIgnoreCase) == true);

        if (existingThemeDictionary is not null)
        {
            dictionaries.Remove(existingThemeDictionary);
        }

        dictionaries.Add(new ResourceDictionary
        {
            Source = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri
        });

        CurrentTheme = theme;
    }

    public static void Toggle(bool useDarkTheme) => Apply(useDarkTheme ? AppTheme.Dark : AppTheme.Light);
}
