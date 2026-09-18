using LibBundle3.Nodes;
using PoeRedux.Services;
using System.IO;
using System.Text;

namespace PoeRedux.Patches;

/// <summary>Removes only decorative full-screen filters; exposure and gameplay cues remain intact.</summary>
public sealed class PostProcessFilters : IPatch, IConfigurablePatch
{
    private const string VignetteMarker = "// PoeRedux: vignette disabled";
    private const string DofMarker = "// PoeRedux: depth of field disabled";
    public string Name => "Reduce Decorative Screen Filters";
    public object Description => "Disables vignette and depth-of-field blur without changing exposure, tone mapping, bloom, or low-life and chill cues.";
    public IList<PatchOption> Options { get; } =
    [
        new("vignette", "Vignette / darkened edges"),
        new("dof", "Depth-of-field blur")
    ];

    public void Apply(DirectoryNode root)
    {
        var shaders = root.Children.OfType<DirectoryNode>()
            .FirstOrDefault(d => d.Name.Equals("shaders", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Could not find the PoE 2 shaders directory.");
        var file = shaders.Children.OfType<FileNode>()
            .FirstOrDefault(f => f.Name.Equals("postprocessuber.hlsl", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Could not find postprocessuber.hlsl; no screen filters were changed.");

        var record = file.Record;
        string source = Encoding.ASCII.GetString(record.Read().ToArray());
        bool vignette = Options.First(option => option.Id == "vignette").IsEnabled;
        bool dof = Options.First(option => option.Id == "dof").IsEnabled;
        if (!vignette && !dof)
            throw new InvalidOperationException("Select at least one screen filter.");
        bool previousVignette = source.Contains(VignetteMarker, StringComparison.Ordinal);
        bool previousDof = source.Contains(DofMarker, StringComparison.Ordinal);
        if ((!vignette && previousVignette) || (!dof && previousDof))
            throw new InvalidOperationException("Restore original game data before changing screen-filter choices.");
        if ((vignette && !previousVignette && !source.Contains("if( vignette_enable )", StringComparison.Ordinal)) ||
            (dof && !previousDof && !source.Contains("if ( dof_enable )", StringComparison.Ordinal)))
            throw new InvalidDataException("Expected screen-filter shader flags were not found; no shader was changed.");
        string updated = RewriteShader(source, vignette, dof);
        if (vignette && !previousVignette) updated += "\n" + VignetteMarker;
        if (dof && !previousDof) updated += "\n" + DofMarker;
        if (updated == source) return;
        updated += "\n";

        BackupManager.RecordOriginal(record);
        record.Write(Encoding.ASCII.GetBytes(updated));
    }

    internal static string RewriteShader(string source, bool vignette = true, bool dof = true)
    {
        if (vignette) source = source.Replace("if( vignette_enable )", "if( 0 )", StringComparison.Ordinal);
        if (dof) source = source.Replace("if ( dof_enable )", "if ( 0 )", StringComparison.Ordinal);
        return source;
    }
}
