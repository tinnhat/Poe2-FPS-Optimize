using PoeRedux.Patches;
using PoeRedux.Services;
using System.Windows;

namespace PoeRedux;

public partial class PatchOptionsWindow : Window
{
    private readonly IConfigurablePatch _patch;
    private readonly List<PatchOption> _options;

    public PatchOptionsWindow(string patchName, IConfigurablePatch patch)
    {
        _patch = patch;
        _options = patch.Options.Select(option => new PatchOption(option.Id,
            LocalizationService.Text("Option_" + option.Id), option.IsEnabled)).ToList();
        InitializeComponent();
        Title = patchName;
        HeadingText.Text = patchName;
        CancelButton.Content = LocalizationService.Text("Cancel");
        SaveButton.Content = LocalizationService.Text("Save");
        OptionsList.ItemsSource = _options;
        Owner = Application.Current.MainWindow;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_options.Any(option => option.IsEnabled))
        {
            MessageBox.Show(LocalizationService.Text("ChoosePatchOption"), LocalizationService.Text("Error"));
            return;
        }
        foreach (var option in _patch.Options)
            option.IsEnabled = _options.First(edited => edited.Id == option.Id).IsEnabled;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
