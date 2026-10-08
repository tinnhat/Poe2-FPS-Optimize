using PoeRedux.Patches;
using PoeRedux.Services;
using System.Text;
using Xunit;

namespace PoeRedux.Tests;

public class VisualSafetyTests
{
    [Fact]
    public void MonsterDensity_ReducesCountsButKeepsEveryEmitterVisible()
    {
        const string source = "version 5\n0\n{\"particles_count\":10,\"particles_count_min\":4,\"particles_count_max\":9,\"particle_duration_min\":3}";
        string result = MonsterEffectDensityReducer.ReduceDensity(source, out int changed);
        Assert.Contains("\"particles_count\":5", result);
        Assert.Contains("\"particles_count_min\":2", result);
        Assert.Contains("\"particles_count_max\":5", result);
        Assert.Contains("\"particle_duration_min\":3", result);
        Assert.Equal(3, changed);
    }

    [Fact]
    public void MonsterDensity_ReducesPpsWithoutChangingTimingOrMode()
    {
        const string source = "{\"particles_per_second\":{\"variance\":6.0,\"points\":[{\"time\":0.0,\"value\":18.0,\"mode\":\"Linear\"},{\"time\":1.0,\"value\":1.0,\"mode\":\"Linear\"}]},\"render_material\":\"danger.mat\"}";
        string result = MonsterEffectDensityReducer.ReduceDensity(source, out int changed);
        Assert.Contains("\"variance\":6.0", result);
        Assert.Contains("\"value\":9", result);
        Assert.Contains("\"value\":1.0", result);
        Assert.Contains("\"time\":1.0", result);
        Assert.Contains("\"mode\":\"Linear\"", result);
        Assert.Contains("\"render_material\":\"danger.mat\"", result);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void MonsterDensity_NeverTurnsAVisibleParticleIntoZero()
    {
        const string source = "{\"particles_count\":1,\"particles_count_min\":2,\"particles_count_max\":3,\"particles_per_second\":{\"variance\":0.0,\"points\":[{\"time\":0.0,\"value\":2.0}]}}";
        string result = MonsterEffectDensityReducer.ReduceDensity(source, out _);
        Assert.Contains("\"particles_count\":1", result);
        Assert.Contains("\"particles_count_min\":1", result);
        Assert.Contains("\"particles_count_max\":2", result);
        Assert.Contains("\"value\":1", result);
        Assert.DoesNotContain("\"particles_count\":0", result);
    }

    [Fact]
    public void PlayerSkillDensity_PreservesSparseCoreLayers()
    {
        const string source = "{\"particles_count\":4,\"particles_count_min\":5,\"particles_count_max\":20,\"particles_per_second\":{\"variance\":2.0,\"points\":[{\"time\":0.0,\"value\":4.0},{\"time\":1.0,\"value\":30.0}]}}";
        string result = MonsterEffectDensityReducer.ReduceDensity(source, out int changed, 0.50, 4.0);
        Assert.Contains("\"particles_count\":4", result);
        Assert.Contains("\"particles_count_min\":3", result);
        Assert.Contains("\"particles_count_max\":10", result);
        Assert.Contains("\"variance\":2.0", result);
        Assert.Contains("\"value\":4.0", result);
        Assert.Contains("\"value\":15", result);
        Assert.Equal(3, changed);
    }

    [Theory]
    [InlineData("bow_lightning_arrow", true)]
    [InlineData("monk_lightningstrike", true)]
    [InlineData("reservation_archmage", true)]
    [InlineData("player_resources", true)]
    [InlineData("monsters_effects", false)]
    [InlineData("environment_effects", false)]
    [InlineData("ground_effects_v3", false)]
    [InlineData("status_ailments", false)]
    [InlineData("npc", false)]
    [InlineData("storm_call_atziri", false)]
    [InlineData("player_monster_shared", false)]
    public void PlayerSkillRoots_RejectSharedAndMonsterFolders(string root, bool expected)
    {
        Assert.Equal(expected, PlayerSkillEffectDensityReducer.IsPlayerSkillRoot(root));
    }

    [Fact]
    public void DeliriumOverlay_ScalesIntensityWithoutRemovingRenderParameters()
    {
        const string source = "{\"name\":\"Fog Haze Intensity\",\"parameters\":[{\"value\":1.25}],\"render_pass\":\"Append\"}";
        int found = 0;
        string result = Delirium.ScaleMaterialScalar(source, "Fog Haze Intensity", 0.4, ref found);
        Assert.Contains("\"value\":0.5", result);
        Assert.Contains("\"render_pass\":\"Append\"", result);
        Assert.Equal(1, found);
    }

    [Fact]
    public void DeliriumObjectMaterial_ScalesBlurAndKeepsOtherValues()
    {
        const string path = "metadata/effects/spells/monsters_effects/league_delirium/deliriumobject/mats/open/blur.mat";
        const string source = "{\"graphinstances\":[{\"custom_parameters\":[{\"name\":\"Blur Intensity\",\"parameters\":[{\"value\":2.0}]},{\"name\":\"Colour\",\"parameters\":[{\"value\":[1.0,0.5,0.25]}]}]}]}";
        string result = Delirium.ScaleDeliriumObjectMaterial(path, source, 0.4, out int found);
        Assert.Contains("\"value\": 0.8", result);
        Assert.Contains("1.0", result);
        Assert.Contains("0.5", result);
        Assert.Equal(1, found);
    }

    [Fact]
    public void DeliriumObjectMaterial_DoesNotRewriteAnAlreadyZeroedValue()
    {
        const string path = "metadata/effects/spells/monsters_effects/league_delirium/deliriumobject/mats/object/blur.mat";
        const string source = "{\"graphinstances\":[{\"custom_parameters\":[{\"name\":\"Blur Intensity\",\"parameters\":[{\"value\":0.0}]}]}]}";
        string result = Delirium.ScaleDeliriumObjectMaterial(path, source, 0.4, out int found);
        Assert.Equal(source, result);
        Assert.Equal(1, found);
    }

    [Theory]
    [InlineData("{\"xcreenspace_fog\":{}}", true)]
    [InlineData("{\"xffect_spawner\":{}}", true)]
    [InlineData("{\"screenspace_fog\":{},\"effect_spawner\":{}}", false)]
    public void Delirium_DetectsOnlyLegacyEnvironmentKeyDamage(string source, bool expected)
    {
        Assert.Equal(expected, Delirium.HasLegacyEnvironmentMutation(source));
    }

    [Fact]
    public void DatWriter_ChangesOnlyOneTextCell()
    {
        byte[] heap = Encoding.Unicode.GetBytes("Id\0Old\0");
        byte[] data = new byte[4 + 16 + 8 + heap.Length];
        BitConverter.GetBytes(1).CopyTo(data, 0);
        BitConverter.GetBytes((ulong)8).CopyTo(data, 4);
        BitConverter.GetBytes((ulong)14).CopyTo(data, 12);
        Array.Fill(data, (byte)0xBB, 20, 8);
        heap.CopyTo(data, 28);

        byte[] result = DatWriter.SetString(data, 16, 0, 8, "New");
        Assert.Equal((ulong)8, BitConverter.ToUInt64(result, 4));
        Assert.Equal("Id\0Old\0", Encoding.Unicode.GetString(result, 28, heap.Length));
        Assert.Equal("New\0", Encoding.Unicode.GetString(result, data.Length, result.Length - data.Length));
        Assert.Equal((ulong)(data.Length - 20), BitConverter.ToUInt64(result, 12));
    }

    [Fact]
    public void LoadingArtwork_RewriteIsRepeatableAndKeepsInput()
    {
        const string source = "begin\r\n\tmouse_enabled = true;\r\nend\r\n";
        string result = LoadingScreen.RewriteLoadingText(source);
        Assert.Contains("begin\r\n\tvisible = false;\r\n\tmouse_enabled = true;", result);
        Assert.Equal(result, LoadingScreen.RewriteLoadingText(result));
    }

    [Fact]
    public void NamedColorGroups_AcceptLegacyModColors()
    {
        Assert.Equal("Dangerous", ModColorGroups.Normalize("red"));
        Assert.Equal("Annoying", ModColorGroups.Normalize("green"));
        Assert.Equal("Great", ModColorGroups.Normalize("blue"));
        Assert.Equal("Avoid", ModColorGroups.Normalize("Avoid"));
    }

    [Fact]
    public void ReadabilityStyle_CanSwitchBetweenWholeLineAndTag()
    {
        const string source = "1 2 \"Monsters deal 30% increased Damage\"";
        string whole = ModTextStyler.Restyle(source, "rgb(209,46,46)", "BAD", true, false);
        string tag = ModTextStyler.Restyle(whole, "rgb(209,46,46)", "BAD", true, true);
        string restored = ModTextStyler.Restyle(tag, null, "BAD", false, false);
        Assert.Contains("<rgb(209,46,46)>{{Monsters deal", whole);
        Assert.Contains("{{BAD}}", tag);
        Assert.Equal(source, restored);
    }

}
