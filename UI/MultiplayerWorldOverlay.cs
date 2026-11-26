using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using PetFrenzy.Models;
using PetFrenzy.Services;

namespace PetFrenzy.UI;

/// <summary>
/// Renders other PetFrenzy players' companions in world space (Above Nameplate, Above Head, or Near Shoulder)
/// using FFXIV's native GetNamePlateWorldPosition / GetHeight + screen-space pixel anchoring so zoom NEVER drifts.
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
        var scale = Math.Clamp(cfg.MultiplayerWorldPetScale, 0.30f, 1.00f);
        var uiScale = ImGuiHelpers.GlobalScale;
        var baseSizePx = 88f * scale * uiScale;
        var anchorMode = Math.Clamp(cfg.MultiplayerWorldPetAnchor, 0, 2);
        var fineOffsetY = Math.Clamp(cfg.MultiplayerWorldPetOffsetY, -80f, 80f) * uiScale;
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

            // Compute exact native anchor point in 3D + zoom-independent 2D pixel offset!
            var worldAnchor = ComputeAnchorWorldPos(pc, anchorMode);
            if (!Plugin.GameGui.WorldToScreen(worldAnchor, out var screenPos))
                continue;

            // Gentle distance scaling (Nameplate mode stays mostly constant like FFXIV nameplates!)
            var dist = MathF.Sqrt(distSq);
            var distFactor = anchorMode == 0
                ? Math.Clamp(1.02f - dist * 0.005f, 0.80f, 1.02f)
                : Math.Clamp(1.08f - dist * 0.010f, 0.68f, 1.08f);
            var sizePx = baseSizePx * distFactor;

            // Zoom-independent 2D screen-space offset
            var screenOffsetY = anchorMode switch
            {
                0 => -44f * uiScale + fineOffsetY, // Sits cleanly right above Name + Title!
                1 => -6f * uiScale + fineOffsetY,  // Perched right on top of the character's head
                _ => 12f * uiScale + fineOffsetY,  // Floating beside the character's shoulder
            };

            var anchorY = screenPos.Y + screenOffsetY;

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
            var minPos = new Vector2(screenPos.X - sizePx * 0.5f, anchorY - sizePx * 0.88f);

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

    private static unsafe Vector3 ComputeAnchorWorldPos(IPlayerCharacter pc, int anchorMode)
    {
        var basePos = pc.Position;
        var modelHeight = 1.65f;

        if (pc.Address != nint.Zero)
        {
            var go = (GameObject*)pc.Address;
            if (go != null)
            {
                var h = go->GetHeight();
                if (h > 0.35f && h < 4.5f)
                    modelHeight = h;

                if (anchorMode == 0)
                {
                    // FFXIV's exact native nameplate 3D anchor (automatically handles race height, mounts, sitting, etc.!)
                    FFXIVClientStructs.FFXIV.Common.Math.Vector3 npPos = default;
                    go->GetNamePlateWorldPosition(&npPos);
                    if (npPos.Y != 0f || npPos.X != 0f || npPos.Z != 0f)
                        return new Vector3(npPos.X, npPos.Y, npPos.Z);
                }
            }
        }

        if (anchorMode == 2)
        {
            // Near Shoulder: float beside the character's right shoulder in 3D space
            var rot = pc.Rotation;
            var rightX = MathF.Cos(rot);
            var rightZ = -MathF.Sin(rot);
            return new Vector3(
                basePos.X + rightX * 0.44f,
                basePos.Y + modelHeight * 0.80f,
                basePos.Z + rightZ * 0.44f);
        }

        // Above Head (or fallback for Above Nameplate): exact top of model head
        return new Vector3(basePos.X, basePos.Y + modelHeight, basePos.Z);
    }
}
