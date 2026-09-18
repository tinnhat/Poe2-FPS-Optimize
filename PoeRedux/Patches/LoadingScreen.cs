using LibBundle3.Nodes;
using PoeRedux.Services;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

public sealed class LoadingScreen : IPatch
{
    public string Name => "Hide Loading Artwork";
    public object Description => "Hides loading artwork immediately, without changing mouse input or game rendering.";

    public void Apply(DirectoryNode root)
    {
        var metadata = root.Children.OfType<DirectoryNode>().FirstOrDefault(d => d.Name == "metadata");
        var ui = metadata?.Children.OfType<DirectoryNode>().FirstOrDefault(d => d.Name == "ui");
        var loading = ui?.Children.OfType<DirectoryNode>().FirstOrDefault(d => d.Name == "loadingstate")
            ?? throw new InvalidDataException("Could not find PoE 2 loading-state UI files.");

        var files = loading.Children.OfType<FileNode>()
            .Where(file => file.Name.EndsWith(".ui", StringComparison.OrdinalIgnoreCase)).ToList();
        if (files.Count == 0)
            throw new InvalidDataException("No loading-state UI files were found.");

        int matched = 0;
        bool alreadyPatched = false;
        foreach (var file in files)
        {
            var record = file.Record;
            string source = Encoding.Unicode.GetString(record.Read().ToArray()).TrimStart('\uFEFF');
            alreadyPatched |= source.Contains("\tvisible = false;", StringComparison.Ordinal);
            string updated = RewriteLoadingText(source);
            if (updated == source) continue;
            matched++;
            BackupManager.RecordOriginal(record);
            record.Write([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(updated)]);
        }
        if (matched == 0 && !alreadyPatched)
            throw new InvalidDataException("No supported loading artwork transition was found; no data was changed.");
    }

    internal static string RewriteLoadingText(string source) => Regex.Replace(source,
        "(?m)^(?<begin>[ \\t]*begin\\b[^\\r\\n]*)(?<newline>\\r?\\n)(?![ \\t]*visible[ \\t]*=[ \\t]*false;)",
        "${begin}${newline}\tvisible = false;${newline}");
}
