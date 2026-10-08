using PoeRedux.Models;
using System.Collections.ObjectModel;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PoeRedux.Services;

namespace PoeRedux;

public partial class ColorModsEditor : Window
{
    private readonly ObservableCollection<ColorGroupViewModel> _groups;

    public ColorModsEditor(ObservableCollection<ColorModsViewModel> colorMods,
        ObservableCollection<ColorGroupViewModel> groups, bool tagPrefix)
    {
        _groups = groups;
        InitializeComponent();
        ColorModsItemsControl.ItemsSource = colorMods;
        ColorGroupsItemsControl.ItemsSource = groups;
        TagPrefixCheckBox.IsChecked = tagPrefix;
        ApplyLocalization();
        SourceInitialized += (s, e) => ApplyDarkTitleBar();
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.Text("ColorEditorTitle");
        EditorTitleText.Text = LocalizationService.Text("ColorEditorTitle");
        EditorDescriptionText.Text = LocalizationService.Text("ColorEditorDescription");
        GroupsTitleText.Text = LocalizationService.Text("ColorGroupsTitle");
        TagPrefixCheckBox.Content = LocalizationService.Text("ColorTagPrefix");
        SaveConfigButton.Content = LocalizationService.Text("SaveConfig");
        LoadConfigButton.Content = LocalizationService.Text("LoadConfig");
        SaveButton.Content = LocalizationService.Text("Save");
        ExitButton.Content = LocalizationService.Text("Cancel");
    }

    private void ApplyDarkTitleBar()
    {
        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            IntPtr hwnd = hwndSource.Handle;

            // Use DWMWA_USE_IMMERSIVE_DARK_MODE (20) for Windows 11 / Windows 10 build 19041+
            int attribute = 20;
            int useImmersiveDarkMode = 1;
            DwmSetWindowAttribute(hwnd, attribute, ref useImmersiveDarkMode, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_groups.Any(group => !System.Text.RegularExpressions.Regex.IsMatch(
            group.Hex ?? "", "^#[0-9a-fA-F]{6}$")))
        {
            MessageBox.Show(LocalizationService.Text("InvalidGroupColor"), LocalizationService.Text("Error"));
            return;
        }
        DialogResult = true;
        Close();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var colorMods = ColorModsItemsControl.ItemsSource as ObservableCollection<ColorModsViewModel>;
        if (colorMods == null)
        {
            MessageBox.Show(LocalizationService.Text("NoColorModsSave"), LocalizationService.Text("Error"));
            return;
        }
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "color_mods.json"
        };
        if (saveFileDialog.ShowDialog() == true)
        {
            var colorModsDict = new Dictionary<string, object>();
            foreach (var mod in colorMods)
            {
                colorModsDict[mod.Name] = new
                {
                    enabled = mod.IsSelected,
                    color = mod.SelectedColor,
                };
            }
            colorModsDict["__groups"] = _groups.ToDictionary(group => group.Name, group => group.Hex);
            var options = new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var json = System.Text.Json.JsonSerializer.Serialize(colorModsDict, options);
            System.IO.File.WriteAllText(saveFileDialog.FileName, json);
            MessageBox.Show(LocalizationService.Text("ColorModsSaved"), LocalizationService.Text("Success"));
        }
    }

    private void LoadConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var colorMods = ColorModsItemsControl.ItemsSource as ObservableCollection<ColorModsViewModel>;
        if (colorMods == null)
        {
            MessageBox.Show(LocalizationService.Text("NoColorModsLoad"), LocalizationService.Text("Error"));
            return;
        }
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
        };
        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var json = System.IO.File.ReadAllText(openFileDialog.FileName);
                var colorModsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>>>(json);
                if (colorModsDict != null)
                {
                    if (colorModsDict.TryGetValue("__groups", out var groupData))
                        foreach (var group in _groups)
                            if (groupData.TryGetValue(group.Name, out var value) &&
                                value.ValueKind == System.Text.Json.JsonValueKind.String &&
                                System.Text.RegularExpressions.Regex.IsMatch(value.GetString() ?? "", "^#[0-9a-fA-F]{6}$"))
                                group.Hex = value.GetString()!;
                    foreach (var mod in colorMods)
                    {
                        if (colorModsDict.TryGetValue(mod.Name, out var modData))
                        {
                            if (modData.TryGetValue("enabled", out var enabledObj) 
                                && (enabledObj.ValueKind == System.Text.Json.JsonValueKind.True || enabledObj.ValueKind == System.Text.Json.JsonValueKind.False))
                            {
                                mod.IsSelected = enabledObj.GetBoolean();
                            }
                            if (modData.TryGetValue("color", out var colorObj) 
                                && colorObj.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                mod.SelectedColor = ModColorGroups.Normalize(colorObj.GetString() ?? string.Empty);
                            }
                        }
                    }
                    MessageBox.Show(LocalizationService.Text("ColorModsLoaded"), LocalizationService.Text("Success"));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(LocalizationService.Format("ColorModsLoadError", ex.Message), LocalizationService.Text("Error"));
            }
        }
    }

    public static bool Show(ObservableCollection<ColorModsViewModel> colorMods,
        ObservableCollection<ColorGroupViewModel> groups, bool tagPrefix, out bool updatedTagPrefix)
    {
        var dialog = new ColorModsEditor(colorMods, groups, tagPrefix);

        if (Application.Current.MainWindow != null)
        {
            dialog.Owner = Application.Current.MainWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        bool saved = dialog.ShowDialog() == true;
        updatedTagPrefix = saved ? dialog.TagPrefixCheckBox.IsChecked == true : tagPrefix;
        return saved;
    }
}
