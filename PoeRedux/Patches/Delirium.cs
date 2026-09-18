using LibBundle3.Nodes;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PoeRedux.Services;

namespace PoeRedux.Patches;

public class Delirium : IPatch
{
    public string Name => "Delirium Fog Patch";
    public object Description => "Removes Delirium fog, mirror-activation smoke, blur, shimmer, and player haze while preserving encounter objects and unrelated combat effects.";

    private static readonly HashSet<string> MirrorFogParticlePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "metadata/effects/spells/monsters_effects/league_delirium/fog_origin/fx/fog_start_01/line.pet",
        "metadata/effects/spells/monsters_effects/league_delirium/tangmazu/fx/gigamirror_01/spread_fog_01/fx_fwdline.pet",
    };

    private List<FileNode> fileNodes = [];

    private readonly string[] extensions = {
        ".ao",
        ".aoc",
        ".pet",
        ".trl",
    };

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

    private void TryPatchFile(FileNode file)
    {
        var record = file.Record;
        string path = (record.Path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
        if (!(path.Contains("fog") || path.Contains("mist") || path.Contains("smoke") ||
              path.Contains("wisp") || path.Contains("cloud")))
            return;

        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());

        if (string.IsNullOrEmpty(data))
            return;

        string original = data;

        if (file.Name.EndsWith(".pet", StringComparison.OrdinalIgnoreCase) ||
            file.Name.EndsWith(".trl", StringComparison.OrdinalIgnoreCase))
        {
            data = "0";
        }
        else if (data.Contains("Metadata/FmtParent") && !data.Contains("AnimatedRender"))
        {
            data = "version 3\nextends \"Metadata/FmtParent\"";
        }
        else if (data.Contains("Metadata/FmtParent") && data.Contains("AnimatedRender"))
        {
            data = "version 3\nextends \"Metadata/FmtParent\"\n\nclient\n{\n\tAnimatedRender\n\t{\n\t\tcannot_be_disabled = true\n\t}\n}";
        }
        else if (data.Contains("Metadata/Parent"))
        {
            data = @"version 3
extends ""Metadata/Parent""

BaseAnimationEvents
{
}

AnimationController
{
	metadata = ""Art/Models/Effects/enviro_effects/weather_attachments/generic_rig/weather_rig.amd""
}

client
{
    ClientAnimationController
    {
        skeleton = ""Art/Models/Effects/enviro_effects/weather_attachments/generic_rig/weather_rig.ast""
    }

    BoneGroups
    {
        bone_group = ""box false aux_box1 aux_box2 aux_box3 ""
    }
}";
        }

        if (data == original)
            return;

        var newBytes = Encoding.Unicode.GetBytes(data);
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
    }

    private bool HasTargetExtension(string fileName) =>
        extensions.Any(ext =>
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
        fileNodes.Clear();
        var dir = NavigateTo(root, "metadata", "effects", "environment", "delirium");
        if (dir is not null)
            CollectFileNodesRecursively(dir);

        if (dir is null || fileNodes.Count == 0)
            throw new InvalidDataException("Could not find PoE 2 Delirium environment effects; the game data layout may have changed.");

        foreach (var file in fileNodes)
        {
            TryPatchFile(file);
        }

        // The map-wide Delirium haze in PoE 2 is a player render-pass effect,
        // not an environment fog object. Patch only the explicit haze parameter
        // in its two materials. The EPK itself is deliberately left intact because
        // malformed/blank effect packs can crash the client during scene setup.
        PatchPlayerDeliriumHazeMaterials(root);

        // The mirror uses separate spell particles and high-intensity material
        // post effects which do not live under the environment Delirium tree.
        // Keep the AO/EPK containers valid and neutralize only their visual
        // particle and material inputs.
        PatchMirrorFogParticles(root);
        PatchDeliriumObjectPostFxMaterials(root);

        var environmentSettings = NavigateTo(root, "metadata", "environmentsettings");
        if (environmentSettings is null)
            throw new InvalidDataException("Could not find PoE 2 environment settings.");
        PatchEnvironmentSettings(environmentSettings);
    }

    private static void PatchMirrorFogParticles(DirectoryNode root)
    {
        var leagueDirectory = NavigateTo(root, "metadata", "effects", "spells", "monsters_effects",
            "league_delirium") ??
            throw new InvalidDataException("Could not find the PoE 2 Delirium spell effects directory.");

        var matched = new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);
        CollectExactFiles(leagueDirectory, MirrorFogParticlePaths, matched);
        if (matched.Count != MirrorFogParticlePaths.Count)
        {
            string missing = string.Join(", ", MirrorFogParticlePaths.Where(path => !matched.ContainsKey(path)));
            throw new InvalidDataException($"Could not find the expected Delirium mirror fog particles: {missing}. No mirror particles were written.");
        }

        byte[] replacement = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("0")];
        foreach (var file in matched.Values)
        {
            string current = Encoding.Unicode.GetString(file.Record.Read().ToArray());
            if (current.Trim('\0', '\uFEFF', ' ', '\r', '\n', '\t') == "0") continue;
            BackupManager.RecordOriginal(file.Record);
            file.Record.Write(replacement);
        }
    }

    private static void CollectExactFiles(
        DirectoryNode directory,
        IReadOnlySet<string> targetPaths,
        IDictionary<string, FileNode> matched)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode subdirectory)
            {
                CollectExactFiles(subdirectory, targetPaths, matched);
                continue;
            }

            if (node is not FileNode file) continue;
            string path = NormalizePath(file.Record.Path);
            if (targetPaths.Contains(path)) matched[path] = file;
        }
    }

    private static void PatchDeliriumObjectPostFxMaterials(DirectoryNode root)
    {
        var materialsDirectory = NavigateTo(root, "metadata", "effects", "spells", "monsters_effects",
            "league_delirium", "deliriumobject", "mats") ??
            throw new InvalidDataException("Could not find the PoE 2 Delirium mirror materials directory.");

        var pendingWrites = new List<(FileNode File, string Patched)>();
        int matchedParameters = 0;
        CollectDeliriumMaterialWrites(materialsDirectory, pendingWrites, ref matchedParameters);

        const int expectedParameters = 14;
        if (matchedParameters != expectedParameters)
        {
            throw new InvalidDataException(
                $"Expected {expectedParameters} Delirium mirror post-effect parameters, found {matchedParameters}; no mirror materials were written.");
        }

        foreach (var pending in pendingWrites)
        {
            byte[] bytes = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(pending.Patched)];
            BackupManager.RecordOriginal(pending.File.Record);
            pending.File.Record.Write(bytes);
        }
    }

    private static void CollectDeliriumMaterialWrites(
        DirectoryNode directory,
        ICollection<(FileNode File, string Patched)> pendingWrites,
        ref int matchedParameters)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode subdirectory)
            {
                CollectDeliriumMaterialWrites(subdirectory, pendingWrites, ref matchedParameters);
                continue;
            }

            if (node is not FileNode file || !file.Name.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                continue;

            string path = NormalizePath(file.Record.Path);
            if (!IsDeliriumPostFxMaterialPath(path)) continue;

            string current = Encoding.Unicode.GetString(file.Record.Read().ToArray()).TrimStart('\uFEFF');
            string patched = PatchDeliriumObjectMaterial(path, current, out int found);
            matchedParameters += found;
            if (patched != current) pendingWrites.Add((file, patched));
        }
    }

    internal static bool IsDeliriumPostFxMaterialPath(string path) =>
        path.Contains("/deliriumobject/mats/open/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/deliriumobject/mats/object/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/deliriumobject/mats/object_journeyend/", StringComparison.OrdinalIgnoreCase);

    internal static string PatchDeliriumObjectMaterial(string path, string data, out int parametersFound)
    {
        parametersFound = 0;
        JsonNode root = JsonNode.Parse(data) ?? throw new InvalidDataException($"Invalid Delirium material JSON: {path}");
        JsonArray? graphs = root["graphinstances"] as JsonArray;
        if (graphs is null) return data;

        foreach (JsonNode? graphNode in graphs)
        {
            if (graphNode is not JsonObject graph || graph["custom_parameters"] is not JsonArray customParameters)
                continue;

            foreach (JsonNode? parameterNode in customParameters)
            {
                if (parameterNode is not JsonObject parameter) continue;
                string name = parameter["name"]?.GetValue<string>() ?? string.Empty;
                bool isPostFx = name.Equals("Blur Intensity", StringComparison.OrdinalIgnoreCase) ||
                                name.Equals("Shimmer Intensity", StringComparison.OrdinalIgnoreCase);
                bool isMirrorSmoke = path.EndsWith("/mats/open/disperse_smoke_r.mat", StringComparison.OrdinalIgnoreCase) &&
                                     name.Equals("- Intensity", StringComparison.OrdinalIgnoreCase);
                if (!isPostFx && !isMirrorSmoke) continue;

                if (parameter["parameters"] is not JsonArray values || values.Count == 0)
                    throw new InvalidDataException($"Delirium material parameter {name} has no values: {path}");

                bool foundNumericValue = false;
                foreach (JsonNode? valueNode in values)
                {
                    if (valueNode is not JsonObject valueObject || valueObject["value"] is not JsonNode value)
                        continue;
                    foundNumericValue |= ZeroNumericValue(valueObject, "value", value);
                }

                if (!foundNumericValue)
                    throw new InvalidDataException($"Delirium material parameter {name} is not numeric: {path}");
                parametersFound++;
            }
        }

        if (parametersFound == 0) return data;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool ZeroNumericValue(JsonObject owner, string propertyName, JsonNode value)
    {
        if (value is JsonArray array)
        {
            bool changed = false;
            for (int i = 0; i < array.Count; i++)
            {
                if (array[i] is JsonValue item && item.TryGetValue<double>(out _))
                {
                    array[i] = 0.0;
                    changed = true;
                }
            }
            return changed;
        }

        if (value is JsonValue scalar && scalar.TryGetValue<double>(out _))
        {
            owner[propertyName] = 0.0;
            return true;
        }

        return false;
    }

    private static string NormalizePath(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').TrimStart('/').ToLowerInvariant();

    private static void PatchPlayerDeliriumHazeMaterials(DirectoryNode root)
    {
        var epkDirectory = NavigateTo(root, "metadata", "effects", "spells", "monsters_effects",
            "league_delirium", "deliriumobject", "epks") ??
            throw new InvalidDataException("Could not find the PoE 2 Delirium player render-pass directory.");

        string[] names = ["playeraffliction_epk_pass1.mat", "playeraffliction_epk_pass2.mat"];

        var pendingWrites = new List<(FileNode File, string Patched)>();
        foreach (string name in names)
        {
            var file = epkDirectory.Children.OfType<FileNode>().FirstOrDefault(node =>
                node.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ??
                throw new InvalidDataException($"Could not find the PoE 2 Delirium haze material {name}.");

            string current = Encoding.Unicode.GetString(file.Record.Read().ToArray()).TrimStart('\uFEFF');
            int parametersFound = 0;
            string patched = SetMaterialScalar(current, "AlbedoMulti", 0.0, ref parametersFound);
            patched = SetMaterialScalar(patched, "Fog Haze Intensity", 0.0, ref parametersFound);
            if (name.Contains("pass2", StringComparison.OrdinalIgnoreCase))
                patched = SetMaterialScalar(patched, "Blur Intensity", 0.0, ref parametersFound);

            int expected = name.Contains("pass2", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
            if (parametersFound != expected)
                throw new InvalidDataException($"Expected {expected} Delirium overlay parameters in {name}, found {parametersFound}; no data was written.");
            if (patched != current)
                pendingWrites.Add((file, patched));
        }

        // Validate both materials before writing either one, preventing a layout
        // change in a future game patch from leaving a half-applied overlay patch.
        foreach (var pending in pendingWrites)
        {
            byte[] bytes = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(pending.Patched)];
            BackupManager.RecordOriginal(pending.File.Record);
            pending.File.Record.Write(bytes);
        }
    }

    internal static string SetMaterialScalar(string data, string parameterName, double value, ref int found)
    {
        string pattern = "(?s)(\\\"name\\\"\\s*:\\s*\\\"" + Regex.Escape(parameterName) +
                         "\\\"\\s*,\\s*\\\"parameters\\\"\\s*:\\s*\\[\\s*\\{\\s*" +
                         "\\\"value\\\"\\s*:\\s*)[-+0-9.eE]+";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);
        if (!regex.IsMatch(data)) return data;

        found++;
        string formatted = value.ToString("0.0###############", System.Globalization.CultureInfo.InvariantCulture);
        return regex.Replace(data, "${1}" + formatted, 1);
    }

    private static void PatchEnvironmentSettings(DirectoryNode directory)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode subdirectory)
            {
                PatchEnvironmentSettings(subdirectory);
                continue;
            }

            if (node is not FileNode file || !file.Name.EndsWith(".env", StringComparison.OrdinalIgnoreCase))
                continue;

            var record = file.Record;
            string path = (record.Path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            string data = Encoding.Unicode.GetString(record.Read().ToArray());
            bool isDelirium = path.Contains("delirium", StringComparison.Ordinal) ||
                              data.Contains("/Delirium/", StringComparison.OrdinalIgnoreCase) ||
                              data.Contains("FourDelirium", StringComparison.OrdinalIgnoreCase);
            if (!isDelirium) continue;

            string patched = PatchEnvironmentText(data);

            if (patched == data) continue;
            BackupManager.RecordOriginal(record);
            record.Write(Encoding.Unicode.GetBytes(patched));
        }
    }

    internal static string PatchEnvironmentText(string data)
    {
        string patched = data
                .Replace("\"fog\"", "\"xog\"")
                .Replace("\"screenspace_fog\"", "\"xcreenspace_fog\"")
                .Replace("\"effect_spawner\"", "\"xffect_spawner\"");

        // Detach only explicitly named fog objects. Generic environment AOs may
        // carry lighting and must remain connected.
        patched = Regex.Replace(patched,
            "(?i)(?<prefix>\"player_environment_ao\"\\s*:\\s*)\"(?<path>[^\"]*)\"",
            match => Regex.IsMatch(match.Groups["path"].Value, "fog|mist|smoke|delirium", RegexOptions.IgnoreCase)
                ? match.Groups["prefix"].Value + "\"\""
                : match.Value);

        return patched;
    }
}
