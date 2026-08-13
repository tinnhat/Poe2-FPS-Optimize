using LibBundledGGPK3;
using Microsoft.Win32;
using PoeRedux.Models;
using PoeRedux.Patches;
using PoeRedux.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PoeRedux.Patches.Black;

namespace PoeRedux;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<PatchViewModel> _patches;
    private readonly ObservableCollection<ColorModsViewModel> _colorMods;
    private string _ggpkPath = string.Empty;
    private double _cameraZoom = 2.4;
    private string? _updateDownloadUrl;
    private string? _updateVersion;
    private bool _languageUiReady;

    public MainWindow()
    {
        _patches = new ObservableCollection<PatchViewModel>();
        _colorMods = new ObservableCollection<ColorModsViewModel>();
        InitializeComponent();
        PatchesItemsControl.ItemsSource = _patches;
        InitializePoe2Patches();
        LanguageComboBox.SelectedIndex = LocalizationService.CurrentLanguage == AppLanguage.Vietnamese ? 1 : 0;
        ApplyLocalization();
        _languageUiReady = true;

        SourceInitialized += (s, e) => ApplyDarkTitleBar();
        Loaded += async (s, e) =>
        {
            UpdateRestoreButtonState();
            await CheckForUpdatesAsync();
        };
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

    private void InitializePoe2Patches()
    {
        var patchInstances = new IPatch[]
        {
            new Camera(),
            new Minimap(),
            new MonsterAmbientFxReducer(),
            new SafePlayerSkillFxReducer(),
            new ResidualSmoke(),
            new SafeMtxParticleReducer(),
            new DecorativeClutter(),
            new Corpse(),
            new ColorMods2(),
            new MonsterHP(),
            new AtlasFog(),
            new Fog(),
            new Rain(),
            new Clouds(),
            new EnvironmentParticles2(),
            new Shadow(),
            new Light(),
            new Delirium(),
            // new Aoc(),
            // new Env(),
            // new Epk(),
            // new Ffx(),
            // new Hlsl(),
            // new Mat(),
            // new NoCorpse(),
            // new Pet(),
        };

        foreach (var patch in patchInstances)
        {
            var viewModel = new PatchViewModel(patch);
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(PatchViewModel.IsSelected)) UpdateSelectionSummary();
            };
            _patches.Add(viewModel);
            if (patch is ColorMods2 colorModsPatch)            {
                foreach (var option in colorModsPatch.ColorModsOptions)
                {
                    _colorMods.Add(new ColorModsViewModel(option.Copy()));
                }
            }
        }
    }

    private void LanguageComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_languageUiReady) return;
        AppLanguage language = LanguageComboBox.SelectedIndex == 1
            ? AppLanguage.Vietnamese : AppLanguage.English;
        LocalizationService.SetLanguage(language);
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        Title = $"PoeRedux — {LocalizationService.Text("HeaderSubtitle")}";
        HeaderSubtitleText.Text = LocalizationService.Text("HeaderSubtitle");
        GameBadgeText.Text = LocalizationService.Text("GameBadge");
        GameFileTitleText.Text = LocalizationService.Text("GameDataTitle");
        GameFileDescriptionText.Text = LocalizationService.Text("GameDataDescription");
        BrowseButton.Content = LocalizationService.Text("BrowseFile");
        CameraTitleText.Text = LocalizationService.Text("CameraTitle");
        CameraDescriptionText.Text = LocalizationService.Text("CameraDescription");
        SafetyTitleText.Text = LocalizationService.Text("RecoveryTitle");
        SafetyDescriptionText.Text = LocalizationService.Text("RecoveryDescription");
        DeleteBackupButton.Content = LocalizationService.Text("DeleteBackup");
        PatchesTitleText.Text = LocalizationService.Text("PatchesTitle");
        SelectAllButton.Content = LocalizationService.Text("SelectAll");
        SelectNoneButton.Content = LocalizationService.Text("Clear");
        ModsColorsButton.Content = LocalizationService.Text("ModColors");
        StatusLabelText.Text = LocalizationService.Text("Status");
        ApplyButton.Content = LocalizationService.Text("ApplySelected");
        foreach (PatchViewModel patch in _patches) patch.RefreshLanguage();
        UpdateSelectionSummary();
        UpdateRestoreButtonState();
        UpdateStatus();
        if (_updateVersion is not null)
            UpdateNotificationButton.Content = LocalizationService.Format("UpdateAvailable", _updateVersion);
    }

    private void UpdateSelectionSummary()
    {
        if (PatchesSummaryText is null) return;
        PatchesSummaryText.Text = LocalizationService.Format(
            "SelectionSummary", _patches.Count(patch => patch.IsSelected), _patches.Count);
    }

    private void ModsColorsButton_Click(object sender, RoutedEventArgs e)
    {
        var result = ColorModsEditor.Show(_colorMods);
        if (result == true)
        {
            foreach (var colorMod in _colorMods)
            {
                colorMod.Option.Color = colorMod.SelectedColor;
                colorMod.Option.IsEnabled = colorMod.IsSelected;
            }
        }
        else
        {
            foreach (var colorMod in _colorMods)
            {
                colorMod.IsSelected = colorMod.Option.IsEnabled;
                colorMod.SelectedColor = colorMod.Option.Color;
            }
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = LocalizationService.Text("GgpkFilter"),
            Title = LocalizationService.Text("SelectGgpkTitle")
        };

        if (openFileDialog.ShowDialog() == true)
        {
            GgpkPathTextBox.Text = openFileDialog.FileName;
        }
    }

    private void GgpkPathTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _ggpkPath = GgpkPathTextBox.Text.Trim();
        UpdateStatus();
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var patch in _patches)
        {
            patch.IsSelected = true;
        }
    }

    private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var patch in _patches)
        {
            patch.IsSelected = false;
        }
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ZoomValueText != null)
        {
            ZoomValueText.Text = e.NewValue.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "×";
        }
        _cameraZoom = e.NewValue;
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPatches = _patches.Where(p => p.IsSelected).ToList();

        foreach (var patch in selectedPatches)
        {
            if (patch.Patch is Camera cameraPatch)
            {
                cameraPatch.ZoomLevel = _cameraZoom;
            }
            if (patch.Patch is ColorMods colorModsPatch)
            {
                colorModsPatch.ColorModsOptions = _colorMods.Select(cm => cm.Option.Copy()).ToList();
            }
            if (patch.Patch is ColorMods2 colorModsPatch2)
            {
                colorModsPatch2.ColorModsOptions = _colorMods.Select(cm => cm.Option.Copy()).ToList();
            }
        }

        if (selectedPatches.Count == 0)
        {
            MessageBox.Show(LocalizationService.Text("NoPatchesMessage"), LocalizationService.Text("NoPatchesTitle"));
            return;
        }

        await ApplyPatches(selectedPatches);
    }

    private async Task ApplyPatches(List<PatchViewModel> patchesToApply)
    {
        if (string.IsNullOrEmpty(_ggpkPath) || !File.Exists(_ggpkPath))
        {
            MessageBox.Show(LocalizationService.Text("InvalidGgpkMessage"), LocalizationService.Text("InvalidFileTitle"));
            return;
        }

        StatusTextBlock.Text = LocalizationService.Text("StartingPatch");

        // Disable buttons during operation
        ApplyButton.IsEnabled = false;
        if (RestoreButton != null) RestoreButton.IsEnabled = false;
        //ZoomSlider.IsEnabled = false;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = patchesToApply.Count;
        ProgressBar.Value = 0;

        try
        {
            await Task.Run(() =>
            {
                BackupManager.Begin();
                try
                {
                    if (_ggpkPath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                    {
                        using var index = new LibBundle3.Index(_ggpkPath, false);
                        index.ParsePaths();
                        PatchIndex(index, patchesToApply);
                    }
                    else if (_ggpkPath.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase))
                    {
                        using BundledGGPK ggpk = new(_ggpkPath, false);
                        var index = ggpk.Index;
                        index.ParsePaths();
                        PatchIndex(index, patchesToApply);
                    }
                    else
                    {
                        throw new InvalidDataException("The selected file is neither a GGPK nor an index BIN file.");
                    }
                }
                finally
                {
                    BackupManager.End();
                }
            });

            StatusTextBlock.Text = LocalizationService.Format("ApplySuccess", patchesToApply.Count);
            MessageBox.Show(LocalizationService.Format("ApplySuccess", patchesToApply.Count), LocalizationService.Text("Success"));
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = LocalizationService.Text("ApplyErrorStatus");
            MessageBox.Show(LocalizationService.Format("ApplyErrorMessage", ex.Message), LocalizationService.Text("Error"));
        }
        finally
        {
            // Re-enable buttons
            ApplyButton.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressBar.Value = 0;
            //ZoomSlider.IsEnabled = true;
            UpdateRestoreButtonState();
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_ggpkPath) || !File.Exists(_ggpkPath))
        {
            MessageBox.Show(LocalizationService.Text("InvalidGameMessage"), LocalizationService.Text("InvalidFileTitle"));
            return;
        }

        if (!BackupManager.HasBackup())
        {
            MessageBox.Show(LocalizationService.Text("NoBackupRestore"), LocalizationService.Text("NothingToRestore"));
            return;
        }

        var fileCount = BackupManager.CountBackedUpFiles();
        StatusTextBlock.Text = LocalizationService.Format("RestoringFiles", fileCount);

        ApplyButton.IsEnabled = false;
        if (RestoreButton != null) RestoreButton.IsEnabled = false;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = fileCount;
        ProgressBar.Value = 0;

        int restored = 0;
        try
        {
            await Task.Run(() =>
            {
                void Progress(int done, int total)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        StatusTextBlock.Text = LocalizationService.Format("RestoringProgress", done, total);
                        ProgressBar.Value = done;
                    });
                }

                if (_ggpkPath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    using var index = new LibBundle3.Index(_ggpkPath, false);
                    index.ParsePaths();
                    restored = BackupManager.Restore(index, Progress);
                    index.Save();
                }
                else if (_ggpkPath.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase))
                {
                    using BundledGGPK ggpk = new(_ggpkPath, false);
                    var index = ggpk.Index;
                    index.ParsePaths();
                    restored = BackupManager.Restore(index, Progress);
                    index.Save();
                }
                else
                {
                    throw new InvalidDataException("The selected file is neither a GGPK nor an index BIN file.");
                }
            });

            BackupManager.DeleteBackup();
            StatusTextBlock.Text = LocalizationService.Format("RestoredStatus", restored);
            MessageBox.Show(LocalizationService.Format("RestoredMessage", restored), LocalizationService.Text("RestoreComplete"));
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = LocalizationService.Text("RestoreErrorStatus");
            MessageBox.Show(LocalizationService.Format("RestoreErrorMessage", ex.Message), LocalizationService.Text("Error"));
        }
        finally
        {
            ApplyButton.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressBar.Value = 0;
            UpdateRestoreButtonState();
        }
    }

    private void DeleteBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (!BackupManager.HasBackup())
        {
            MessageBox.Show(LocalizationService.Text("NoBackupDelete"), LocalizationService.Text("NothingToDelete"));
            UpdateRestoreButtonState();
            return;
        }

        BackupManager.DeleteBackup();
        StatusTextBlock.Text = LocalizationService.Text("BackupDeleted");
        UpdateRestoreButtonState();
    }

    private void UpdateRestoreButtonState()
    {
        if (RestoreButton == null) return;
        var count = BackupManager.CountBackedUpFiles();
        RestoreButton.IsEnabled = count > 0;
        RestoreButton.Content = count > 0
            ? LocalizationService.Format("RestoreOriginalCount", count)
            : LocalizationService.Text("RestoreOriginal");
        if (DeleteBackupButton != null) DeleteBackupButton.IsEnabled = count > 0;
    }

    private void PatchIndex(LibBundle3.Index index, List<PatchViewModel> patches)
    {
        var fileTree = index.BuildTree(true);

        for (int i = 0; i < patches.Count; i++)
        {
            var patch = patches[i];

            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = LocalizationService.Format("ApplyingProgress", patch.Name, i + 1, patches.Count);
                ProgressBar.Value = i;
            });

            patch.Patch.Apply(fileTree);
            index.Save();

            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = i + 1;
            });
        }
    }

    private void UpdateStatus()
    {
        if (string.IsNullOrEmpty(_ggpkPath))
        {
            StatusTextBlock.Text = LocalizationService.Text("SelectFileBegin");
        }
        else
        {
            StatusTextBlock.Text = LocalizationService.Format("Ready", Path.GetFileName(_ggpkPath));
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        const string githubOwner = "Gineticus";
        const string githubRepo = "PoeRedux";

        try
        {
            var updateInfo = await GitHubUpdateChecker.CheckForUpdatesAsync(githubOwner, githubRepo);

            if (updateInfo?.IsUpdateAvailable == true)
            {
                _updateDownloadUrl = updateInfo.DownloadUrl;
                _updateVersion = updateInfo.LatestVersion;
                UpdateNotificationButton.Content = LocalizationService.Format("UpdateAvailable", updateInfo.LatestVersion ?? string.Empty);
                UpdateNotificationButton.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            // Silently ignore update check failures
        }
    }

    private void UpdateNotificationButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_updateDownloadUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _updateDownloadUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show(LocalizationService.Text("OpenUpdateError"), LocalizationService.Text("Error"));
            }
        }
    }
}
