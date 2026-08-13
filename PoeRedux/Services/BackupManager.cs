using LibBundle3.Records;
using System.IO;
using System.IO.Compression;

namespace PoeRedux.Services;

public static class BackupManager
{
    private static readonly object _lock = new();
    private static ZipArchive? _zip;
    private static HashSet<string>? _knownPaths;

    public static string GetBackupFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PoeRedux", "Backups");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "poe2.zip");
    }

    public static bool HasBackup() => File.Exists(GetBackupFilePath());

    public static int CountBackedUpFiles()
    {
        if (!HasBackup()) return 0;
        try
        {
            using var zip = ZipFile.OpenRead(GetBackupFilePath());
            return zip.Entries.Count(e => !e.FullName.EndsWith('/'));
        }
        catch { return 0; }
    }

    public static void Begin()
    {
        lock (_lock)
        {
            End();

            var path = GetBackupFilePath();
            _knownPaths = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                _zip = ZipFile.Open(path, ZipArchiveMode.Update);
                foreach (var entry in _zip.Entries)
                    _knownPaths.Add(entry.FullName);
            }
            catch
            {
                // Corrupt/unreadable archive: start fresh.
                _zip?.Dispose();
                _zip = null;
                try { File.Delete(path); } catch { /* best effort */ }
                _knownPaths.Clear();
                _zip = ZipFile.Open(path, ZipArchiveMode.Update);
            }
        }
    }

    public static void RecordOriginal(FileRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_lock)
        {
            if (_zip == null || _knownPaths == null)
                throw new InvalidOperationException("Backup archive is not open; refusing to modify game data.");

            var path = record.Path ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException("The game record has no path; refusing an unbacked modification.");
            if (!_knownPaths.Add(path)) return;

            var originalBytes = record.Read();

            var entry = _zip.CreateEntry(path, CompressionLevel.Optimal);
            using var es = entry.Open();
            es.Write(originalBytes.Span);
        }
    }

    public static bool TryReadOriginal(string recordPath, out byte[] originalBytes)
    {
        if (string.IsNullOrWhiteSpace(recordPath))
            throw new ArgumentException("A backup record path is required.", nameof(recordPath));

        lock (_lock)
        {
            if (_zip == null || _knownPaths == null)
                throw new InvalidOperationException("Backup archive is not open; refusing to restore game data.");

            var entry = _zip.Entries.FirstOrDefault(item =>
                item.FullName.Equals(recordPath, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                originalBytes = [];
                return false;
            }

            using Stream stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            originalBytes = memory.ToArray();
            return true;
        }
    }

    public static void End()
    {
        lock (_lock)
        {
            _zip?.Dispose();
            _zip = null;
            _knownPaths = null;
        }
    }

    public static int Restore(LibBundle3.Index index, Action<int, int>? progress = null)
    {
        var path = GetBackupFilePath();
        if (!File.Exists(path)) return 0;

        using var zip = ZipFile.OpenRead(path);
        var total = zip.Entries.Count;

        int done = 0;
        return LibBundle3.Index.Replace(index, zip.Entries, (record, path) =>
        {
            done++;
            progress?.Invoke(done, total);
            return false; // continue
        }, saveIndex: false);
    }

    public static void DeleteBackup()
    {
        var path = GetBackupFilePath();
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }
}
