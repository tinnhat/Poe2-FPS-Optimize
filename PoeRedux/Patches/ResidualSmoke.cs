using LibBundle3.Nodes;
using PoeRedux.Services;
using System.IO;
using System.Text;

namespace PoeRedux.Patches;

public sealed class ResidualSmoke : IPatch
{
    public string Name => "Remove Residual Smoke (Safe)";
    public object Description => "Removes an allowlisted set of boss-death smoke, harmless skill smoke trails, and summon residue. Poison clouds, ground effects, projectiles, AoE markers, and danger telegraphs are preserved.";

    private static readonly HashSet<string> SafeResidualParticlePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        // Boss-death aftermath: gameplay danger has already ended.
        "metadata/effects/spells/monsters_effects/act2_four/titanboss/pets/onacts/death/headsmoke.pet",
        "metadata/effects/spells/monsters_effects/act2_four/titanboss/pets/onacts/death/smoke_burst.pet",
        "metadata/effects/spells/monsters_effects/act2_four/titanboss/pets/onacts/death/smoke.pet",
        "metadata/effects/spells/monsters_effects/atlasofworldsbosses/arbiter_of_divinity/fx/emerge/death_wisps.trl",
        "metadata/effects/spells/monsters_effects/league_expedition/boss/reaper_boss/pet/on_act_death_smoke_drip.pet",
        "metadata/effects/spells/monsters_effects/league_expedition/boss/uhtred/fx/act/death_smoke_body.pet",
        "metadata/effects/spells/monsters_effects/league_expedition/boss/uhtred/fx/act/death_smoke_limbs.pet",

        // Secondary grenade trails; active clouds, impacts, and markers remain.
        "metadata/effects/spells/crossbow_explosivegrenade/fx/grenadesmoke.trl",
        "metadata/effects/spells/crossbow_oilgrenade/fx/grenadesmoke.trl",
        "metadata/effects/spells/crossbow_shock_grenade/trl/grenadesmoke.trl",
        "metadata/effects/spells/crossbow_stungrenade/fx/grenadesmoke.trl",

        // Summon/movement residue which does not communicate a hazardous area.
        "metadata/effects/spells/summoned_reaper/pet/dash_smoke.pet",
        "metadata/effects/spells/summoned_reaper/pet/wispy_line.pet",
        "metadata/effects/spells/undead_summon_skeletons/pet/revivewisps.pet",

        // Legacy secondary trails and impact aftermath.
        "metadata/particles/blast_rain/fall/trail_smoke.trl",
        "metadata/particles/grim_ward/corpse/smoke.pet",
        "metadata/particles/masters/elreon/unholy_smite/impact/smokecircle.pet",
        "metadata/particles/masters/keema/charge_explosion/smoke_aftermath.pet",
        "metadata/particles/puncture_arrow/smoke_trail.trl",
        "metadata/particles/shield_charge/trail_smoke.pet",
    };

    public void Apply(DirectoryNode root)
    {
        var matched = new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);
        Collect(root, matched);
        if (matched.Count == 0)
            throw new InvalidDataException("No known safe residual-smoke particles were found; the game data layout may have changed.");

        byte[] replacement = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("0")];
        foreach (FileNode file in matched.Values)
        {
            string current = Encoding.Unicode.GetString(file.Record.Read().ToArray());
            if (current.Trim('\0', '\uFEFF', ' ', '\r', '\n', '\t') == "0") continue;
            BackupManager.RecordOriginal(file.Record);
            file.Record.Write(replacement);
        }
    }

    private static void Collect(DirectoryNode directory, IDictionary<string, FileNode> matched)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode subdirectory)
            {
                Collect(subdirectory, matched);
                continue;
            }

            if (node is not FileNode file) continue;
            string path = NormalizePath(file.Record.Path);
            if (IsAllowlistedPath(path)) matched[path] = file;
        }
    }

    internal static bool IsAllowlistedPath(string path) => SafeResidualParticlePaths.Contains(NormalizePath(path));

    private static string NormalizePath(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').TrimStart('/').ToLowerInvariant();
}
