using System.Globalization;
using System.IO;
using System.Text.Json;

namespace PoeRedux.Services;

public enum AppLanguage
{
    English,
    Vietnamese,
}

public static class LocalizationService
{
    private sealed record Settings(string Language);

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PoeRedux", "settings.json");

    private static readonly Dictionary<string, (string English, string Vietnamese)> Ui = new(StringComparer.Ordinal)
    {
        ["HeaderSubtitle"] = ("A safer, focused performance toolkit for Path of Exile 2", "Bộ công cụ tối ưu hiệu năng an toàn, tập trung cho Path of Exile 2"),
        ["GameBadge"] = ("PATH OF EXILE 2", "PATH OF EXILE 2"),
        ["GameDataTitle"] = ("File", "File game"),
        ["GameDataDescription"] = ("Select Content.ggpk or the bundle index file.", "Chọn Content.ggpk hoặc file bundle index."),
        ["BrowseFile"] = ("Browse", "Chọn file"),
        ["CameraTitle"] = ("Camera", "Camera"),
        ["CameraDescription"] = ("Used only when Camera Patch is selected.", "Chỉ được dùng khi chọn Camera Patch."),
        ["RecoveryTitle"] = ("Recovery ready", "Sẵn sàng khôi phục"),
        ["RecoveryDescription"] = ("Original entries are backed up before the first write. Close the game before applying patches.", "Dữ liệu gốc được sao lưu trước lần ghi đầu tiên. Hãy đóng game trước khi áp dụng patch."),
        ["DeleteBackup"] = ("Delete backup", "Xóa backup"),
        ["PatchesTitle"] = ("Patches", "Patch"),
        ["ReadabilityCategory"] = ("Readability", "Dễ đọc"),
        ["MapCameraCategory"] = ("Map & Camera", "Bản đồ và camera"),
        ["VisualNoiseCategory"] = ("Visual Noise", "Giảm nhiễu hình ảnh"),
        ["SelectionSummary"] = ("{0} of {1} selected", "Đã chọn {0} / {1}"),
        ["SelectAll"] = ("All", "Tất cả"),
        ["Clear"] = ("None", "Bỏ chọn"),
        ["ModColors"] = ("Colors", "Màu"),
        ["Status"] = ("STATUS", "TRẠNG THÁI"),
        ["RestoreOriginal"] = ("Restore", "Khôi phục"),
        ["RestoreOriginalCount"] = ("Restore ({0})", "Khôi phục ({0})"),
        ["ApplySelected"] = ("Apply patches", "Áp dụng patch"),
        ["SelectGgpkTitle"] = ("Select GGPK or index file", "Chọn file GGPK hoặc index"),
        ["GgpkFilter"] = ("GGPK files (*.ggpk;*.bin)|*.ggpk;*.bin|All files (*.*)|*.*", "File GGPK (*.ggpk;*.bin)|*.ggpk;*.bin|Tất cả file (*.*)|*.*"),
        ["NoPatchesMessage"] = ("Select at least one patch to apply.", "Hãy chọn ít nhất một patch để áp dụng."),
        ["NoPatchesTitle"] = ("No patches selected", "Chưa chọn patch"),
        ["InvalidGgpkMessage"] = ("Select a valid GGPK file first.", "Hãy chọn một file GGPK hợp lệ trước."),
        ["InvalidGameMessage"] = ("Select a valid game file first.", "Hãy chọn một file game hợp lệ trước."),
        ["InvalidFileTitle"] = ("Invalid file", "File không hợp lệ"),
        ["StartingPatch"] = ("Preparing to apply patches…", "Đang chuẩn bị áp dụng patch…"),
        ["ApplySuccess"] = ("Successfully applied {0} patch(es).", "Đã áp dụng thành công {0} patch."),
        ["Success"] = ("Success", "Thành công"),
        ["ApplyErrorStatus"] = ("An error occurred while applying patches.", "Đã xảy ra lỗi khi áp dụng patch."),
        ["ApplyErrorMessage"] = ("Error applying patches:\n\n{0}", "Lỗi khi áp dụng patch:\n\n{0}"),
        ["Error"] = ("Error", "Lỗi"),
        ["NoBackupRestore"] = ("No backup was found for this game.", "Không tìm thấy bản sao lưu cho game này."),
        ["NothingToRestore"] = ("Nothing to restore", "Không có gì để khôi phục"),
        ["RestoringFiles"] = ("Restoring {0} file(s)…", "Đang khôi phục {0} file…"),
        ["RestoringProgress"] = ("Restoring ({0}/{1})", "Đang khôi phục ({0}/{1})"),
        ["RestoredStatus"] = ("Restored {0} file(s) to their original state.", "Đã khôi phục {0} file về trạng thái gốc."),
        ["RestoredMessage"] = ("Restored {0} file(s) to their original state.\n\nThe backup was cleared.", "Đã khôi phục {0} file về trạng thái gốc.\n\nBản sao lưu đã được xóa."),
        ["RestoreComplete"] = ("Restore complete", "Khôi phục hoàn tất"),
        ["RestoreErrorStatus"] = ("An error occurred while restoring files.", "Đã xảy ra lỗi khi khôi phục file."),
        ["RestoreErrorMessage"] = ("Error restoring files:\n\n{0}", "Lỗi khi khôi phục file:\n\n{0}"),
        ["NoBackupDelete"] = ("No backup was found for this game.", "Không tìm thấy bản sao lưu cho game này."),
        ["NothingToDelete"] = ("Nothing to delete", "Không có gì để xóa"),
        ["BackupDeleted"] = ("Backup deleted.", "Đã xóa bản sao lưu."),
        ["ApplyingProgress"] = ("Applying {0} ({1}/{2})…", "Đang áp dụng {0} ({1}/{2})…"),
        ["SelectFileBegin"] = ("Select a game file to begin.", "Chọn file game để bắt đầu."),
        ["Ready"] = ("Ready — {0}", "Sẵn sàng — {0}"),
        ["BackupReviewStatus"] = ("Backup exists — restore before testing new visual settings.", "Đã có backup — hãy khôi phục trước khi thử tùy chọn hình ảnh mới."),
        ["UpdateAvailable"] = ("Update available: {0}", "Có bản cập nhật: {0}"),
        ["OpenUpdateError"] = ("Could not open the download page. Please visit the GitHub releases page manually.", "Không thể mở trang tải xuống. Vui lòng truy cập trang GitHub Releases theo cách thủ công."),
        ["ColorEditorTitle"] = ("Map mod colors", "Màu mod bản đồ"),
        ["ColorEditorDescription"] = ("Choose which map modifiers to highlight and assign their colors.", "Chọn các mod bản đồ cần làm nổi bật và màu tương ứng."),
        ["ColorGroupsTitle"] = ("Color groups (#RRGGBB)", "Nhóm màu (#RRGGBB)"),
        ["ColorTagPrefix"] = ("Color a short group tag instead of the whole mod line", "Tô màu nhãn nhóm ngắn thay vì cả dòng mod"),
        ["InvalidGroupColor"] = ("Each group color must be in #RRGGBB format.", "Màu nhóm phải có dạng #RRGGBB."),
        ["ChoosePatchOption"] = ("Select at least one effect in this patch.", "Hãy chọn ít nhất một hiệu ứng cho patch này."),
        ["Option_vignette"] = ("Vignette / darkened edges", "Vignette / viền màn hình tối"),
        ["Option_dof"] = ("Depth-of-field blur", "Làm mờ độ sâu"),
        ["Option_rain"] = ("Rain", "Mưa"),
        ["Option_clouds"] = ("Clouds", "Mây"),
        ["SaveConfig"] = ("Save config", "Lưu cấu hình"),
        ["LoadConfig"] = ("Load config", "Tải cấu hình"),
        ["Save"] = ("Save", "Lưu"),
        ["Cancel"] = ("Cancel", "Hủy"),
        ["NoColorModsSave"] = ("There are no color mods to save.", "Không có mod màu nào để lưu."),
        ["NoColorModsLoad"] = ("There are no color mods to load.", "Không có mod màu nào để tải."),
        ["ColorModsSaved"] = ("Color mod configuration saved.", "Đã lưu cấu hình màu mod."),
        ["ColorModsLoaded"] = ("Color mod configuration loaded.", "Đã tải cấu hình màu mod."),
        ["ColorModsLoadError"] = ("Failed to load color mods: {0}", "Không thể tải màu mod: {0}"),
    };

    private static readonly Dictionary<string, string> VietnamesePatchNames = new(StringComparer.Ordinal)
    {
        ["Camera Patch"] = "Mở rộng camera",
        ["Minimap Patch"] = "Mở toàn bộ minimap",
        ["Reduce Monster Effect Density"] = "Giảm mật độ hiệu ứng quái",
        ["Reduce Player Skill Effect Density"] = "Giảm mật độ hiệu ứng skill người chơi",
        ["Reduce Delirium Fog & Contact Effects"] = "Giảm khói và hiệu ứng chạm Delirium",
        ["Color Mods Patch"] = "Tô màu mod",
        ["Compact Mod Lines"] = "Rút gọn dòng mod",
        ["Monster HP Patch"] = "Luôn hiện HP quái",
    };

    private static readonly Dictionary<string, string> VietnamesePatchDescriptions = new(StringComparer.Ordinal)
    {
        ["Camera Patch"] = "Cho phép điều chỉnh độ xa camera mặc định.",
        ["Minimap Patch"] = "Mở toàn bộ minimap theo mặc định.",
        ["Reduce Monster Effect Density"] = "Giảm mật độ particle trong hiệu ứng quái nhưng luôn giữ từng emitter hiển thị. Không sửa timing skill, projectile, trail, ground effect, AoE marker, model hoặc block render.",
        ["Reduce Player Skill Effect Density"] = "Chỉ giảm các lớp particle dày trong những thư mục skill người chơi đã kiểm tra. Giữ hiệu ứng chính, projectile, trail, impact, marker mặt đất, timing, animation và âm thanh.",
        ["Reduce Delirium Fog & Contact Effects"] = "Giảm khói, lớp haze trên nhân vật, blur, shimmer và hiệu ứng bùng lên khi mở Delirium. Giữ đòn đánh quái, projectile, marker mặt đất, trail, ánh sáng, animation và âm thanh.",
        ["Color Mods Patch"] = "Thay đổi màu hiển thị của các mod trong game.",
        ["Compact Mod Lines"] = "Rút gọn các dòng Prefix, Suffix và Crafted trên tooltip vật phẩm.",
        ["Monster HP Patch"] = "Luôn hiển thị thanh HP của quái.",
    };

    public static AppLanguage CurrentLanguage { get; private set; } = LoadLanguage();

    public static string Text(string key) => Ui.TryGetValue(key, out var value)
        ? CurrentLanguage == AppLanguage.Vietnamese ? value.Vietnamese : value.English
        : key;

    public static string Format(string key, params object[] values) => string.Format(Text(key), values);

    public static string PatchName(string englishName) =>
        CurrentLanguage == AppLanguage.Vietnamese && VietnamesePatchNames.TryGetValue(englishName, out string? translated)
            ? translated : englishName;

    public static string PatchDescription(string englishName, string englishDescription) =>
        CurrentLanguage == AppLanguage.Vietnamese && VietnamesePatchDescriptions.TryGetValue(englishName, out string? translated)
            ? translated : englishDescription;

    public static void SetLanguage(AppLanguage language)
    {
        if (CurrentLanguage == language) return;
        CurrentLanguage = language;
        SaveLanguage();
    }

    private static AppLanguage LoadLanguage()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                Settings? settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
                if (settings?.Language.Equals("vi", StringComparison.OrdinalIgnoreCase) == true)
                    return AppLanguage.Vietnamese;
                if (settings?.Language.Equals("en", StringComparison.OrdinalIgnoreCase) == true)
                    return AppLanguage.English;
            }
        }
        catch
        {
            // A damaged preference file must never prevent the patcher from starting.
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Vietnamese : AppLanguage.English;
    }

    private static void SaveLanguage()
    {
        try
        {
            string? directory = Path.GetDirectoryName(SettingsPath);
            if (directory is not null) Directory.CreateDirectory(directory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(
                new Settings(CurrentLanguage == AppLanguage.Vietnamese ? "vi" : "en")));
        }
        catch
        {
            // Language still changes for the current session if persistence is unavailable.
        }
    }
}
