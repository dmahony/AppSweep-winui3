using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AppSweep;

public sealed class MainWindow : Window
{
    private readonly InstalledProductService _service = new();
    private bool _isLoading;

    private readonly Grid _rootGrid;
    private readonly TextBox _searchBox;
    private readonly Button _refreshButton;
    private readonly Button _selectAllButton;
    private readonly Button _deselectAllButton;
    private readonly ProgressRing _loadingRing;
    private readonly TextBlock _countText;
    private readonly ListView _productsListView;
    private readonly Button _uninstallButton;
    private readonly Button _forceRemoveButton;
    private readonly TextBlock _statusText;
    private readonly TextBox _logTextBox;
    private readonly Dictionary<string, CheckBox> _selectionBoxes = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<InstalledProduct> AllProducts { get; } = new();
    public ObservableCollection<InstalledProduct> FilteredProducts { get; } = new();

    public MainWindow()
    {
        Title = "AppSweep";

        _searchBox = new TextBox
        {
            PlaceholderText = "Search by name, product code, version, or date"
        };
        _searchBox.TextChanged += SearchBox_TextChanged;

        _refreshButton = new Button { Content = "Refresh List" };
        _refreshButton.Click += RefreshButton_Click;

        _selectAllButton = new Button { Content = "Select All" };
        _selectAllButton.Click += SelectAllButton_Click;

        _deselectAllButton = new Button { Content = "Deselect All" };
        _deselectAllButton.Click += DeselectAllButton_Click;

        _loadingRing = new ProgressRing
        {
            Width = 28,
            Height = 28,
            IsActive = false
        };

        _countText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Application.Current.Resources["SystemControlForegroundBaseMediumBrush"] as Brush,
            Text = string.Empty
        };

        _productsListView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = false
        };

        _uninstallButton = new Button
        {
            Content = "Uninstall Selected",
            IsEnabled = false
        };
        _uninstallButton.Click += UninstallButton_Click;

        _forceRemoveButton = new Button
        {
            Content = "Force Remove Selected",
            IsEnabled = false
        };
        _forceRemoveButton.Click += ForceRemoveButton_Click;

        _statusText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Application.Current.Resources["SystemControlForegroundBaseMediumBrush"] as Brush,
            Margin = new Thickness(8, 0, 0, 0)
        };

        _logTextBox = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
        };

        _rootGrid = BuildLayout();
        _rootGrid.Loaded += RootGrid_Loaded;
        Content = _rootGrid;

        SetStatus("Ready");
    }

    private Grid BuildLayout()
    {
        var root = new Grid
        {
            Padding = new Thickness(16),
            RowSpacing = 12
        };

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(200) });

        var topRow = new Grid { ColumnSpacing = 12 };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var searchLabel = new TextBlock { Text = "Search:", VerticalAlignment = VerticalAlignment.Center };
        topRow.Children.Add(searchLabel);
        Grid.SetColumn(_searchBox, 1);
        topRow.Children.Add(_searchBox);
        Grid.SetColumn(_refreshButton, 2);
        topRow.Children.Add(_refreshButton);
        Grid.SetColumn(_selectAllButton, 3);
        topRow.Children.Add(_selectAllButton);
        Grid.SetColumn(_deselectAllButton, 4);
        topRow.Children.Add(_deselectAllButton);
        Grid.SetColumn(_loadingRing, 5);
        topRow.Children.Add(_loadingRing);
        Grid.SetColumn(_countText, 6);
        topRow.Children.Add(_countText);

        var headerRow = new Grid { Margin = new Thickness(6, 0, 6, 0), ColumnSpacing = 12 };
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });

        var productCodeHeader = new TextBlock { Text = "Product Code", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        Grid.SetColumn(productCodeHeader, 1);
        headerRow.Children.Add(productCodeHeader);
        var nameHeader = new TextBlock { Text = "Name", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        Grid.SetColumn(nameHeader, 2);
        headerRow.Children.Add(nameHeader);
        var versionHeader = new TextBlock { Text = "Version", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        Grid.SetColumn(versionHeader, 3);
        headerRow.Children.Add(versionHeader);
        var dateHeader = new TextBlock { Text = "Install Date", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        Grid.SetColumn(dateHeader, 4);
        headerRow.Children.Add(dateHeader);

        var buttonsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        buttonsRow.Children.Add(_uninstallButton);
        buttonsRow.Children.Add(_forceRemoveButton);
        buttonsRow.Children.Add(_statusText);

        var logContainer = new Grid { RowSpacing = 8 };
        logContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        logContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        logContainer.Children.Add(new TextBlock { Text = "Activity Log", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Grid.SetRow(_logTextBox, 1);
        logContainer.Children.Add(_logTextBox);

        root.Children.Add(topRow);
        Grid.SetRow(headerRow, 1);
        root.Children.Add(headerRow);
        Grid.SetRow(_productsListView, 2);
        root.Children.Add(_productsListView);
        Grid.SetRow(buttonsRow, 3);
        root.Children.Add(buttonsRow);
        Grid.SetRow(logContainer, 4);
        root.Children.Add(logContainer);

        return root;
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshProductsAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _searchBox.Text = string.Empty;
        await RefreshProductsAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(_searchBox.Text);
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
            _selectionBoxes.Clear();
            foreach (var product in products)
            {
                product.PropertyChanged += Product_PropertyChanged;
                AllProducts.Add(product);
            }

            ApplyFilter(_searchBox.Text);
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
                product.InstallDate.Contains(term, StringComparison.OrdinalIgnoreCase)));

        FilteredProducts.Clear();
        _productsListView.Items.Clear();
        foreach (var product in matches)
        {
            FilteredProducts.Add(product);
            _productsListView.Items.Add(BuildProductRow(product));
        }

        _countText.Text = $"{FilteredProducts.Count} shown / {AllProducts.Count} total";
        SetStatus($"Displaying {FilteredProducts.Count} products");
        UpdateActionState();
    }

    private UIElement BuildProductRow(InstalledProduct product)
    {
        var checkBox = new CheckBox
        {
            IsChecked = product.IsSelected,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };

        checkBox.Checked += (_, _) => product.IsSelected = true;
        checkBox.Unchecked += (_, _) => product.IsSelected = false;
        _selectionBoxes[product.ProductCode] = checkBox;

        var row = new Grid
        {
            Padding = new Thickness(6, 6, 6, 6),
            ColumnSpacing = 12,
            BorderBrush = Application.Current.Resources["SystemControlForegroundBaseLowBrush"] as Brush,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });

        row.Children.Add(checkBox);

        var productCodeText = new TextBlock { Text = product.ProductCode, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(productCodeText, 1);
        row.Children.Add(productCodeText);

        var nameText = new TextBlock { Text = product.Name, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(nameText, 2);
        row.Children.Add(nameText);

        var versionText = new TextBlock { Text = product.Version, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(versionText, 3);
        row.Children.Add(versionText);

        var installDateText = new TextBlock { Text = product.InstallDate, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(installDateText, 4);
        row.Children.Add(installDateText);

        return new Border { Child = row };
    }

    private void Product_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstalledProduct.IsSelected))
        {
            if (sender is InstalledProduct product && _selectionBoxes.TryGetValue(product.ProductCode, out var checkBox))
            {
                checkBox.IsChecked = product.IsSelected;
            }

            UpdateActionState();
        }
    }

    private void UpdateActionState()
    {
        var hasCheckedItems = AllProducts.Any(product => product.IsSelected);
        _uninstallButton.IsEnabled = hasCheckedItems && !_isLoading;
        _forceRemoveButton.IsEnabled = hasCheckedItems && !_isLoading;
    }

    private void SetBusy(bool isBusy)
    {
        _isLoading = isBusy;
        _loadingRing.IsActive = isBusy;
        _refreshButton.IsEnabled = !isBusy;
        _selectAllButton.IsEnabled = !isBusy;
        _deselectAllButton.IsEnabled = !isBusy;
        _searchBox.IsEnabled = !isBusy;
        UpdateActionState();
    }

    private void AppendLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}: {message}{Environment.NewLine}";

        if (DispatcherQueue.HasThreadAccess)
        {
            _logTextBox.Text += line;
            _logTextBox.SelectionStart = _logTextBox.Text.Length;
            _logTextBox.SelectionLength = 0;
        }
        else
        {
            _ = DispatcherQueue.TryEnqueue(() => AppendLog(message));
        }

        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        _statusText.Text = message;
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
            XamlRoot = _rootGrid.XamlRoot
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
            XamlRoot = _rootGrid.XamlRoot
        };

        await dialog.ShowAsync();
    }
}
