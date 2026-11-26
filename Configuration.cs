using System;
using Dalamud.Configuration;
using Newtonsoft.Json;
using PetFrenzy.Models;
using PetFrenzy.Services;

namespace PetFrenzy;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;

    /// <summary>Default English ("en"), toggleable to Portuguese ("pt").</summary>
    public string Language { get; set; } = "en";

    /// <summary>Whether the first-time Milly interactive spotlight tutorial has been completed or skipped.</summary>
    public bool TutorialCompleted { get; set; } = false;

    /// <summary>Floating HUD visibility and appearance (stored in PetFrenzy.json).</summary>
    public bool ShowHud { get; set; } = true;
    public bool TransparentHud { get; set; } = false;
    public bool LockHudPosition { get; set; } = false;
    public bool ShowHudQuickButtons { get; set; } = true;
    public bool ShowSpeechBubbles { get; set; } = true;
    public bool ShowJobWeapon { get; set; } = true;
    public bool HideHudInCutscene { get; set; } = true;
    public float HudScale { get; set; } = 1.0f;

    // ── Multiplayer Settings (Testing / Beta) ──
    public bool MultiplayerEnabled { get; set; } = true;
    public bool MultiplayerShareOwnPet { get; set; } = true;
    public bool MultiplayerShowWorldPets { get; set; } = true;
    public float MultiplayerWorldPetScale { get; set; } = 0.48f;
    public float MultiplayerWorldPetHeightOffset { get; set; } = 0.58f;
    public float MultiplayerWorldMaxDistance { get; set; } = 35f;
    public bool MultiplayerShowWorldPetLabels { get; set; } = false;
    public bool MultiplayerShowPartyPets { get; set; } = true;
    public float MultiplayerPartyPetScale { get; set; } = 0.85f;
    public bool MultiplayerLockPartyPets { get; set; } = false;
    public bool MultiplayerTransparentPartyPets { get; set; } = true;
    public bool MultiplayerShowPartyPetNames { get; set; } = true;
    public bool MultiplayerHideWorldPetForParty { get; set; } = true;

    /// <summary>
    /// One-time migration field for older plain-text saves; automatically moved to encrypted pet.sav and cleared.
    /// </summary>
    [JsonProperty("Pet", NullValueHandling = NullValueHandling.Ignore)]
    public PetStateData? LegacyPet { get; set; }

    /// <summary>
    /// Runtime pet state — NEVER serialized in plain text to PetFrenzy.json.
    /// Persisted exclusively in AES-256-GCM encrypted pet.sav.
    /// </summary>
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public PetStateData Pet { get; set; } = new();

    public void Save()
    {
        LegacyPet = null;
        Plugin.PluginInterface.SavePluginConfig(this);
        PetSaveStorage.Save(Pet);
    }
}
