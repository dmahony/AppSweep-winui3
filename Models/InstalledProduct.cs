using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AppSweep;

public sealed class InstalledProduct : INotifyPropertyChanged
{
    private bool _isSelected;

    public string ProductCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string InstallDate { get; init; } = "Unknown";
    public string UninstallString { get; init; } = string.Empty;
    public string InstallSource { get; init; } = string.Empty;
    public string LocalPackage { get; init; } = string.Empty;
    public string SourceStatus { get; init; } = "Unknown";
    public string RegistryScope { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
