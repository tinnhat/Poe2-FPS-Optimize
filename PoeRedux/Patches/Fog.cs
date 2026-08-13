using LibBundle3.Nodes;
using PoeRedux.Services;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

/// <summary>
/// Removes environment fog without blanking AO/AOC/EPK files. Screen-space fog
/// is detached at the environment-setting reference and clearly decorative fog
/// PET/TRL assets are disabled only in non-gameplay environment directories.
/// </summary>
public sealed class Fog : IPatch
{
    public string Name => "Fog Patch (Safe)";
    public object Description => "Removes screen-space and decorative environment fog while preserving monster, trap, Ritual, strongbox, and other gameplay effects.";

    private static readonly Regex PlayerEnvironmentAo = new(
        "(?<prefix>\\\"player_environment_ao\\\"\\s*:\\s*)\\\"(?<path>[^\\\"]*)\\\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FogPath = new(
        "(?:fog|mist|smoke|cloud)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public void Apply(DirectoryNode root)
    {
        var environmentSettings = NavigateTo(root, "metadata", "environmentsettings") ??
            throw new InvalidDataException("Could not find PoE 2 environment settings.");

        var aoLookup = new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);
        IndexAnimatedObjects(root, aoLookup);

        int envCandidates = 0;
        int particleCandidates = 0;
        int changed = 0;

        PatchEnvironmentFiles(environmentSettings, aoLookup, ref envCandidates, ref changed);
        PatchDecorativeFogParticles(root, ref particleCandidates, ref changed);

        if (envCandidates == 0 && particleCandidates == 0)
            throw new InvalidDataException("No supported PoE 2 fog data was found; the game data layout may have changed.");
    }

    private static void PatchEnvironmentFiles(
        DirectoryNode directory,
        IReadOnlyDictionary<string, FileNode> aoLookup,
        ref int candidates,
        ref int changed)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode subdirectory)
            {
                PatchEnvironmentFiles(subdirectory, aoLookup, ref candidates, ref changed);
                continue;
            }

            if (node is not FileNode file || !file.Name.EndsWith(".env", StringComparison.OrdinalIgnoreCase))
                continue;

            string data = Read(file);
            string patched = PatchEnvironmentText(data, aoLookup, ref candidates);
            if (patched == data) continue;

            Write(file, patched);
            changed++;
        }
    }

    internal static string PatchEnvironmentText(
        string data,
        IReadOnlyDictionary<string, FileNode> aoLookup,
        ref int candidates)
    {
        candidates += Regex.Matches(data,
            "(?m)\\\"(?:fog|screenspace_fog)\\\"\\s*:",
            RegexOptions.IgnoreCase).Count;

        string patched = data
            .Replace("\"fog\"", "\"xog\"")
            .Replace("\"screenspace_fog\"", "\"xcreenspace_fog\"");

        int localCandidates = 0;
        patched = PlayerEnvironmentAo.Replace(patched, match =>
        {
            string reference = Normalize(match.Groups["path"].Value);
            if (reference.Length == 0 || !IsFogAttachment(reference, aoLookup))
                return match.Value;

            localCandidates++;
            return match.Groups["prefix"].Value + "\"\"";
        });
        candidates += localCandidates;

        return patched;
    }

    private static bool IsFogAttachment(string reference, IReadOnlyDictionary<string, FileNode> aoLookup)
    {
        if (FogPath.IsMatch(reference)) return true;
        if (!aoLookup.TryGetValue(reference, out var file)) return false;

        string content = Read(file);
        return content.Contains("screenSpaceWeatherEffect.fmt", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("ScreenspaceFog", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(content, "(?:fog|mist|cloud)[^\\\"\\r\\n]*\\.(?:mat|pet|trl)", RegexOptions.IgnoreCase);
    }

    private static void PatchDecorativeFogParticles(DirectoryNode directory, ref int candidates, ref int changed)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode subdirectory)
            {
                PatchDecorativeFogParticles(subdirectory, ref candidates, ref changed);
                continue;
            }

            if (node is not FileNode file ||
                !(file.Name.EndsWith(".pet", StringComparison.OrdinalIgnoreCase) ||
                  file.Name.EndsWith(".trl", StringComparison.OrdinalIgnoreCase)))
                continue;

            string path = Normalize(file.Record.Path ?? string.Empty);
            if (!IsSafeDecorativeFogParticle(path)) continue;

            candidates++;
            if (IsZero(Read(file))) continue;

            Write(file, "0");
            changed++;
        }
    }

    internal static bool IsSafeDecorativeFogParticle(string path)
    {
        path = Normalize(path);
        if (!FogPath.IsMatch(path)) return false;

        // These directories can contain damage zones, traps, encounter state,
        // monster attacks, or other necessary gameplay telegraphs.
        string[] excluded =
        {
            "/monsters_effects/", "/ground_effects/", "/traps/", "/rituals/",
            "/strongboxes/", "/league_", "/leagues/", "/boss", "/microtransactions/",
            "/ascendancy", "/shrine", "/encounter/", "/mechanics/"
        };
        if (excluded.Any(path.Contains)) return false;

        return path.StartsWith("metadata/effects/weather_attachments/", StringComparison.Ordinal) ||
               Regex.IsMatch(path, "^metadata/effects/environment/act[0-9]", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(path, "^metadata/effects/environment/act[0-9]+_four/", RegexOptions.IgnoreCase) ||
               path.StartsWith("metadata/particles/doodads/", StringComparison.Ordinal) ||
               path.StartsWith("metadata/terrain/doodads/", StringComparison.Ordinal);
    }

    private static void IndexAnimatedObjects(DirectoryNode directory, Dictionary<string, FileNode> lookup)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode subdirectory)
                IndexAnimatedObjects(subdirectory, lookup);
            else if (node is FileNode file &&
                     (file.Name.EndsWith(".ao", StringComparison.OrdinalIgnoreCase) ||
                      file.Name.EndsWith(".aoc", StringComparison.OrdinalIgnoreCase)))
                lookup[Normalize(file.Record.Path ?? string.Empty)] = file;
        }
    }

    private static string Read(FileNode file) =>
        Encoding.Unicode.GetString(file.Record.Read().ToArray()).TrimStart('\uFEFF');

    private static bool IsZero(string data) =>
        data.Trim('\0', '\uFEFF', ' ', '\r', '\n', '\t') == "0";

    private static void Write(FileNode file, string data)
    {
        byte[] bytes = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(data)];
        BackupManager.RecordOriginal(file.Record);
        file.Record.Write(bytes);
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();

    private static DirectoryNode? NavigateTo(DirectoryNode root, params string[] path)
    {
        DirectoryNode current = root;
        foreach (string name in path)
        {
            var next = current.Children.OfType<DirectoryNode>()
                .FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (next is null) return null;
            current = next;
        }
        return current;
    }
}
