using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AfriHospital.src.Desktop.ViewModels;

/// <summary>
/// Small MVVM foundation for shell-level presentation state.
/// Module-specific data and commands can be migrated here incrementally.
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private string _pageTitle = "Tableau de bord";
    private string _statusMessage = "Connecté à la base locale.";
    private string _currentUserName = string.Empty;
    private string _currentUserRole = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PageTitle
    {
        get => _pageTitle;
        set => SetField(ref _pageTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string CurrentUserName
    {
        get => _currentUserName;
        set => SetField(ref _currentUserName, value);
    }

    public string CurrentUserRole
    {
        get => _currentUserRole;
        set => SetField(ref _currentUserRole, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
