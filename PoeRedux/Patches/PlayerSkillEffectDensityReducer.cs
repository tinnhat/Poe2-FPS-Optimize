using LibBundle3.Nodes;
using PoeRedux.Services;
using System.IO;
using System.Text;

namespace PoeRedux.Patches;

public sealed class PlayerSkillEffectDensityReducer : IPatch
{
    private const double DensityFactor = 0.50;
    private const double PreserveAtOrBelow = 4.0;

    // Audited PoE 2 player-skill roots. Shared status, environment, NPC, monster,
    // ground-effect, MTX, and legacy metadata/particles roots are deliberately absent.
    private static readonly string[] PlayerSkillPrefixes =
    {
        "assassin_", "aura_", "berserk_", "blood_", "bone_", "bow_",
        "chaos_", "cold_", "crossbow_", "druid_", "elementalbow_", "fire_",
        "flame_", "flask_throw", "herald_", "holy_", "infernalist_", "lightning_",
        "mace_", "mantra_", "minion_", "monk_", "necromancer_", "poison_",
        "ranger_", "reservation_", "sceptre_", "shield_", "shock_", "slam_",
        "spear_", "staff_", "storm_", "sword_", "undead_", "vaal_", "wand_",
        "warcr", "warrior_", "werewolf_", "witch_", "wyvern_",
    };

    private static readonly HashSet<string> PlayerSkillRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "absolution_blast", "ambush", "archon", "ascendancy_classes", "ascension",
        "base_arrow_attack", "caustic_arrow_02", "celestial_conviction", "charms",
        "chronomancer", "corpse_eruption", "crystal_bloom", "deadeye_tailwind",
        "elemental_mortars", "elemental_snap", "executioner_support", "exorcism",
        "feast_of_flesh", "frozen_sphere", "heavy_strike_02", "hydro_catalyst",
        "icetemplar_ringstatue", "maneuver_mechanics", "mercenary_crossbow",
        "metagem_triggers", "mount_rhoa", "offering_celestial", "oil_arrow", "orbs",
        "pain_suppression", "player_resources", "prismatic_rain", "rage_slash",
        "sands_of_silk_blink", "secret_blade", "serles_masterwork", "skill_charges",
        "skill_infusion", "soul_link", "spectral_hammer", "spectral_weapon_throw",
        "spell_surges", "spell_unleash", "sprint", "summoned_reaper",
        "sunprismblight", "totems", "towering_shadow", "trauma_strike",
        "triggered_volcanic_eruption", "vampiric_icon", "viper_strike", "void_burst",
    };

    private static readonly HashSet<string> ExcludedSharedOrMonsterRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "aura_generic",
        "storm_call_atziri",
    };

    public string Name => "Reduce Player Skill Effect Density";

    public object Description =>
        "Reduces only dense particle layers in audited player-skill folders. Sparse core visuals, " +
        "projectiles, trails, impacts, ground markers, timing, animation, and sound remain unchanged.";

    public void Apply(DirectoryNode root)
    {
        DirectoryNode spells = NavigateTo(root, "metadata", "effects", "spells") ??
            throw new InvalidDataException("Could not find metadata/effects/spells in the selected game data.");

        int particleFiles = 0;
        int densityValues = 0;
        foreach (DirectoryNode skillRoot in spells.Children.OfType<DirectoryNode>())
        {
            if (!IsPlayerSkillRoot(skillRoot.Name)) continue;
            Visit(skillRoot, ref particleFiles, ref densityValues);
        }

        if (particleFiles == 0)
            throw new InvalidDataException("No audited PoE 2 player-skill particle files were found; the game data layout may have changed.");
        if (densityValues == 0)
            throw new InvalidDataException("Player-skill particles were found, but no dense particle layers needed reduction.");
    }

    internal static bool IsPlayerSkillRoot(string name) =>
        !name.Contains("monster", StringComparison.OrdinalIgnoreCase) &&
        !ExcludedSharedOrMonsterRoots.Contains(name) &&
        (PlayerSkillRoots.Contains(name) ||
         PlayerSkillPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

    private static void Visit(DirectoryNode directory, ref int particleFiles, ref int densityValues)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode child)
            {
                Visit(child, ref particleFiles, ref densityValues);
                continue;
            }

            // Trail geometry supplies motion and projectile direction and must remain exact.
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
            string reduced = MonsterEffectDensityReducer.ReduceDensity(
                source, out int valuesChanged, DensityFactor, PreserveAtOrBelow);
            densityValues += valuesChanged;
            if (valuesChanged == 0) continue;

            byte[] body = Encoding.Unicode.GetBytes(reduced);
            byte[] replacement = hasBom ? [.. Encoding.Unicode.Preamble, .. body] : body;
            if (currentBytes.AsSpan().SequenceEqual(replacement)) continue;

            BackupManager.RecordOriginal(record);
            record.Write(replacement);
        }
    }

    private static DirectoryNode? NavigateTo(DirectoryNode root, params string[] path)
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
}
