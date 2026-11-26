using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using PetFrenzy.Models;
using PetFrenzy.Services;

namespace PetFrenzy.UI;

/// <summary>
/// Renders other PetFrenzy players' companions directly above their in-game nameplates
/// using 3D -> 2D WorldToScreen projection on ImGui's background draw list (click-through).
/// </summary>
public sealed class MultiplayerWorldOverlay
{
    public void Draw()
    {
        var cfg = Plugin.Config;
        if (!cfg.MultiplayerEnabled || !cfg.MultiplayerShowWorldPets)
            return;
        if (!Plugin.ClientState.IsLoggedIn)
            return;
        if (cfg.HideHudInCutscene &&
            (Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Plugin.Condition[ConditionFlag.WatchingCutscene]))
            return;

        var local = Plugin.ObjectTable.LocalPlayer;
        if (local == null) return;

        var localPos = local.Position;
        var maxDistSq = cfg.MultiplayerWorldMaxDistance * cfg.MultiplayerWorldMaxDistance;
        var scale = Math.Clamp(cfg.MultiplayerWorldPetScale, 0.28f, 0.95f);
        var baseSizePx = 96f * scale;
        var heightOffset = Math.Clamp(cfg.MultiplayerWorldPetHeightOffset, 0.05f, 1.80f);
        var dl = ImGui.GetBackgroundDrawList();
        var animTime = Plugin.Sim.AnimTimeSec;

        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj is not IPlayerCharacter pc) continue;
            if (pc.EntityId == local.EntityId) continue;

            var key = PetMultiplayerService.MakePlayerKey(pc.Name.TextValue, pc.HomeWorld.RowId);
            if (string.IsNullOrEmpty(key)) continue;

            if (!Plugin.Multiplayer.TryGetPeer(key, out var peer))
                continue;

            // Avoid duplicating party members when Party Pet HUD is active (configurable)
            if (peer.IsParty && cfg.MultiplayerShowPartyPets && cfg.MultiplayerHideWorldPetForParty)
                continue;

            var distSq = Vector3.DistanceSquared(localPos, pc.Position);
            if (distSq > maxDistSq) continue;

            // Project above the player's head / nameplate
            var worldPos = pc.Position + new Vector3(0f, 1.85f + heightOffset, 0f);
            if (!Plugin.GameGui.WorldToScreen(worldPos, out var screenPos))
                continue;

            // Subtle perspective shrink with distance (stays crisp up close, slightly smaller far away)
            var dist = MathF.Sqrt(distSq);
            var distFactor = Math.Clamp(1.08f - dist * 0.012f, 0.65f, 1.05f);
            var sizePx = baseSizePx * distFactor;

            // Instant local status reaction for 0ms combat / death responsiveness
            var liveMood = peer.Mood;
            var liveWeaponDrawn = peer.IsWeaponDrawn;
            if (pc.CurrentHp == 0)
            {
                liveMood = PetMood.Crying;
                liveWeaponDrawn = false;
            }
            else if ((pc.StatusFlags & StatusFlags.InCombat) != 0)
            {
                if (liveMood is PetMood.Idle or PetMood.Sleeping or PetMood.Happy)
                    liveMood = PetMood.Cheering;
                liveWeaponDrawn = true;
            }

            var weaponStyle = peer.ShowWeapon ? peer.Job.Weapon : JobWeaponStyle.None;
            var minPos = new Vector2(screenPos.X - sizePx * 0.5f, screenPos.Y - sizePx * 0.92f);

            PetCanvasRenderer.DrawPetInDrawList(
                dl,
                minPos,
                peer.PetData,
                liveMood,
                animTime,
                0f,
                sizePx,
                weaponStyle,
                liveWeaponDrawn,
                hovered: false);

            // Optional mini speech bubble above the remote pet (off by default)
            if (cfg.MultiplayerShowRemoteSpeechBubbles && !string.IsNullOrEmpty(peer.SpeechBubble) && dist < 24f)
            {
                var txtSize = ImGui.CalcTextSize(peer.SpeechBubble);
                var padX = 7f;
                var padY = 3f;
                var bW = txtSize.X + padX * 2f;
                var bH = txtSize.Y + padY * 2f;
                var bMin = new Vector2(screenPos.X - bW * 0.5f, minPos.Y - bH - 4f);
                var bMax = new Vector2(screenPos.X + bW * 0.5f, minPos.Y - 4f);
                dl.AddRectFilled(bMin, bMax, Theme.Rgba(0.11f, 0.02f, 0.22f, 0.88f), 8f);
                dl.AddRect(bMin, bMax, Theme.Rgba(1.0f, 0.90f, 0.0f, 0.78f), 8f);
                dl.AddText(new Vector2(bMin.X + padX, bMin.Y + padY), Theme.Rgba(1.0f, 0.94f, 0.45f, 0.96f), peer.SpeechBubble);
            }

            // Optional compact pet label (Name Lv.X)
            if (cfg.MultiplayerShowWorldPetLabels)
            {
                var lbl = peer.PetData.IsHatched
                    ? $"{peer.PetData.Name} Lv.{peer.PetData.Level}"
                    : $"{peer.PetData.Name} (Egg)";
                var lblSize = ImGui.CalcTextSize(lbl);
                var lMin = new Vector2(screenPos.X - lblSize.X * 0.5f - 5f, minPos.Y + sizePx - 2f);
                var lMax = new Vector2(screenPos.X + lblSize.X * 0.5f + 5f, minPos.Y + sizePx + lblSize.Y + 2f);
                dl.AddRectFilled(lMin, lMax, Theme.Rgba(0.07f, 0.01f, 0.14f, 0.78f), 5f);
                dl.AddText(new Vector2(lMin.X + 5f, lMin.Y + 1f), Theme.Rgba(0.95f, 0.93f, 1.0f, 0.92f), lbl);
            }
        }
    }
}
