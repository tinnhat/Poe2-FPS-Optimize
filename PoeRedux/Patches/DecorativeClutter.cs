using LibBundle3.Nodes;
using PoeRedux.Services;
using System.IO;
using System.Text;
using System.Text.Json;

namespace PoeRedux.Patches;

/// <summary>
/// Repairs the terrain materials written by PoeRedux 1.1.22/1.1.23. A particle
/// material is not compatible with foliage meshes and can render as bright green.
/// Only byte-for-byte copies of that known bad donor are restored.
/// </summary>
public sealed class DecorativeClutter : IPatch
{
    public string Name => "Repair Green Foliage Materials";
    public object Description => "Restores foliage materials damaged by the old clutter-removal option while leaving every other FPS patch intact. This requires the existing PoeRedux backup; otherwise use PackCheck or game-file verification.";

    private static readonly string[] BadMaterialDonors =
    {
        "metadata/effects/spells/monsters_effects/act1_four/bitterguy_boss/epk/invisible_epk_pass0.mat",
        "metadata/effects/spells/monsters_effects/league_3_9_0/alchemylab/invisible_tank_epk_pass0.mat"
    };

    public void Apply(DirectoryNode root)
    {
        var terrain = NavigateTo(root, "art", "models", "terrain") ??
            throw new InvalidDataException("Could not find PoE 2 terrain materials.");

        var donors = FindBadMaterialDonors(root);
        if (donors.Count == 0)
            throw new InvalidDataException("The known bad foliage-material donor was not found; no files were changed.");

        var brokenFiles = new List<FileNode>();
        CollectBrokenMaterials(terrain, donors, brokenFiles);
        if (brokenFiles.Count == 0) return;

        var repairs = new List<(FileNode File, byte[] Original)>(brokenFiles.Count);
        var missingBackups = new List<string>();

        // Resolve and validate all originals before the first write, preventing a
        // missing/corrupt backup from leaving a partially repaired GGPK.
        foreach (var file in brokenFiles)
        {
            string path = file.Record.Path ?? string.Empty;
            if (!BackupManager.TryReadOriginal(path, out byte[] original))
            {
                missingBackups.Add(path);
                continue;
            }

            string originalText = DecodeMaterial(original);
            ValidateMaterialJson(originalText, path);
            if (donors.Any(donor => original.AsSpan().SequenceEqual(donor)))
            {
                missingBackups.Add(path);
                continue;
            }

            repairs.Add((file, original));
        }

        if (missingBackups.Count != 0)
        {
            throw new InvalidDataException(
                $"Cannot safely repair {missingBackups.Count} foliage material(s) because their clean originals are missing from the PoeRedux backup. Run PackCheck or game-file verification before applying patches again.");
        }

        foreach (var repair in repairs)
            repair.File.Record.Write(repair.Original);
    }

    internal static bool IsAffectedTerrainPath(string path)
    {
        path = Normalize(path);
        return path.StartsWith("art/models/terrain/", StringComparison.Ordinal) &&
               path.EndsWith(".mat", StringComparison.Ordinal) &&
               (path.Contains("/foliage/", StringComparison.Ordinal) ||
                path.Contains("/vegetation/", StringComparison.Ordinal) ||
                path.Contains("/doodads/", StringComparison.Ordinal));
    }

    private static void CollectBrokenMaterials(
        DirectoryNode directory,
        IReadOnlyList<byte[]> donors,
        List<FileNode> brokenFiles)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode subdirectory)
            {
                CollectBrokenMaterials(subdirectory, donors, brokenFiles);
                continue;
            }

            if (node is not FileNode file || !IsAffectedTerrainPath(file.Record.Path ?? string.Empty))
                continue;

            byte[] current = file.Record.Read().ToArray();
            if (donors.Any(donor => current.AsSpan().SequenceEqual(donor)))
                brokenFiles.Add(file);
        }
    }

    private static List<byte[]> FindBadMaterialDonors(DirectoryNode root)
    {
        var donors = new List<byte[]>();
        foreach (string donorPath in BadMaterialDonors)
        {
            string[] parts = donorPath.Split('/');
            var directory = NavigateTo(root, parts[..^1]);
            var file = directory?.Children.OfType<FileNode>()
                .FirstOrDefault(item => item.Name.Equals(parts[^1], StringComparison.OrdinalIgnoreCase));
            if (file is null) continue;

            byte[] bytes = file.Record.Read().ToArray();
            string text = DecodeMaterial(bytes);
            ValidateMaterialJson(text, file.Record.Path ?? file.Name);
            if (text.Contains("Invisible.fxgraph", StringComparison.OrdinalIgnoreCase) &&
                !donors.Any(existing => existing.AsSpan().SequenceEqual(bytes)))
            {
                donors.Add(bytes);
            }
        }

        return donors;
    }

    private static void ValidateMaterialJson(string data, string path)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(data);
            if (!document.RootElement.TryGetProperty("version", out var version) || version.GetInt32() != 4)
                throw new JsonException("unsupported material version");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"Material validation failed for {path}; no files were changed.", exception);
        }
    }

    private static string DecodeMaterial(byte[] bytes) =>
        Encoding.Unicode.GetString(bytes).TrimStart('\uFEFF', '\0');

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();

    private static DirectoryNode? NavigateTo(DirectoryNode root, params string[] path)
    {
        DirectoryNode current = root;
        foreach (string name in path)
        {
            var next = current.Children.OfType<DirectoryNode>()
                .FirstOrDefault(directory => directory.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (next is null) return null;
            current = next;
        }

        return current;
    }
}
