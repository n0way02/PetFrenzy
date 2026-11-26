using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using PetFrenzy.Models;
using PetFrenzy.Services;

namespace PetFrenzy.UI;

/// <summary>
/// Individual draggable, resizable HUD window for a Party Member's PetFrenzy companion.
/// Up to 7 slots (one per party member) so the user can freely arrange friends' pets anywhere on screen.
/// </summary>
public sealed class PartyPetHudWindow : Window
{
    public const int MaxPartySlots = 7;

    private readonly int slotIndex;
    private RemotePetPeer? boundPeer;

    public PartyPetHudWindow(int slotIndex)
        : base($"PartyPetHUD_{slotIndex}###PetFrenzyPartySlot{slotIndex}",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.slotIndex = slotIndex;
        RespectCloseHotkey = false;
        IsOpen = false;
    }

    public void UpdateBinding(RemotePetPeer? peer, bool allowVisible)
    {
        boundPeer = peer;
        IsOpen = allowVisible && peer != null;
    }

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize;
        if (Plugin.Config.MultiplayerLockPartyPets)
            Flags |= ImGuiWindowFlags.NoMove;
        if (Plugin.Config.MultiplayerTransparentPartyPets)
            Flags |= ImGuiWindowFlags.NoBackground;

        // Place new slots neatly in a row on first appearance
        PositionCondition = ImGuiCond.FirstUseEver;
        Position = new Vector2(220f + slotIndex * 118f, 120f);

        base.PreDraw();
    }

    public override void Draw()
    {
        var peer = boundPeer;
        if (peer == null) return;

        var pushedColors = Theme.PushTheme();
        var pushedVars = Theme.PushStyleVars();

        try
        {
            var cfg = Plugin.Config;
            var scale = Math.Clamp(cfg.MultiplayerPartyPetScale, 0.50f, 1.50f);
            var canvasSize = 104f * scale;

            // Instant local status override if party member is in ObjectTable
            var liveMood = peer.Mood;
            var liveWeaponDrawn = peer.IsWeaponDrawn;
            foreach (var obj in Plugin.ObjectTable)
            {
                if (obj is IPlayerCharacter pc
                    && pc.HomeWorld.RowId == peer.HomeWorldId
                    && string.Equals(pc.Name.TextValue, peer.PlayerName, StringComparison.OrdinalIgnoreCase))
                {
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
                    break;
                }
            }

            // Reserved top slot for speech bubble so pet doesn't jump
            var bubbleSlotH = cfg.ShowSpeechBubbles ? 30f * scale : 0f;
            var bubbleOrigin = ImGui.GetCursorScreenPos();
            if (bubbleSlotH > 0f)
                ImGui.Dummy(new Vector2(canvasSize, bubbleSlotH));

            var weaponStyle = peer.ShowWeapon ? peer.Job.Weapon : JobWeaponStyle.None;
            PetCanvasRenderer.DrawPetCanvas(
                $"##PartyPetCanvas_{slotIndex}",
                peer.PetData,
                liveMood,
                Plugin.Sim.AnimTimeSec,
                0f,
                canvasSize,
                interactive: false,
                weaponStyle: weaponStyle,
                isWeaponDrawn: liveWeaponDrawn);

            // Speech bubble if peer is saying something
            if (cfg.ShowSpeechBubbles && !string.IsNullOrEmpty(peer.SpeechBubble))
            {
                var dl = ImGui.GetWindowDrawList();
                dl.PushClipRectFullScreen();
                var txtSize = ImGui.CalcTextSize(peer.SpeechBubble);
                var padX = 9f * scale;
                var padY = 4f * scale;
                var bW = MathF.Max(58f * scale, txtSize.X + padX * 2f);
                var bH = txtSize.Y + padY * 2f;
                var centerX = bubbleOrigin.X + canvasSize * 0.5f;
                var bMaxY = bubbleOrigin.Y + bubbleSlotH - 4f * scale;
                var bMin = new Vector2(centerX - bW * 0.5f, bMaxY - bH);
                var bMax = new Vector2(centerX + bW * 0.5f, bMaxY);

                dl.AddRectFilled(bMin, bMax, Theme.Rgba(0.13f, 0.03f, 0.27f, 0.94f), 9f * scale);
                dl.AddRect(bMin, bMax, Theme.Rgba(1.0f, 0.90f, 0.0f, 0.85f), 9f * scale);
                dl.AddText(new Vector2(centerX - txtSize.X * 0.5f, bMin.Y + (bH - txtSize.Y) * 0.5f),
                    Theme.Rgba(1.0f, 0.94f, 0.45f, 1f), peer.SpeechBubble);
                dl.PopClipRect();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                var jobTag = !string.IsNullOrEmpty(peer.Job.Abbrev) ? $" [{peer.Job.Abbrev}]" : string.Empty;
                ImGui.TextColored(Theme.YellowMain, $"{peer.PetData.Name}{jobTag} — {L.SpeciesName(peer.PetData.Species)}");
                ImGui.TextColored(Theme.TextLight, $"{L.T("Owner", "Dono")}: {peer.PlayerName}");
                if (peer.PetData.IsHatched)
                    ImGui.TextColored(Theme.TextMuted, $"Lv.{peer.PetData.Level} ({L.StageName(peer.PetData.Stage)})");
                else
                    ImGui.TextColored(Theme.TextMuted, $"{L.T("Egg", "Ovo")}: {peer.PetData.EggProgress:F0}%");
                if (!cfg.MultiplayerLockPartyPets)
                    ImGui.TextColored(Theme.TextMuted, L.T("Drag to reposition anywhere", "Arraste para posicionar onde quiser"));
                ImGui.EndTooltip();
            }

            // Compact name pill below pet
            if (cfg.MultiplayerShowPartyPetNames)
            {
                var firstName = peer.PlayerName.Split(' ')[0];
                var label = peer.PetData.IsHatched
                    ? $"{firstName} • {peer.PetData.Name}"
                    : $"{firstName} • {L.T("Egg", "Ovo")}";
                var txtSize = ImGui.CalcTextSize(label);
                var pillW = MathF.Max(canvasSize, txtSize.X + 12f);
                var cur = ImGui.GetCursorScreenPos();
                var pMin = new Vector2(cur.X + (canvasSize - pillW) * 0.5f, cur.Y);
                var pMax = new Vector2(pMin.X + pillW, pMin.Y + txtSize.Y + 4f);
                var dl = ImGui.GetWindowDrawList();
                dl.AddRectFilled(pMin, pMax, Theme.Rgba(0.08f, 0.02f, 0.17f, 0.85f), 6f);
                dl.AddRect(pMin, pMax, Theme.Rgba(0.62f, 0.42f, 0.96f, 0.45f), 6f);
                dl.AddText(new Vector2(pMin.X + (pillW - txtSize.X) * 0.5f, pMin.Y + 2f),
                    Theme.ColU32(Theme.TextLight), label);
                ImGui.Dummy(new Vector2(canvasSize, txtSize.Y + 6f));
            }
        }
        finally
        {
            ImGui.PopStyleVar(pushedVars);
            ImGui.PopStyleColor(pushedColors);
        }
    }
}
