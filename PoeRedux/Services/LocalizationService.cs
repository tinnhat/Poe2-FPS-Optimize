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
        ["SelectionSummary"] = ("{0} of {1} selected", "Đã chọn {0} / {1}"),
        ["SelectAll"] = ("Select all", "Chọn tất cả"),
        ["Clear"] = ("Clear", "Bỏ chọn"),
        ["ModColors"] = ("Colors", "Màu mod"),
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
        ["UpdateAvailable"] = ("Update available: {0}", "Có bản cập nhật: {0}"),
        ["OpenUpdateError"] = ("Could not open the download page. Please visit the GitHub releases page manually.", "Không thể mở trang tải xuống. Vui lòng truy cập trang GitHub Releases theo cách thủ công."),
        ["ColorEditorTitle"] = ("Map mod colors", "Màu mod bản đồ"),
        ["ColorEditorDescription"] = ("Choose which map modifiers to highlight and assign their colors.", "Chọn các mod bản đồ cần làm nổi bật và màu tương ứng."),
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
        ["Reduce Monster Non-Combat FX (Safe)"] = "Giảm hiệu ứng phụ của quái (An toàn)",
        ["Reduce Player Skill Particles (Performance)"] = "Giảm particle skill người chơi (Hiệu năng)",
        ["Remove Residual Smoke (Safe)"] = "Xóa khói tồn dư (An toàn)",
        ["Remove MTX Particles & Trails (Safe)"] = "Xóa particle và trail MTX (An toàn)",
        ["Repair Green Foliage Materials"] = "Sửa material cây cối bị xanh",
        ["Corpse Patch"] = "Xóa xác quái",
        ["Color Mods Patch"] = "Tô màu mod",
        ["Monster HP Patch"] = "Luôn hiện HP quái",
        ["Atlas Fog Patch"] = "Xóa sương Atlas",
        ["Fog Patch (Safe)"] = "Xóa sương mù (An toàn)",
        ["Rain Patch"] = "Xóa mưa",
        ["Clouds Patch"] = "Xóa mây",
        ["Environment FX Patch"] = "Giảm hiệu ứng môi trường",
        ["Shadow Patch"] = "Xóa bóng đổ",
        ["Light Patch"] = "Giảm ánh sáng",
        ["Delirium Fog Patch"] = "Xóa sương Delirium",
    };

    private static readonly Dictionary<string, string> VietnamesePatchDescriptions = new(StringComparer.Ordinal)
    {
        ["Camera Patch"] = "Cho phép điều chỉnh độ xa camera mặc định.",
        ["Minimap Patch"] = "Mở toàn bộ minimap theo mặc định.",
        ["Reduce Monster Non-Combat FX (Safe)"] = "Xóa particle idle, ambient và bước chân của quái; giữ đòn đánh, projectile, ground effect, aura và cảnh báo nguy hiểm.",
        ["Reduce Player Skill Particles (Performance)"] = "Giảm mạnh particle và trail của skill, nhưng giữ projectile, vùng nguy hiểm, marker, minion, trap, mine, totem, boss và league mechanic.",
        ["Remove Residual Smoke (Safe)"] = "Xóa danh sách khói sau khi boss chết, trail skill vô hại và hiệu ứng triệu hồi; giữ poison cloud, ground effect, projectile, AoE marker và cảnh báo nguy hiểm.",
        ["Remove MTX Particles & Trails (Safe)"] = "Xóa particle và trail của các nhóm MTX phổ biến. Không xóa model MTX hoặc hiệu ứng gameplay.",
        ["Repair Green Foliage Materials"] = "Khôi phục material cây cối bị hỏng bởi tùy chọn xóa vật thể cũ mà không gỡ các patch FPS khác. Cần bản sao lưu hiện có.",
        ["Corpse Patch"] = "Ẩn xác của cả quái lớn và quái nhỏ nhưng vẫn giữ rendering hồi sinh.",
        ["Color Mods Patch"] = "Thay đổi màu hiển thị của các mod trong game.",
        ["Monster HP Patch"] = "Luôn hiển thị thanh HP của quái.",
        ["Atlas Fog Patch"] = "Xóa sương mù trên Atlas.",
        ["Fog Patch (Safe)"] = "Xóa screen-space fog và fog trang trí; giữ hiệu ứng gameplay của quái, trap, Ritual, strongbox và cơ chế khác.",
        ["Rain Patch"] = "Tắt hiệu ứng mưa mặc định.",
        ["Clouds Patch"] = "Tắt hiệu ứng mây mặc định.",
        ["Environment FX Patch"] = "Tắt attachment, spawner, mưa, mây và post-processing môi trường mà không sửa object hiệu ứng gameplay.",
        ["Shadow Patch"] = "Tắt hiệu ứng bóng đổ mặc định.",
        ["Light Patch"] = "Tắt hiệu ứng ánh sáng mặc định.",
        ["Delirium Fog Patch"] = "Xóa fog môi trường Delirium, khói khi chạm mirror, blur, shimmer và lớp haze; giữ object encounter và hiệu ứng chiến đấu không liên quan.",
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
