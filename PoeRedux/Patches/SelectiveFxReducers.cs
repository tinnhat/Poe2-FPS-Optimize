using LibBundle3.Nodes;
using PoeRedux.Services;
using System.IO;
using System.Text;

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
