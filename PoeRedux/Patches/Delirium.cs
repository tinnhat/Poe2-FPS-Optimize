using LibBundle3.Nodes;
using PoeRedux.Services;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

public sealed class Delirium : IPatch
{
    private const double ContactDensityFactor = 0.40;
    private const double PersistentDensityFactor = 0.50;
    private const double OverlayFactor = 0.40;
    private const double FogAlphaFactor = 0.40;
    private const double PreserveAtOrBelow = 4.0;

    private static readonly IReadOnlyDictionary<string, double> ObjectParticles =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["metadata/effects/spells/monsters_effects/league_delirium/deliriumobject/fx/object_open.pet"] = ContactDensityFactor,
            ["metadata/effects/spells/monsters_effects/league_delirium/deliriumobject/fx/object_burst.pet"] = ContactDensityFactor,
            ["metadata/effects/spells/monsters_effects/league_delirium/deliriumobject/fx/deliriumobject.pet"] = PersistentDensityFactor,
            ["metadata/effects/spells/monsters_effects/league_delirium/deliriumobject/fx/deliriumobject_journeyend.pet"] = PersistentDensityFactor,
        };

    public string Name => "Reduce Delirium Fog & Contact Effects";

    public object Description =>
        "Reduces Delirium fog, player haze, blur, shimmer, and the burst produced when a Delirium object opens. " +
        "Monster attacks, projectiles, ground markers, trails, lighting, animation, and sound remain enabled.";

    public void Apply(DirectoryNode root)
    {
        RejectLegacyEnvironmentPatch(root);

        var pending = new List<PendingWrite>();
        CollectObjectParticleWrites(root, pending);
        CollectPlayerOverlayWrites(root, pending);
        CollectObjectMaterialWrites(root, pending);
        CollectEnvironmentFogWrite(root, pending);

        if (pending.Count == 0) return;

        foreach (PendingWrite write in pending)
        {
            byte[] current = write.File.Record.Read().ToArray();
            if (current.AsSpan().SequenceEqual(write.Bytes)) continue;
            BackupManager.RecordOriginal(write.File.Record);
            write.File.Record.Write(write.Bytes);
        }
    }

    private static void CollectObjectParticleWrites(DirectoryNode root, ICollection<PendingWrite> pending)
    {
        DirectoryNode league = NavigateTo(root, "metadata", "effects", "spells", "monsters_effects", "league_delirium") ??
            throw new InvalidDataException("Could not find the PoE 2 Delirium effects directory.");

        var matched = new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);
        CollectExactFiles(league, ObjectParticles.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase), matched);
        if (matched.Count != ObjectParticles.Count)
        {
            string missing = string.Join(", ", ObjectParticles.Keys.Where(path => !matched.ContainsKey(path)));
            throw new InvalidDataException($"Could not find expected Delirium object particles: {missing}. No files were written.");
        }

        foreach ((string path, double factor) in ObjectParticles)
        {
            FileNode file = matched[path];
            SourceText source = ReadOriginalOrCurrent(file);
            RejectBlankParticle(path, source.Text);
            string reduced = MonsterEffectDensityReducer.ReduceDensity(
                source.Text, out int changed, factor, PreserveAtOrBelow);
            if (changed == 0)
                throw new InvalidDataException($"No supported density values were found in {path}; no files were written.");
            pending.Add(new PendingWrite(file, Encode(reduced, source.HasBom)));
        }
    }

    private static void CollectPlayerOverlayWrites(DirectoryNode root, ICollection<PendingWrite> pending)
    {
        DirectoryNode epks = NavigateTo(root, "metadata", "effects", "spells", "monsters_effects",
            "league_delirium", "deliriumobject", "epks") ??
            throw new InvalidDataException("Could not find the PoE 2 Delirium player overlay directory.");

        foreach (string name in new[] { "playeraffliction_epk_pass1.mat", "playeraffliction_epk_pass2.mat" })
        {
            FileNode file = epks.Children.OfType<FileNode>().FirstOrDefault(item =>
                item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ??
                throw new InvalidDataException($"Could not find Delirium overlay material {name}.");

            SourceText source = ReadOriginalOrCurrent(file);
            int found = 0;
            string reduced = ScaleMaterialScalar(source.Text, "AlbedoMulti", OverlayFactor, ref found);
            reduced = ScaleMaterialScalar(reduced, "Fog Haze Intensity", OverlayFactor, ref found);
            if (name.Contains("pass2", StringComparison.OrdinalIgnoreCase))
                reduced = ScaleMaterialScalar(reduced, "Blur Intensity", OverlayFactor, ref found);

            int expected = name.Contains("pass2", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
            if (found != expected)
                throw new InvalidDataException($"Expected {expected} overlay parameters in {name}, found {found}; no files were written.");
            if (reduced == source.Text)
                throw new InvalidDataException(
                    $"Delirium overlay intensities in {name} are already zero. Restore the game with PackCheck or Verify game files before applying this patch.");
            pending.Add(new PendingWrite(file, Encode(reduced, source.HasBom)));
        }
    }

    private static void CollectObjectMaterialWrites(DirectoryNode root, ICollection<PendingWrite> pending)
    {
        DirectoryNode materials = NavigateTo(root, "metadata", "effects", "spells", "monsters_effects",
            "league_delirium", "deliriumobject", "mats") ??
            throw new InvalidDataException("Could not find the PoE 2 Delirium object material directory.");

        var localWrites = new List<PendingWrite>();
        int matchedParameters = 0;
        Visit(materials, file =>
        {
            if (!file.Name.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) return;
            string path = NormalizePath(file.Record.Path);
            if (!IsDeliriumPostFxMaterialPath(path)) return;

            SourceText source = ReadOriginalOrCurrent(file);
            string reduced = ScaleDeliriumObjectMaterial(path, source.Text, OverlayFactor, out int found);
            matchedParameters += found;
            if (found > 0 && reduced != source.Text)
                localWrites.Add(new PendingWrite(file, Encode(reduced, source.HasBom)));
        });

        const int expectedParameters = 14;
        if (matchedParameters != expectedParameters)
            throw new InvalidDataException(
                $"Expected {expectedParameters} Delirium blur, shimmer, and smoke parameters, found {matchedParameters}; no files were written.");
        if (localWrites.Count == 0)
            throw new InvalidDataException(
                "Delirium object intensities are already zero. Restore the game with PackCheck or Verify game files before applying this patch.");
        foreach (PendingWrite write in localWrites) pending.Add(write);
    }

    private static void CollectEnvironmentFogWrite(DirectoryNode root, ICollection<PendingWrite> pending)
    {
        DirectoryNode materials = NavigateTo(root, "metadata", "effects", "environment", "delirium", "blood", "mat") ??
            throw new InvalidDataException("Could not find the PoE 2 Delirium environment fog materials.");
        FileNode fog = materials.Children.OfType<FileNode>().FirstOrDefault(file =>
            file.Name.Equals("fog.mat", StringComparison.OrdinalIgnoreCase)) ??
            throw new InvalidDataException("Could not find the PoE 2 Delirium fog material.");

        SourceText source = ReadOriginalOrCurrent(fog);
        int found = 0;
        string reduced = ScaleMaterialScalar(source.Text, "Alpha Multiply", FogAlphaFactor, ref found);
        if (found != 1)
            throw new InvalidDataException($"Expected one Delirium fog alpha parameter, found {found}; no files were written.");
        pending.Add(new PendingWrite(fog, Encode(reduced, source.HasBom)));
    }

    private static void RejectLegacyEnvironmentPatch(DirectoryNode root)
    {
        DirectoryNode settings = NavigateTo(root, "metadata", "environmentsettings") ??
            throw new InvalidDataException("Could not find the PoE 2 environment settings directory.");

        string? damagedPath = null;
        Visit(settings, file =>
        {
            if (damagedPath is not null || !file.Name.EndsWith(".env", StringComparison.OrdinalIgnoreCase)) return;
            string path = NormalizePath(file.Record.Path);
            string text = Encoding.Unicode.GetString(file.Record.Read().ToArray()).TrimStart('\uFEFF');
            bool delirium = path.Contains("delirium", StringComparison.OrdinalIgnoreCase) ||
                             text.Contains("/Delirium/", StringComparison.OrdinalIgnoreCase) ||
                             text.Contains("FourDelirium", StringComparison.OrdinalIgnoreCase);
            if (!delirium) return;
            if (HasLegacyEnvironmentMutation(text)) damagedPath = path;
        });

        if (damagedPath is not null)
            throw new InvalidDataException(
                $"Old destructive Delirium edits were detected in {damagedPath}. Restore the game with PackCheck or Verify game files before applying this patch.");
    }

    internal static bool HasLegacyEnvironmentMutation(string text) =>
        text.Contains("\"xog\"", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("\"xcreenspace_fog\"", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("\"xffect_spawner\"", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("\"xost_processing\"", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("\"xost_transform\"", StringComparison.OrdinalIgnoreCase);

    internal static string ScaleMaterialScalar(string data, string parameterName, double factor, ref int found)
    {
        string pattern = "(?s)(\\\"name\\\"\\s*:\\s*\\\"" + Regex.Escape(parameterName) +
                         "\\\"\\s*,\\s*\\\"parameters\\\"\\s*:\\s*\\[\\s*\\{\\s*" +
                         "\\\"value\\\"\\s*:\\s*)([-+0-9.eE]+)";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);
        Match match = regex.Match(data);
        if (!match.Success) return data;
        if (!double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            !double.IsFinite(value)) return data;

        found++;
        string formatted = (value * factor).ToString("0.0###############", CultureInfo.InvariantCulture);
        return string.Concat(data.AsSpan(0, match.Groups[2].Index), formatted,
            data.AsSpan(match.Groups[2].Index + match.Groups[2].Length));
    }

    internal static string ScaleDeliriumObjectMaterial(
        string path, string data, double factor, out int parametersFound)
    {
        parametersFound = 0;
        bool valuesChanged = false;
        JsonNode root = JsonNode.Parse(data) ??
            throw new InvalidDataException($"Invalid Delirium material JSON: {path}");
        if (root["graphinstances"] is not JsonArray graphs) return data;

        foreach (JsonNode? graphNode in graphs)
        {
            if (graphNode is not JsonObject graph || graph["custom_parameters"] is not JsonArray parameters)
                continue;
            foreach (JsonNode? parameterNode in parameters)
            {
                if (parameterNode is not JsonObject parameter) continue;
                string name = parameter["name"]?.GetValue<string>() ?? string.Empty;
                bool target = name.Equals("Blur Intensity", StringComparison.OrdinalIgnoreCase) ||
                              name.Equals("Shimmer Intensity", StringComparison.OrdinalIgnoreCase) ||
                              path.EndsWith("/mats/open/disperse_smoke_r.mat", StringComparison.OrdinalIgnoreCase) &&
                              name.Equals("- Intensity", StringComparison.OrdinalIgnoreCase);
                if (!target) continue;
                if (parameter["parameters"] is not JsonArray values || values.Count == 0)
                    throw new InvalidDataException($"Delirium material parameter {name} has no values: {path}");

                bool numeric = false;
                foreach (JsonNode? valueNode in values)
                    if (valueNode is JsonObject valueObject && valueObject["value"] is JsonNode value)
                        numeric |= ScaleNumericValue(valueObject, "value", value, factor, ref valuesChanged);
                if (!numeric)
                    throw new InvalidDataException($"Delirium material parameter {name} is not numeric: {path}");
                parametersFound++;
            }
        }

        return parametersFound == 0 || !valuesChanged ? data :
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool ScaleNumericValue(
        JsonObject owner, string propertyName, JsonNode value, double factor, ref bool valuesChanged)
    {
        if (value is JsonArray array)
        {
            bool numeric = false;
            for (int i = 0; i < array.Count; i++)
                if (array[i] is JsonValue item && item.TryGetValue<double>(out double number))
                {
                    double scaled = number * factor;
                    array[i] = scaled;
                    valuesChanged |= scaled != number;
                    numeric = true;
                }
            return numeric;
        }

        if (value is JsonValue scalar && scalar.TryGetValue<double>(out double scalarNumber))
        {
            double scaled = scalarNumber * factor;
            owner[propertyName] = scaled;
            valuesChanged |= scaled != scalarNumber;
            return true;
        }
        return false;
    }

    internal static bool IsDeliriumPostFxMaterialPath(string path) =>
        path.Contains("/deliriumobject/mats/open/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/deliriumobject/mats/object/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/deliriumobject/mats/object_journeyend/", StringComparison.OrdinalIgnoreCase);

    private static SourceText ReadOriginalOrCurrent(FileNode file)
    {
        byte[] current = file.Record.Read().ToArray();
        byte[] source = BackupManager.TryReadOriginal(file.Record.Path ?? string.Empty, out byte[] original)
            ? original : current;
        bool hasBom = source.AsSpan().StartsWith(Encoding.Unicode.Preamble);
        return new SourceText(Encoding.Unicode.GetString(source).TrimStart('\uFEFF'), hasBom);
    }

    private static void RejectBlankParticle(string path, string text)
    {
        if (text.Trim('\0', '\uFEFF', ' ', '\r', '\n', '\t') == "0")
            throw new InvalidDataException(
                $"Old destructive Delirium particle data was detected in {path}. Restore the game with PackCheck or Verify game files before applying this patch.");
    }

    private static byte[] Encode(string text, bool hasBom)
    {
        byte[] body = Encoding.Unicode.GetBytes(text);
        return hasBom ? [.. Encoding.Unicode.Preamble, .. body] : body;
    }

    private static void CollectExactFiles(
        DirectoryNode directory, IReadOnlySet<string> targets, IDictionary<string, FileNode> matched)
    {
        Visit(directory, file =>
        {
            string path = NormalizePath(file.Record.Path);
            if (targets.Contains(path)) matched[path] = file;
        });
    }

    private static void Visit(DirectoryNode directory, Action<FileNode> action)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode child) Visit(child, action);
            else if (node is FileNode file) action(file);
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

    private static string NormalizePath(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').TrimStart('/').ToLowerInvariant();

    private sealed record PendingWrite(FileNode File, byte[] Bytes);
    private sealed record SourceText(string Text, bool HasBom);
}
