using PoeRedux.Patches;
using PoeRedux.Services;
using System.Text;
using Xunit;

namespace PoeRedux.Tests;

public class VisualSafetyTests
{
    [Fact]
    public void EnvironmentFx_PreservesLightingAndPostProcessing()
    {
        const string source = "\"post_processing\":{},\"player_environment_ao\":\"ambient.ao\",\"global_illumination\":{},\"effect_spawner\":{},\"rain_intensity\":1.0";
        int candidates = 0;
        string result = EnvironmentParticles2.PatchEnvironmentText(source, ref candidates);
        Assert.Contains("\"post_processing\":{}", result);
        Assert.Contains("\"player_environment_ao\":\"ambient.ao\"", result);
        Assert.Contains("\"global_illumination\":{}", result);
        Assert.Contains("\"effect_spawner\":{}", result);
        Assert.Contains("\"rain_intensity\":0.0", result);
        Assert.True(candidates > 0);
    }

    [Fact]
    public void Delirium_PreservesExposureAndDesaturation()
    {
        const string source = "\"fog\":{},\"post_processing\":{},\"post_transform\":{},\"desaturation\":0.4";
        string result = Delirium.PatchEnvironmentText(source);
        Assert.Contains("\"xog\":{}", result);
        Assert.Contains("\"post_processing\":{}", result);
        Assert.Contains("\"post_transform\":{}", result);
        Assert.Contains("\"desaturation\":0.4", result);
    }

    [Fact]
    public void ScreenFilters_PreserveGameplayCues()
    {
        const string source = "if( vignette_enable ) if ( dof_enable ) if( desaturation_enable ) exposure tonemap";
        string result = PostProcessFilters.RewriteShader(source);
        Assert.Contains("if( 0 )", result);
        Assert.Contains("if ( 0 )", result);
        Assert.Contains("if( desaturation_enable ) exposure tonemap", result);
        string vignetteOnly = PostProcessFilters.RewriteShader(source, vignette: true, dof: false);
        Assert.Contains("if ( dof_enable )", vignetteOnly);
        Assert.DoesNotContain("if( vignette_enable )", vignetteOnly);
    }

    [Fact]
    public void Weather_OnlyChangesSelectedIntensity()
    {
        const string source = "\"rain_intensity\":1.0,\"clouds_intensity\":0.8,\"directional_light\":{}";
        int candidates = 0;
        string result = EnvironmentParticles2.PatchEnvironmentText(source, ref candidates, rain: true, clouds: false);
        Assert.Contains("\"rain_intensity\":0.0", result);
        Assert.Contains("\"clouds_intensity\":0.8", result);
        Assert.Contains("\"directional_light\":{}", result);
        Assert.Equal(1, candidates);
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
}
