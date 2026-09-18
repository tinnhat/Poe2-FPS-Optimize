using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PoeRedux.Models;

public class ColorModsViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    private string _selectedColor;

    public ColorModsOption Option { get; }
    public string Name => Option.Name;

    public IReadOnlyList<string> AvailableColors => Services.ModColorGroups.Names;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public string SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (_selectedColor != value)
            {
                _selectedColor = value;
                OnPropertyChanged();
            }
        }
    }

    public ColorModsViewModel(ColorModsOption option)
    {
        Option = option;
        _isSelected = option.IsEnabled;
        _selectedColor = Services.ModColorGroups.Normalize(option.Color);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
