using LibBundle3.Nodes;
using PoeRedux.Services;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

public sealed class EnvironmentParticles2 : IPatch, IConfigurablePatch
{
    public string Name => "Weather FX Patch";
    public object Description => "Reduces rain and clouds while preserving environment effect spawners, lighting, fog attachments, and post-processing.";
    public IList<PatchOption> Options { get; } =
    [
        new("rain", "Rain"),
        new("clouds", "Clouds")
    ];

    public void Apply(DirectoryNode root)
    {
        var directory = NavigateTo(root, "metadata", "environmentsettings") ??
            throw new InvalidDataException("Could not find PoE 2 environment settings.");

        int candidates = 0;
        int changed = 0;
        bool rain = Options.First(option => option.Id == "rain").IsEnabled;
        bool clouds = Options.First(option => option.Id == "clouds").IsEnabled;
        if (!rain && !clouds)
            throw new InvalidOperationException("Select at least one weather effect.");
        PatchDirectory(directory, rain, clouds, ref candidates, ref changed);

        if (candidates == 0)
            throw new InvalidDataException("No supported PoE 2 environment settings were found; the game data layout may have changed.");
    }

    private static void PatchDirectory(DirectoryNode directory, bool rain, bool clouds, ref int candidates, ref int changed)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode subdirectory)
            {
                PatchDirectory(subdirectory, rain, clouds, ref candidates, ref changed);
                continue;
            }

            if (node is not FileNode file || !file.Name.EndsWith(".env", StringComparison.OrdinalIgnoreCase))
                continue;

            string data = Encoding.Unicode.GetString(file.Record.Read().ToArray()).TrimStart('\uFEFF');
            string patched = PatchEnvironmentText(data, ref candidates, rain, clouds);
            if (patched == data) continue;

            BackupManager.RecordOriginal(file.Record);
            byte[] bytes = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(patched)];
            file.Record.Write(bytes);
            changed++;
        }
    }

    internal static string PatchEnvironmentText(string data, ref int candidates, bool rain = true, bool clouds = true)
    {
        string patched = data;

        if (clouds) patched = SetNumericPropertyToZero(patched, "clouds_intensity", ref candidates);
        if (rain) patched = SetNumericPropertyToZero(patched, "rain_intensity", ref candidates);
        return patched;
    }

    private static string SetNumericPropertyToZero(string data, string property, ref int candidates)
    {
        string pattern = $"(?<prefix>\\\"{Regex.Escape(property)}\\\"\\s*:\\s*)(?<value>[-+0-9.eE]+)";
        int localCandidates = 0;
        string patched = Regex.Replace(data, pattern, match =>
        {
            localCandidates++;
            return match.Groups["prefix"].Value + "0.0";
        }, RegexOptions.IgnoreCase);
        candidates += localCandidates;
        return patched;
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
