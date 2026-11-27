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

            // Pre-compute name pill width so the window auto-sizes cleanly without clipping long names!
            var nameLabel = string.Empty;
            var nameTxtSize = Vector2.Zero;
            var pillW = 0f;
            if (cfg.MultiplayerShowPartyPetNames)
            {
                var firstName = peer.PlayerName.Split(' ')[0];
                nameLabel = peer.PetData.IsHatched
                    ? $"{firstName} • {peer.PetData.Name}"
                    : $"{firstName} • {L.T("Egg", "Ovo")}";
                nameTxtSize = ImGui.CalcTextSize(nameLabel);
                pillW = nameTxtSize.X + 18f;
            }

            var contentW = MathF.Max(canvasSize, pillW);

            // Reserved top slot for speech bubble only when remote speech bubbles are enabled
            var bubbleSlotH = cfg.MultiplayerShowRemoteSpeechBubbles ? 30f * scale : 0f;
            var bubbleOrigin = ImGui.GetCursorScreenPos();
            if (bubbleSlotH > 0f)
                ImGui.Dummy(new Vector2(contentW, bubbleSlotH));

            // Center the pet canvas horizontally inside contentW
            if (contentW > canvasSize)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (contentW - canvasSize) * 0.5f);

            var weaponStyle = peer.ShowWeapon ? peer.Job.Weapon : JobWeaponStyle.None;
            var clicked = PetCanvasRenderer.DrawPetCanvas(
                $"##PartyPetCanvas_{slotIndex}",
                peer.PetData,
                liveMood,
                Plugin.Sim.AnimTimeSec,
                0f,
                canvasSize,
                interactive: true,
                weaponStyle: weaponStyle,
                isWeaponDrawn: liveWeaponDrawn);

            if (clicked)
            {
                Plugin.Multiplayer.SendInteraction(peer, "pet");
            }

            // Right-click context menu for live petting & gifting snacks/potions!
            if (ImGui.BeginPopupContextItem($"##PartyPetCtx_{slotIndex}"))
            {
                ImGui.TextColored(Theme.YellowMain, $"{peer.PetData.Name} ({peer.PlayerName})");
                ImGui.Separator();

                var petCd = Plugin.Multiplayer.GetInteractCooldownSec(peer.PlayerKey, "pet");
                ImGui.BeginDisabled(petCd > 0f);
                var petLbl = petCd > 0f
                    ? L.T($"♥ Pet ({petCd:F0}s)", $"♥ Carinho ({petCd:F0}s)")
                    : L.T($"♥ Pet {peer.PetData.Name} (Free)", $"♥ Fazer Carinho em {peer.PetData.Name} (Grátis)");
                if (ImGui.MenuItem(petLbl))
                    Plugin.Multiplayer.SendInteraction(peer, "pet");
                ImGui.EndDisabled();

                var snackCd = Plugin.Multiplayer.GetInteractCooldownSec(peer.PlayerKey, "snack");
                ImGui.BeginDisabled(snackCd > 0f || Plugin.Config.Pet.Coins < PetMultiplayerService.GiftSnackCost);
                if (ImGui.MenuItem(L.T($"★ Gift Snack ({PetMultiplayerService.GiftSnackCost} Coins)", $"★ Presentear Petisco ({PetMultiplayerService.GiftSnackCost} Moedas)")))
                    Plugin.Multiplayer.SendInteraction(peer, "snack");
                ImGui.EndDisabled();

                var potCd = Plugin.Multiplayer.GetInteractCooldownSec(peer.PlayerKey, "potion");
                ImGui.BeginDisabled(potCd > 0f || Plugin.Config.Pet.Coins < PetMultiplayerService.GiftPotionCost);
                if (ImGui.MenuItem(L.T($"⚡ Gift Energy Potion ({PetMultiplayerService.GiftPotionCost} Coins)", $"⚡ Presentear Poção ({PetMultiplayerService.GiftPotionCost} Moedas)")))
                    Plugin.Multiplayer.SendInteraction(peer, "potion");
                ImGui.EndDisabled();

                ImGui.EndPopup();
            }

            // Speech bubble if remote speech bubbles are enabled and peer is saying something
            if (cfg.MultiplayerShowRemoteSpeechBubbles && !string.IsNullOrEmpty(peer.SpeechBubble))
            {
                var dl = ImGui.GetWindowDrawList();
                dl.PushClipRectFullScreen();
                var txtSize = ImGui.CalcTextSize(peer.SpeechBubble);
                var padX = 9f * scale;
                var padY = 4f * scale;
                var bW = MathF.Max(58f * scale, txtSize.X + padX * 2f);
                var bH = txtSize.Y + padY * 2f;
                var centerX = bubbleOrigin.X + contentW * 0.5f;
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
                ImGui.TextColored(Theme.YellowMain, L.T("Left-click: Pet ♥  •  Right-click: Gift Snack/Potion", "Clique esq.: Carinho ♥  •  Dir.: Dar Presente"));
                if (!cfg.MultiplayerLockPartyPets)
                    ImGui.TextColored(Theme.TextMuted, L.T("Drag to reposition anywhere", "Arraste para posicionar onde quiser"));
                ImGui.EndTooltip();
            }

            // Full-width unclipped name pill centered below pet
            if (cfg.MultiplayerShowPartyPetNames)
            {
                var cur = ImGui.GetCursorScreenPos();
                var boxW = MathF.Max(pillW, MathF.Min(contentW, canvasSize));
                var pMin = new Vector2(cur.X + (contentW - boxW) * 0.5f, cur.Y);
                var pMax = new Vector2(pMin.X + boxW, pMin.Y + nameTxtSize.Y + 6f);
                var dl = ImGui.GetWindowDrawList();
                dl.AddRectFilled(pMin, pMax, Theme.Rgba(0.08f, 0.02f, 0.17f, 0.88f), 6f);
                dl.AddRect(pMin, pMax, Theme.Rgba(0.62f, 0.42f, 0.96f, 0.50f), 6f);
                dl.AddText(new Vector2(pMin.X + (boxW - nameTxtSize.X) * 0.5f, pMin.Y + 3f),
                    Theme.ColU32(Theme.TextLight), nameLabel);
                ImGui.Dummy(new Vector2(contentW, nameTxtSize.Y + 8f));
            }
        }
        finally
        {
            ImGui.PopStyleVar(pushedVars);
            ImGui.PopStyleColor(pushedColors);
        }
    }
}
