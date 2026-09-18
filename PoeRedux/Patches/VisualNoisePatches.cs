using LibBundle3.Nodes;

namespace PoeRedux.Patches;

public sealed class VisualNoiseParticles : IPatch, IConfigurablePatch
{
    public string Name => "Reduce Combat Particle Noise";
    public object Description => "Reduces monster idle and footstep particles plus secondary player skill particles; keeps projectiles, ground hazards, markers, and danger telegraphs.";
    public IList<PatchOption> Options { get; } =
    [
        new("monster", "Monster idle and footsteps"),
        new("player", "Secondary player skill particles")
    ];

    public void Apply(DirectoryNode root)
    {
        var monster = new MonsterAmbientFxReducer();
        var player = new SafePlayerSkillFxReducer();
        if (!Options.Any(option => option.IsEnabled))
            throw new InvalidOperationException("Select at least one particle category.");
        if (Options.First(option => option.Id == "monster").IsEnabled) monster.ValidateTargets(root);
        if (Options.First(option => option.Id == "player").IsEnabled) player.ValidateTargets(root);
        if (Options.First(option => option.Id == "monster").IsEnabled) monster.Apply(root);
        if (Options.First(option => option.Id == "player").IsEnabled) player.Apply(root);
    }
}

public sealed class VisualNoiseEffects : IPatch
{
    public string Name => "Reduce Residual Effect Noise";
    public object Description => "Removes selected harmless smoke and trails after deaths or skill use; keeps active combat effects and hazard cues.";

    public void Apply(DirectoryNode root) => new ResidualSmoke().Apply(root);
}
