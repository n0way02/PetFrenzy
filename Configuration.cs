using System;
using Dalamud.Configuration;
using PetFrenzy.Models;

namespace PetFrenzy;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Default English ("en"), toggleable to Portuguese ("pt").</summary>
    public string Language { get; set; } = "en";

    /// <summary>Floating HUD visibility and appearance.</summary>
    public bool ShowHud { get; set; } = true;
    public bool TransparentHud { get; set; } = false;
    public bool LockHudPosition { get; set; } = false;
    public bool ShowHudQuickButtons { get; set; } = true;
    public bool ShowSpeechBubbles { get; set; } = true;
    public bool HideHudInCutscene { get; set; } = true;
    public float HudScale { get; set; } = 1.0f;

    /// <summary>Persistent Tamagotchi pet state.</summary>
    public PetStateData Pet { get; set; } = new();

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
