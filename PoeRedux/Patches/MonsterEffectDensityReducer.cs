using LibBundle3.Nodes;
using PoeRedux.Services;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

public sealed partial class MonsterEffectDensityReducer : IPatch
{
    private const double DensityFactor = 0.50;

    private static readonly string[][] MonsterParticleRoots =
    {
        ["metadata", "effects", "spells", "monsters_effects"],
        ["metadata", "particles", "monster_effects"],
    };

    public string Name => "Reduce Monster Effect Density";

    public object Description =>
        "Reduces particle density in monster effects while keeping every emitter visible. " +
        "Skill timing, projectiles, trails, ground effects, AoE markers, models, and render blocks are preserved.";

    public void Apply(DirectoryNode root)
    {
        int particleFiles = 0;
        int densityValues = 0;

        foreach (string[] path in MonsterParticleRoots)
        {
            DirectoryNode? directory = NavigateTo(root, path);
            if (directory is not null)
                Visit(directory, ref particleFiles, ref densityValues);
        }

        if (particleFiles == 0)
            throw new InvalidDataException("No PoE 2 monster particle files were found; the game data layout may have changed.");
        if (densityValues == 0)
            throw new InvalidDataException("Monster particle files were found, but no supported density values were present; no files were changed.");
    }

    private static void Visit(DirectoryNode directory, ref int particleFiles, ref int densityValues)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode child)
            {
                Visit(child, ref particleFiles, ref densityValues);
                continue;
            }

            // Trails communicate movement and projectile direction, so they are deliberately untouched.
            if (node is not FileNode file || !file.Name.EndsWith(".pet", StringComparison.OrdinalIgnoreCase))
                continue;

            particleFiles++;
            var record = file.Record;
            byte[] currentBytes = record.Read().ToArray();
            byte[] sourceBytes = BackupManager.TryReadOriginal(record.Path ?? string.Empty, out byte[] originalBytes)
                ? originalBytes
                : currentBytes;

            bool hasBom = sourceBytes.AsSpan().StartsWith(Encoding.Unicode.Preamble);
            string source = Encoding.Unicode.GetString(sourceBytes).TrimStart('\uFEFF');
            string reduced = ReduceDensity(source, out int valuesChanged);
            densityValues += valuesChanged;
            if (valuesChanged == 0) continue;

            byte[] body = Encoding.Unicode.GetBytes(reduced);
            byte[] replacement = hasBom ? [.. Encoding.Unicode.Preamble, .. body] : body;
            if (currentBytes.AsSpan().SequenceEqual(replacement)) continue;

            BackupManager.RecordOriginal(record);
            record.Write(replacement);
        }
    }

    internal static string ReduceDensity(
        string source,
        out int valuesChanged,
        double densityFactor = DensityFactor,
        double reduceAbove = 1.0)
    {
        valuesChanged = 0;
        if (string.IsNullOrWhiteSpace(source) || source.Trim() == "0") return source;

        int fixedChanges = 0;
        string result = FixedCountRegex().Replace(source, match =>
            ReplaceNumber(match, 2, ref fixedChanges, densityFactor, reduceAbove));
        valuesChanged = fixedChanges;

        int searchFrom = 0;
        const string property = "\"particles_per_second\"";
        while (true)
        {
            int propertyStart = result.IndexOf(property, searchFrom, StringComparison.Ordinal);
            if (propertyStart < 0) break;
            int objectStart = result.IndexOf('{', propertyStart + property.Length);
            if (objectStart < 0) break;
            int objectEnd = FindMatchingBrace(result, objectStart);
            if (objectEnd < 0) break;

            string block = result.Substring(objectStart, objectEnd - objectStart + 1);
            int blockChanges = 0;
            string reducedBlock = RateValueRegex().Replace(block, match =>
                ReplaceNumber(match, 2, ref blockChanges, densityFactor, reduceAbove));
            valuesChanged += blockChanges;

            if (blockChanges > 0)
            {
                result = string.Concat(result.AsSpan(0, objectStart), reducedBlock,
                    result.AsSpan(objectEnd + 1));
                searchFrom = objectStart + reducedBlock.Length;
            }
            else
            {
                searchFrom = objectEnd + 1;
            }
        }

        return result;
    }

    private static string ReplaceNumber(
        Match match,
        int valueGroup,
        ref int valuesChanged,
        double densityFactor,
        double reduceAbove)
    {
        string raw = match.Groups[valueGroup].Value;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            !double.IsFinite(value) || value <= reduceAbove)
            return match.Value;

        double reduced = Math.Max(1.0, Math.Ceiling(value * densityFactor));
        if (reduced >= value) return match.Value;

        valuesChanged++;
        string formatted = reduced.ToString("0.################", CultureInfo.InvariantCulture);
        int relativeStart = match.Groups[valueGroup].Index - match.Index;
        return string.Concat(match.Value.AsSpan(0, relativeStart), formatted,
            match.Value.AsSpan(relativeStart + raw.Length));
    }

    private static int FindMatchingBrace(string text, int openingBrace)
    {
        int depth = 0;
        bool inString = false;
        bool escaped = false;
        for (int i = openingBrace; i < text.Length; i++)
        {
            char c = text[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static DirectoryNode? NavigateTo(DirectoryNode root, IEnumerable<string> path)
    {
        DirectoryNode current = root;
        foreach (string name in path)
        {
            DirectoryNode? next = current.Children.OfType<DirectoryNode>()
                .FirstOrDefault(child => child.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (next is null) return null;
            current = next;
        }
        return current;
    }

    [GeneratedRegex("(\\\"particles_count(?:_min|_max)?\\\"\\s*:\\s*)(-?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:[eE][+-]?\\d+)?)")]
    private static partial Regex FixedCountRegex();

    [GeneratedRegex("(\\\"value\\\"\\s*:\\s*)(-?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:[eE][+-]?\\d+)?)")]
    private static partial Regex RateValueRegex();
}
