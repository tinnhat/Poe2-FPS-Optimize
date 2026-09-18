using System.ComponentModel;
using System.Runtime.CompilerServices;
using PoeRedux.Patches;
using PoeRedux.Services;
using System.Windows;

namespace PoeRedux.Models;

public class PatchViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public IPatch Patch { get; }
    public string Name => LocalizationService.PatchName(Patch.Name);
    public string Description => LocalizationService.PatchDescription(
        Patch.Name, Patch.Description?.ToString() ?? string.Empty);
    public string Category => LocalizationService.Text(Patch switch
    {
        ColorMods2 or ClientStrings or MonsterHP => "ReadabilityCategory",
        Camera or Minimap => "MapCameraCategory",
        _ => "VisualNoiseCategory"
    });
    public Visibility OptionsVisibility => Patch is IConfigurablePatch ? Visibility.Visible : Visibility.Collapsed;

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

    public PatchViewModel(IPatch patch)
    {
        Patch = patch;
        _isSelected = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Category));
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
