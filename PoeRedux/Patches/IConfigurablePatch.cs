namespace PoeRedux.Patches;

public sealed class PatchOption(string id, string label, bool isEnabled = true)
{
    public string Id { get; } = id;
    public string Label { get; } = label;
    public bool IsEnabled { get; set; } = isEnabled;
}

public interface IConfigurablePatch
{
    IList<PatchOption> Options { get; }
}
