using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace AppSweep;

public partial class MainWindow : Window
{
    private readonly InstalledProductService _service = new();
    private bool _isLoading;
    private bool _isThemeSyncing;

    public ObservableCollection<InstalledProduct> AllProducts { get; } = new();
    public ObservableCollection<InstalledProduct> FilteredProducts { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        SearchTextBox.TextChanged += SearchTextBox_TextChanged;
        RefreshButton.Click += RefreshButton_Click;
        SelectAllButton.Click += SelectAllButton_Click;
        DeselectAllButton.Click += DeselectAllButton_Click;
        RunButton.Click += RunButton_Click;
        CleanupButton.Click += CleanupButton_Click;
        ThemeCheckBox.Checked += ThemeCheckBox_Checked;
        ThemeCheckBox.Unchecked += ThemeCheckBox_Unchecked;
        Loaded += MainWindow_Loaded;

        MethodComboBox.SelectedIndex = 0;
        ProductsGrid.ItemsSource = FilteredProducts;

        _isThemeSyncing = true;
        ThemeCheckBox.IsChecked = ThemeService.CurrentTheme == AppTheme.Dark;
        _isThemeSyncing = false;

        SetStatus("Ready");
        UpdateCounts();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshProductsAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = string.Empty;
        await RefreshProductsAsync();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(SearchTextBox.Text);
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var product in FilteredProducts)
        {
            product.IsSelected = true;
        }

        UpdateActionState();
    }

    private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var product in FilteredProducts)
        {
            product.IsSelected = false;
        }

        UpdateActionState();
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        await ProcessSelectedAsync(GetSelectedMethod());
    }

    private async void CleanupButton_Click(object sender, RoutedEventArgs e)
    {
        await ProcessSelectedAsync(RemovalMethod.OrphanedRegistryCleanup);
    }

    private void ThemeCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_isThemeSyncing)
        {
            return;
        }

        ThemeService.Apply(AppTheme.Dark);
    }

    private void ThemeCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isThemeSyncing)
        {
            return;
        }

        ThemeService.Apply(AppTheme.Light);
    }

    private async Task RefreshProductsAsync()
    {
        if (_isLoading)
        {
            return;
        }

        try
        {
            SetBusy(true);
            AppendLog("Retrieving list of installed MSI products...");
            var products = await Task.Run(() => _service.GetInstalledProducts());

            foreach (var existing in AllProducts)
            {
                existing.PropertyChanged -= Product_PropertyChanged;
            }

            AllProducts.Clear();
            foreach (var product in products)
            {
                product.PropertyChanged += Product_PropertyChanged;
                AllProducts.Add(product);
            }

            ApplyFilter(SearchTextBox.Text);
            AppendLog($"Found {AllProducts.Count} unique installed products.");
            AppendLog("Package list refreshed.");
        }
        catch (Exception ex)
        {
            AppendLog($"Error retrieving installed products: {ex.Message}");
            SetStatus("Failed to refresh package list.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ProcessSelectedAsync(RemovalMethod method)
    {
        var selected = AllProducts.Where(product => product.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await ShowMessageAsync("No Selection", method == RemovalMethod.OrphanedRegistryCleanup
                ? "Please select products to clean up."
                : "Please select products to remove.");
            return;
        }

        var summary = string.Join(Environment.NewLine + Environment.NewLine,
            selected.Select(product =>
                $"{product.Name} (v{product.Version})\n{product.ProductCode}\n{product.SourceStatus}"));

        var warningText = method switch
        {
            RemovalMethod.WindowsInstallerApi => "This uses the Windows Installer API to uninstall the selected products.",
            RemovalMethod.MsiExec => "This calls msiexec.exe directly to uninstall the selected products.",
            RemovalMethod.OrphanedRegistryCleanup => "WARNING: This only removes broken uninstall entries from the registry.",
            _ => "This will try Windows Installer API removal, then msiexec.exe, then registry cleanup if needed."
        };

        var confirmed = await ShowConfirmationAsync(
            method == RemovalMethod.OrphanedRegistryCleanup ? "Confirm Registry Cleanup" : "Confirm Removal",
            $"{warningText}\n\n{summary}");

        if (!confirmed)
        {
            return;
        }

        try
        {
            SetBusy(true);
            foreach (var product in selected)
            {
                AppendLog($"Processing: {product.Name} (v{product.Version})");
                await _service.RemoveProductAsync(
                    product,
                    method,
                    AppendLog,
                    CancellationToken.None);
                await Task.Delay(250);
            }

            AppendLog(method == RemovalMethod.OrphanedRegistryCleanup
                ? "Registry cleanup completed. Refresh the list to update the display."
                : "Removal pass completed. Refresh the list to update the display.");
        }
        finally
        {
            SetBusy(false);
            await RefreshProductsAsync();
        }
    }

    private void ApplyFilter(string? searchTerm)
    {
        var term = searchTerm?.Trim() ?? string.Empty;
        var matches = string.IsNullOrWhiteSpace(term)
            ? AllProducts
            : new ObservableCollection<InstalledProduct>(AllProducts.Where(product =>
                product.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                product.ProductCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                product.Version.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                product.InstallDate.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                product.SourceStatus.Contains(term, StringComparison.OrdinalIgnoreCase)));

        FilteredProducts.Clear();
        foreach (var product in matches)
        {
            FilteredProducts.Add(product);
        }

        UpdateCounts();
        SetStatus($"Displaying {FilteredProducts.Count} products");
        UpdateActionState();
        ProductsGrid.Items.Refresh();
    }

    private void Product_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstalledProduct.IsSelected))
        {
            UpdateActionState();
        }
    }

    private void UpdateCounts()
    {
        CountTextBlock.Text = $"{FilteredProducts.Count} shown / {AllProducts.Count} total";
    }

    private void UpdateActionState()
    {
        var hasCheckedItems = AllProducts.Any(product => product.IsSelected);
        var enabled = hasCheckedItems && !_isLoading;
        RunButton.IsEnabled = enabled;
        CleanupButton.IsEnabled = enabled;
    }

    private void SetBusy(bool isBusy)
    {
        _isLoading = isBusy;
        LoadingBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !isBusy;
        SelectAllButton.IsEnabled = !isBusy;
        DeselectAllButton.IsEnabled = !isBusy;
        SearchTextBox.IsEnabled = !isBusy;
        MethodComboBox.IsEnabled = !isBusy;
        ProductsGrid.IsEnabled = !isBusy;
        UpdateActionState();
    }

    private void AppendLog(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendLog(message));
            return;
        }

        var line = $"{DateTime.Now:HH:mm:ss}: {message}{Environment.NewLine}";
        LogTextBox.Text += line;
        LogTextBox.ScrollToEnd();
        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    private RemovalMethod GetSelectedMethod()
    {
        return (MethodComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
        {
            "Windows Installer API" => RemovalMethod.WindowsInstallerApi,
            "msiexec.exe" => RemovalMethod.MsiExec,
            "Orphaned Registry Cleanup" => RemovalMethod.OrphanedRegistryCleanup,
            _ => RemovalMethod.Auto
        };
    }

    private Task<bool> ShowConfirmationAsync(string title, string content)
    {
        var result = MessageBox.Show(this, content, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    private Task ShowMessageAsync(string title, string content)
    {
        MessageBox.Show(this, content, title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }
}
