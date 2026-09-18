using LibBundle3.Nodes;
using PoeRedux.Services;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

public abstract class SelectiveFxReducer : IPatch
{
    public abstract string Name { get; }
    public abstract object Description { get; }
    protected abstract string[] RootPath { get; }
    protected abstract bool ShouldReduce(string normalizedPath);
    protected virtual bool HasSupportedExtension(string fileName) =>
        fileName.EndsWith(".pet", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".trl", StringComparison.OrdinalIgnoreCase);
    protected virtual byte[] GetReplacement(string fileName) =>
        [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("0")];

    public void ValidateTargets(DirectoryNode root)
    {
        var directory = NavigateTo(root, RootPath) ??
            throw new InvalidDataException($"Could not find {string.Join('/', RootPath)} in the selected game data.");
        if (!HasTarget(directory))
            throw new InvalidDataException($"{Name} found no supported particle or trail files; no files were changed.");
    }

    private bool HasTarget(DirectoryNode directory) => directory.Children.Any(node => node switch
    {
        DirectoryNode child => HasTarget(child),
        FileNode file => HasSupportedExtension(file.Name) &&
            ShouldReduce((file.Record.Path ?? string.Empty).Replace('\\', '/').ToLowerInvariant()),
        _ => false
    });

    public void Apply(DirectoryNode root)
    {
        var directory = NavigateTo(root, RootPath) ??
            throw new InvalidDataException($"Could not find {string.Join('/', RootPath)} in the selected game data.");

        int changed = 0;
        int matched = 0;
        Visit(directory, ref matched, ref changed);
        if (matched == 0)
            throw new InvalidDataException($"{Name} found no supported particle or trail files; the game data layout may have changed.");
    }

    private void Visit(DirectoryNode directory, ref int matched, ref int changed)
    {
        foreach (var child in directory.Children)
        {
            if (child is DirectoryNode subdirectory)
            {
                Visit(subdirectory, ref matched, ref changed);
                continue;
            }

            if (child is not FileNode file || !HasSupportedExtension(file.Name))
                continue;

            string path = (file.Record.Path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            if (!ShouldReduce(path))
                continue;

            matched++;
            var record = file.Record;
            string current = Encoding.Unicode.GetString(record.Read().ToArray());
            if (current.Trim('\0', '\uFEFF', ' ', '\r', '\n', '\t') == "0")
                continue;

            byte[] replacement = GetReplacement(file.Name);
            BackupManager.RecordOriginal(record);
            record.Write(replacement);
            changed++;
        }
    }

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

public sealed class MonsterAmbientFxReducer : SelectiveFxReducer
{
    public override string Name => "Reduce Monster Non-Combat FX (Safe)";
    public override object Description => "Removes PoE 2 monster idle, ambient, and footstep particles while preserving attacks, projectiles, ground effects, auras, and danger telegraphs.";
    protected override string[] RootPath => ["metadata", "effects", "spells", "monsters_effects"];

    protected override bool ShouldReduce(string path) =>
        path.Contains("idle", StringComparison.Ordinal) ||
        path.Contains("ambient", StringComparison.Ordinal) ||
        path.Contains("/footstep", StringComparison.Ordinal);
}

public sealed class SafePlayerSkillFxReducer : SelectiveFxReducer
{
    public override string Name => "Reduce Player Skill Particles (Performance)";
    public override object Description => "Aggressively removes player-skill particles and trails while preserving paths associated with projectiles, ground/area effects, danger telegraphs, indicators, minions, traps, mines, totems, bosses, and league mechanics. PoE 2 can share some skill assets with monsters.";
    protected override string[] RootPath => ["metadata", "effects", "spells"];

    private static readonly string[] ExcludedRoots =
    {
        "/monsters_effects/", "/ground_effects/", "/ground_effects_v2/", "/ground_effects_v3/",
        "/microtransactions/", "/environment_effects/", "/traps/", "/league_", "/leagues/",
        "/ritual", "/breach", "/abyss", "/delirium", "/boss", "/npc/", "/objects/",
        "/terrain/", "/hideout", "/sanctum/", "/ultimatum/"
    };

    private static readonly Regex ProtectedGameplayVisual = new(
        "(?:warning|telegraph|danger|ground|grd|area|aoe|zone|indicator|marker|target|reticle|radius|boundary|wall|beam|laser|projectile|proj|arrow|bolt|grenade|missile|orb|portal|checkpoint|waypoint|minion|summon|skeleton|spectre|reaper|companion|totem|mine|trap)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    protected override bool ShouldReduce(string path)
    {
        string extension = Path.GetExtension(path);
        return (extension.Equals(".pet", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".trl", StringComparison.OrdinalIgnoreCase)) &&
               path.StartsWith("metadata/effects/spells/", StringComparison.Ordinal) &&
               !ExcludedRoots.Any(path.Contains) &&
               !ProtectedGameplayVisual.IsMatch(path);
    }
}

public sealed class SafeMtxParticleReducer : SelectiveFxReducer
{
    public override string Name => "Remove MTX Particles & Trails (Safe)";
    public override object Description => "Removes particles and trails from common cosmetic MTX categories. It does not remove MTX models; skill and gameplay effects are untouched.";
    protected override string[] RootPath => ["metadata", "effects", "microtransactions"];

    private static readonly string[] CosmeticCategories =
    {
        "aura", "bodyarmours", "boots", "capes", "character_fx", "foot_prints",
        "gloves", "head", "pets", "weapon_effects"
    };

    protected override bool ShouldReduce(string path) => CosmeticCategories.Any(category =>
        path.Contains($"metadata/effects/microtransactions/{category}/", StringComparison.Ordinal));
}
