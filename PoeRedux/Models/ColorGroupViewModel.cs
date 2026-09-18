using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PoeRedux.Models;

public sealed class ColorGroupViewModel(string name, string hex) : INotifyPropertyChanged
{
    private string _hex = hex;
    public string Name { get; } = name;
    public string Hex
    {
        get => _hex;
        set { if (_hex == value) return; _hex = value; PropertyChanged?.Invoke(this, new(nameof(Hex))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
