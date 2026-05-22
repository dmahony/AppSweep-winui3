using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AppSweep;

public sealed partial class MainWindow : Window
{
    private readonly InstalledProductService _service = new();
    private bool _isLoading;

    public ObservableCollection<InstalledProduct> AllProducts { get; } = new();
    public ObservableCollection<InstalledProduct> FilteredProducts { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        SetStatus("Ready");
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshProductsAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        await RefreshProductsAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
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

    private async void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        await ProcessSelectedAsync(forceRemoval: false);
    }

    private async void ForceRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        await ProcessSelectedAsync(forceRemoval: true);
    }

    private async Task RefreshProductsAsync()
    {
        if (_isLoading)
        {
            return;
        }

        await LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            SetBusy(true);
            AppendLog("Retrieving list of installed products...");
            AppendLog("Attempting registry method...");

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

            ApplyFilter(SearchBox.Text);
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

    private async Task ProcessSelectedAsync(bool forceRemoval)
    {
        var selected = AllProducts.Where(product => product.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await ShowMessageAsync("No Selection", forceRemoval
                ? "Please select products to force remove."
                : "Please select products to uninstall.");
            return;
        }

        var summary = string.Join(Environment.NewLine + Environment.NewLine,
            selected.Select(product => $"{product.Name} (v{product.Version})\n{product.ProductCode}"));

        var warningText = forceRemoval
            ? "WARNING: Force removal directly modifies the registry and can leave remnants on the system."
            : "Are you sure you want to uninstall the selected products?";

        var confirmed = await ShowConfirmationAsync(
            forceRemoval ? "Confirm Force Removal" : "Confirm Uninstall",
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
                await _service.UninstallProductAsync(
                    product.ProductCode,
                    forceRemoval,
                    AppendLog,
                    CancellationToken.None);
                await Task.Delay(500);
            }

            AppendLog(forceRemoval
                ? "Batch force removal completed. Use Refresh List to update the display."
                : "Batch uninstall completed. Use Refresh List to update the display.");
        }
        finally
        {
            SetBusy(false);
            await LoadProductsAsync();
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
                product.InstallDate.Contains(term, StringComparison.OrdinalIgnoreCase)));

        FilteredProducts.Clear();
        foreach (var product in matches)
        {
            FilteredProducts.Add(product);
        }

        CountText.Text = $"{FilteredProducts.Count} shown / {AllProducts.Count} total";
        SetStatus($"Displaying {FilteredProducts.Count} products");
        UpdateActionState();
    }

    private void Product_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstalledProduct.IsSelected))
        {
            UpdateActionState();
        }
    }

    private void UpdateActionState()
    {
        var hasCheckedItems = AllProducts.Any(product => product.IsSelected);
        UninstallButton.IsEnabled = hasCheckedItems && !_isLoading;
        ForceRemoveButton.IsEnabled = hasCheckedItems && !_isLoading;
    }

    private void SetBusy(bool isBusy)
    {
        _isLoading = isBusy;
        LoadingRing.IsActive = isBusy;
        RefreshButton.IsEnabled = !isBusy;
        SelectAllButton.IsEnabled = !isBusy;
        DeselectAllButton.IsEnabled = !isBusy;
        SearchBox.IsEnabled = !isBusy;
        UpdateActionState();
    }

    private void AppendLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}: {message}{Environment.NewLine}";

        if (DispatcherQueue.HasThreadAccess)
        {
            LogTextBox.Text += line;
            LogTextBox.SelectionStart = LogTextBox.Text.Length;
            LogTextBox.SelectionLength = 0;
        }
        else
        {
            _ = DispatcherQueue.TryEnqueue(() => AppendLog(message));
        }

        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private async Task<bool> ShowConfirmationAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowMessageAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            XamlRoot = RootGrid.XamlRoot
        };

        await dialog.ShowAsync();
    }
}
