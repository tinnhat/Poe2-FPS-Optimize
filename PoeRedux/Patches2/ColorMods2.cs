using LibBundle3.Nodes;
using PoeRedux.Models;
using System.IO;
using System.Text;
using PoeRedux.Services;

namespace PoeRedux.Patches;

public class ColorMods2 : IPatch
{
    public string Name => "Color Mods Patch";
    public object Description => "Changes colors of mods in the game.";

    public List<ColorModsOption> ColorModsOptions { get; set; } = new()
    {
        // red
        new ColorModsOption("map_monsters_%_all_damage_to_gain_as_fire", "red", true),
        new ColorModsOption("map_monsters_%_physical_damage_to_gain_as_fire", "red", true),
        new ColorModsOption("map_monsters_%_all_damage_to_gain_as_cold", "red", true),
        new ColorModsOption("map_monsters_%_physical_damage_to_gain_as_cold", "red", true),
        new ColorModsOption("map_monsters_%_all_damage_to_gain_as_lightning", "red", true),
        new ColorModsOption("map_monsters_%_physical_damage_to_gain_as_lightning", "red", true),
        new ColorModsOption("map_monsters_%_all_damage_to_gain_as_chaos", "red", true),
        new ColorModsOption("map_monsters_%_physical_damage_to_gain_as_chaos", "red", true),
        new ColorModsOption("map_monsters_damage_+%", "red", true),
        new ColorModsOption("map_monster_damage_+%_final_from_deadly_atlas", "red", true),
        new ColorModsOption("map_monster_damage_+%_final_from_vital_atlas", "red", true),
        new ColorModsOption("map_monsters_attack_speed_+%", "red", true),
        new ColorModsOption("map_monsters_cast_speed_+%", "red", true),
        new ColorModsOption("map_monsters_movement_speed_+%", "red", true),
        new ColorModsOption("map_monsters_critical_strike_chance_+%", "red", true),
        new ColorModsOption("map_monsters_critical_strike_multiplier_+", "red", true),
        new ColorModsOption("map_monsters_chance_to_poison_on_hit_%", "red", true),
        new ColorModsOption("map_monsters_chance_to_inflict_bleeding_%", "red", true),
        new ColorModsOption("map_packs_fire_projectiles", "red", true),
        new ColorModsOption("map_monsters_penetrate_elemental_resistances_%", "red", true),
        new ColorModsOption("map_monsters_elemental_ailment_chance_+%", "red", true),
        new ColorModsOption("map_additional_player_maximum_resistances_%", "red", true),
        new ColorModsOption("map_player_life_and_es_recovery_speed_+%_final", "red", true),
        // green 
        new ColorModsOption("map_monsters_life_+%", "green", true),
        new ColorModsOption("monster_life_+%_final_from_map", "green", true),
        new ColorModsOption("map_heist_monster_life_+%_final_from_sextant", "green", true),
        new ColorModsOption("map_monsters_armour_break_physical_damage_%_dealt_as_armour_break", "green", true),
        new ColorModsOption("map_monsters_accuracy_rating_+%", "green", true),
        new ColorModsOption("map_monsters_hit_damage_stun_multiplier_+%", "green", true),
        new ColorModsOption("map_monsters_additional_fire_resistance", "green", true),
        new ColorModsOption("map_monsters_additional_cold_resistance", "green", true),
        new ColorModsOption("map_monsters_additional_lightning_resistance", "green", true),
        new ColorModsOption("map_monsters_additional_chaos_resistance", "green", true),
        new ColorModsOption("map_monsters_ailment_threshold_+%", "green", true),
        new ColorModsOption("map_monsters_stun_threshold_+%", "green", true),
        new ColorModsOption("map_base_ground_fire_damage_to_deal_per_10_seconds", "green", true),
        new ColorModsOption("map_base_ground_fire_damage_to_deal_per_minute", "green", true),
        new ColorModsOption("map_ground_ice_base_magnitude", "green", true),
        new ColorModsOption("map_ground_ice", "green", true),
        new ColorModsOption("map_ground_lightning", "green", true),
        new ColorModsOption("map_ground_lightning_base_magnitude", "green", true),
        new ColorModsOption("flask_charges_gained_+%", "green", true),
        new ColorModsOption("map_players_cannot_gain_flask_charges", "green", true),
        new ColorModsOption("map_player_cooldown_speed_+%_final", "green", true),
        new ColorModsOption("map_monsters_base_self_critical_strike_multiplier_-%", "green", true),
        new ColorModsOption("map_monsters_curse_effect_on_self_+%_final", "green", true),
        new ColorModsOption("map_monsters_curse_effect_+%", "green", true),
        // yellow
        new ColorModsOption("map_monster_potency_+%_final_from_map", "yellow", true),
        new ColorModsOption("map_monster_potency_+%", "yellow", true),
        new ColorModsOption("map_item_drop_rarity_+%_final_from_map", "yellow", true),
        new ColorModsOption("map_item_drop_rarity_+%", "yellow", true),
        new ColorModsOption("map_pack_size_+%_final_from_map", "yellow", true),
        new ColorModsOption("map_pack_size_+%", "yellow", true),
        new ColorModsOption("map_number_of_magic_and_rare_packs_+%_final_and_rare_monster_modifiers_chance_+%_final_from_map", "yellow", true),
        new ColorModsOption("map_number_of_magic_and_rare_packs_+%_and_rare_monster_modifiers_chance_%", "yellow", true),
        new ColorModsOption("map_number_of_magic_packs_+%", "yellow", true),
        new ColorModsOption("map_number_of_rare_packs_+%", "yellow", true),
        new ColorModsOption("map_rare_monster_modifiers_chance_%", "yellow", true),
        // white
        new ColorModsOption("map_map_item_drop_chance_+%_final_from_map", "blue", true),
        new ColorModsOption("map_map_item_drop_chance_+%", "blue", true),
    };

    public Dictionary<string, string> GroupColors { get; set; } =
        ModColorGroups.Defaults.ToDictionary(
            group => group.Name,
            group => $"rgb({group.R},{group.G},{group.B})", StringComparer.Ordinal);
    public Dictionary<string, string> GroupTags { get; set; } = ModColorGroups.DefaultTags();
    public bool TagPrefix { get; set; }

    private enum ReadState
    {
        WritingData,
        ReadingData,
        ReadingDescription,
        ReadingToDescription
    }

    private readonly string[] _extensions = {
        ".csd",
    };

    private List<FileNode> fileNodes = [];

    private void CollectFileNodesRecursively(DirectoryNode dir)
    {
        foreach (var node in dir.Children)
        {
            switch (node)
            {
                case DirectoryNode childDir:
                    CollectFileNodesRecursively(childDir);
                    break;

                case FileNode fileNode:
                    if (HasTargetExtension(fileNode.Name))
                        fileNodes.Add(fileNode);
                    break;
            }
        }
    }

    private bool TryPatchFile(FileNode file)
    {
        var record = file.Record;
        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());

        if (string.IsNullOrEmpty(data))
            return false;

        var lines = data.Split("\r\n").ToList();

        ReadState state = ReadState.ReadingToDescription;

        string? currentAnnotation = null;
        string currentTag = string.Empty;
        bool? currentIsEnabled = null;

        int linesToWrite = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];

            if (line.StartsWith("description"))
            {
                state = ReadState.ReadingDescription;
                continue;
            }

            if (state == ReadState.ReadingToDescription) continue;

            // Read the description on the next line.
            if (state == ReadState.ReadingDescription)
            {
                string[] description = line.Split(' ');
                if (description.Length < 2)
                {
                    state = ReadState.ReadingToDescription;
                    continue;
                }

                string modType = description[1];

                if (ColorModsOptions.FirstOrDefault(x => x.Name == modType) is ColorModsOption option
                    && GroupColors.TryGetValue(ModColorGroups.Normalize(option.Color), out string? annotation))
                {
                    currentAnnotation = annotation;
                    currentTag = GroupTags.TryGetValue(ModColorGroups.Normalize(option.Color), out var tag)
                        ? tag : ModColorGroups.Normalize(option.Color);
                    currentIsEnabled = option.IsEnabled;
                    state = ReadState.ReadingData;
                }
                else
                {
                    currentAnnotation = null;
                    state = ReadState.ReadingToDescription;
                }

                continue;
            }

            if (state == ReadState.ReadingData)
            {
                // Replace tabs in line with nothing.
                string firstNumber = line.Replace("\t", "").Split(' ')[0];

                // May be a "lang" value. If the value is a number write those lines.
                if (int.TryParse(firstNumber, out int value))
                {
                    state = ReadState.WritingData;
                    linesToWrite = value;
                    continue;
                }
                ;
            }

            if (state == ReadState.WritingData)
            {
                lines[i] = ModTextStyler.Restyle(
                    line, currentAnnotation, currentTag, currentIsEnabled == true, TagPrefix);

                linesToWrite--;
                if (linesToWrite == 0)
                {
                    state = ReadState.ReadingData;
                    continue;
                }
            }
        }

        var newData = string.Join("\r\n", lines);
        if (newData == data)
            return false;
        var newBytes = Encoding.Unicode.GetBytes(newData);
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
        return true;
    }

    private bool HasTargetExtension(string fileName) =>
        _extensions.Any(ext =>
            fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    private static DirectoryNode? NavigateTo(DirectoryNode root, params string[] path)
    {
        DirectoryNode current = root;
        foreach (var name in path)
        {
            var next = current.Children.OfType<DirectoryNode>().FirstOrDefault(d => d.Name == name);
            if (next is null) return null;
            current = next;
        }
        return current;
    }

    public void Apply(DirectoryNode root)
    {
        var dir = NavigateTo(root, "data", "statdescriptions");
        if (dir is null)
            throw new InvalidDataException("data/statdescriptions was not found in the game files.");
        fileNodes.Clear();
        CollectFileNodesRecursively(dir);

        int changed = 0;
        foreach (var file in fileNodes)
            if (TryPatchFile(file)) changed++;
        _ = changed; // Re-applying the same readability style is a valid no-op.
    }
}
