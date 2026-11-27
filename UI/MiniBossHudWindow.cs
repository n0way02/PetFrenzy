using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PetFrenzy.Models;
using PetFrenzy.Services;

namespace PetFrenzy.UI;

/// <summary>
/// Draggable on-screen Mini-Boss Battle Arena HUD!
/// Displays the Mini-Boss surrounded by your pet and all Party pets actively lunging and attacking
/// with weapons drawn, auto-attacking automatically while you trigger 3 Special Skills!
/// </summary>
public sealed class MiniBossHudWindow : Window
{
    private int nextBossChoice;
    private bool inviteZoneNext = true;

    public MiniBossHudWindow()
        : base("PetFrenzyMiniBossHUD###PetFrenzyBossArenaHud",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize)
    {
        RespectCloseHotkey = false;
        IsOpen = false;
        PositionCondition = ImGuiCond.FirstUseEver;
        Position = new Vector2(420f, 180f);
    }

    public override void Draw()
    {
        var mp = Plugin.Multiplayer;
        var boss = mp.ActiveBoss;
        if (boss == null) return;

        var pushedColors = Theme.PushTheme();
        var pushedVars = Theme.PushStyleVars();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14f, 12f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.065f, 0.015f, 0.145f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.55f, 0.30f, 0.92f, 0.55f));

        try
        {
            var cfg = Plugin.Config;
            var sim = Plugin.Sim;
            var t = sim.AnimTimeSec;
            var arenaW = 376f;

            // ── Header: Level Badge + Boss Name + Timer + Close button ──
            var headerCol = boss.Defeated ? Theme.GreenAccent : boss.Wiped ? Theme.RedDanger : Theme.TextWhite;
            MiniBossRenderer.DrawLevelBadge(boss.BossLevel, isBossDanger: boss.BossLevel >= 70);
            ImGui.SameLine(0, 8f);
            ImGui.AlignTextToFramePadding();
            var titleTxt = boss.Defeated
                ? L.T($"★ VICTORY! {boss.BossName}", $"★ VITÓRIA! {boss.BossName}")
                : boss.Wiped
                    ? L.T($"× WIPE! {boss.BossName}", $"× WIPE! {boss.BossName}")
                    : boss.BossName;
            ImGui.TextColored(headerCol, titleTxt);

            if (!boss.IsEnded)
            {
                var timerTxt = $"{boss.RemainingSec}s";
                var timerW = ImGui.CalcTextSize(timerTxt).X;
                ImGui.SameLine(arenaW - timerW - 24f);
                ImGui.TextColored(boss.RemainingSec <= 25 ? Theme.RedDanger : Theme.YellowMain, timerTxt);
            }

            ImGui.SameLine(arenaW - 14f);
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.BgSurface);
            if (ImGui.SmallButton("×##CloseBossHud"))
                mp.DismissBossHud();
            ImGui.PopStyleColor();

            ImGui.Dummy(new Vector2(0, 3f));

            // ── Spacious RPG Gauges (Boss HP, Pet Party HP, Shared Limit Break) ──
            var hpFrac = Math.Clamp((float)boss.Hp / Math.Max(1, boss.MaxHp), 0f, 1f);
            MiniBossRenderer.DrawRpgBar(
                arenaW, 10f, hpFrac,
                boss.Defeated ? Theme.GreenAccent : new Vector4(0.92f, 0.22f, 0.34f, 1f),
                L.T("BOSS HP", "HP DO BOSS"),
                $"{boss.Hp:N0} / {boss.MaxHp:N0}",
                leftLabelCol: Theme.TextMuted,
                rightValueCol: Theme.TextWhite);

            ImGui.Dummy(new Vector2(0, 3f));

            var hasServerHp = boss.PartyMaxHp > 0;
            var partyFrac = hasServerHp ? Math.Clamp((float)boss.PartyHp / boss.PartyMaxHp, 0f, 1f) : 1f;
            if (hasServerHp)
            {
                var partyBarCol = boss.ShieldActive
                    ? Theme.CyanAccent
                    : partyFrac <= 0.30f
                        ? Theme.RedDanger
                        : partyFrac <= 0.55f
                            ? Theme.OrangeAccent
                            : Theme.GreenAccent;
                var partyLeft = boss.ShieldActive
                    ? L.T("PET PARTY HP  [SHIELD -70%]", "HP DOS PETS  [ESCUDO -70%]")
                    : L.T("PET PARTY HP", "HP DOS PETS");
                MiniBossRenderer.DrawRpgBar(
                    arenaW, 9f, partyFrac, partyBarCol,
                    partyLeft,
                    $"{boss.PartyHp:N0} / {boss.PartyMaxHp:N0}",
                    leftLabelCol: boss.ShieldActive ? Theme.CyanAccent : Theme.TextMuted,
                    rightValueCol: partyFrac <= 0.30f ? Theme.RedDanger : Theme.TextWhite,
                    glowBorder: boss.ShieldActive);
            }
            else
            {
                ImGui.TextColored(Theme.OrangeAccent, L.T(
                    "! Update server (discloud.zip) for Pet HP & Boss Levels!",
                    "! Atualize o servidor (discloud.zip) para HP e Níveis!"));
            }

            if (hasServerHp && !boss.IsEnded)
            {
                ImGui.Dummy(new Vector2(0, 3f));
                var lbFrac = Math.Clamp(boss.LbGauge / 100f, 0f, 1f);
                var lbReady = boss.LbGauge >= 100;
                var lbCol = lbReady
                    ? Vector4.Lerp(Theme.YellowMain, Theme.CyanAccent, 0.5f + 0.5f * MathF.Sin(t * 9f))
                    : new Vector4(0.96f, 0.68f, 0.14f, 1f);
                MiniBossRenderer.DrawRpgBar(
                    arenaW, 7f, lbFrac, lbCol,
                    lbReady ? L.T("★ PARTY LIMIT BREAK READY!", "★ LIMIT BREAK DA PARTY PRONTO!") : "LIMIT BREAK",
                    $"{boss.LbGauge}%",
                    leftLabelCol: lbReady ? Theme.YellowMain : Theme.TextMuted,
                    rightValueCol: lbReady ? Theme.YellowMain : Theme.TextLight,
                    glowBorder: lbReady);
            }

            // Telegraphed Boss Cast Warning Card (spacious, never squished!)
            var isBossCasting = !boss.IsEnded && !string.IsNullOrEmpty(boss.BossCastName);
            if (isBossCasting)
            {
                ImGui.Dummy(new Vector2(0, 5f));
                MiniBossRenderer.DrawBossCastBanner(
                    arenaW,
                    boss.BossCastName,
                    boss.BossCastProgress,
                    t,
                    L.T("[ USE SHIELD! ]", "[ USE ESCUDO! ]"));
            }

            ImGui.Dummy(new Vector2(0, 5f));

            // ── Live Battle Stage: Boss in center + up to 8 Party Pets surrounding and attacking! ──
            MiniBossRenderer.DrawLiveBattleStage("##HudBossSprite", boss, arenaW, 158f, t);

            // Last hit / boss attack callout inside a clean pill
            if (!string.IsNullOrEmpty(boss.LastHitText))
            {
                ImGui.Dummy(new Vector2(0, 3f));
                var logCol = boss.LastHitText.StartsWith("!") ? Theme.OrangeAccent : Theme.YellowMain;
                MiniBossRenderer.DrawCombatLogPill(arenaW, boss.LastHitText, logCol);
            }

            ImGui.Dummy(new Vector2(0, 4f));

            // ── 4 Tactical Skills (while alive) OR Choose Next Boss (after Victory / Wipe!) ──
            if (!boss.IsEnded)
            {
                var btnW = (arenaW - 8f) * 0.5f;
                var btnH = 31f;

                // Row 1: Burst | Special
                var bCd = mp.BurstCooldownSec;
                ImGui.BeginDisabled(bCd > 0f);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.18f, 0.44f, 0.82f, 1f));
                if (ImGui.Button(bCd > 0f ? $"▲ {bCd:0.0}s##sk1" : L.T("▲ Burst (2.2x)##sk1", "▲ Golpe (2.2x)##sk1"), new Vector2(btnW, btnH)))
                    mp.AttackPartyBoss("burst", L.T("Burst Strike", "Golpe Duplo"));
                ImGui.PopStyleColor();
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(L.T("Burst Strike — 2.2x damage (7.5s cooldown)", "Golpe Duplo — 2.2x dano (7.5s recarga)"));

                ImGui.SameLine(0, 8f);
                var uCd = mp.UltimateCooldownSec;
                var ultName = PetMultiplayerService.GetSpeciesUltimateName(cfg.Pet.Species);
                ImGui.BeginDisabled(uCd > 0f);
                ImGui.PushStyleColor(ImGuiCol.Button, Theme.PurpleMain);
                if (ImGui.Button(uCd > 0f ? $"★ {uCd:0.0}s##sk2" : L.T("★ Special (3.2x)##sk2", "★ Especial (3.2x)##sk2"), new Vector2(btnW, btnH)))
                    mp.AttackPartyBoss("ultimate", ultName);
                ImGui.PopStyleColor();
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip($"{ultName} — 3.2x damage (12.5s cooldown)");

                ImGui.Dummy(new Vector2(0, 2f));

                // Row 2: Shared Party Limit Break | Heal & Shield
                var lbReady = boss.LbGauge >= 100;
                ImGui.BeginDisabled(!lbReady);
                var lbBtnCol = lbReady
                    ? Vector4.Lerp(Theme.YellowMain, Theme.CyanAccent, 0.5f + 0.5f * MathF.Sin(t * 9f))
                    : Theme.BgSurface;
                ImGui.PushStyleColor(ImGuiCol.Button, lbBtnCol);
                ImGui.PushStyleColor(ImGuiCol.Text, lbReady ? Theme.BgDark : Theme.TextMuted);
                if (ImGui.Button(lbReady ? "♥ LIMIT BREAK! (5.5x)##sk3" : $"♥ LB {boss.LbGauge}%##sk3", new Vector2(btnW, btnH)))
                    mp.AttackPartyBoss("lb", "Pet Limit Break");
                ImGui.PopStyleColor(2);
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(L.T(
                        "Shared Party Limit Break! Charges over time & from skills/blocks. Using it spends the gauge for the whole party! (5.5x guaranteed CRIT)",
                        "Limit Break compartilhado da Party! Carrega com o tempo, skills e bloqueios. Ao usar, gasta a barra para toda a party! (5.5x CRIT garantido)"));

                ImGui.SameLine(0, 8f);
                var hCd = mp.HealCooldownSec;
                var shieldBtnCol = isBossCasting && hCd <= 0f
                    ? Vector4.Lerp(Theme.CyanAccent, Theme.GreenAccent, 0.5f + 0.5f * MathF.Sin(t * 10f))
                    : new Vector4(0.14f, 0.58f, 0.42f, 1f);
                ImGui.BeginDisabled(hCd > 0f);
                ImGui.PushStyleColor(ImGuiCol.Button, shieldBtnCol);
                ImGui.PushStyleColor(ImGuiCol.Text, isBossCasting && hCd <= 0f ? Theme.BgDark : Theme.TextWhite);
                if (ImGui.Button(hCd > 0f ? $"+ {hCd:0.0}s##sk4" : L.T("+ Heal & Shield##sk4", "+ Cura & Escudo##sk4"), new Vector2(btnW, btnH)))
                    mp.AttackPartyBoss("heal", L.T("Heal & Shield", "Cura & Escudo"));
                ImGui.PopStyleColor(2);
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(L.T(
                        "Heals Pet Party HP & raises a 5.5s Shield (-70% boss damage/casts)! (8.5s cooldown)",
                        "Cura o HP dos Pets e ativa um Escudo de 5.5s (-70% dano/casts do boss)! (8.5s recarga)"));

                // Mid-fight backup button if room has < 8 fighters and there are other players in the zone!
                var fightersCount = Math.Max(1, boss.ParticipantKeys.Count);
                if (fightersCount < 8 && mp.ZonePeerCount > mp.PartyPeerCount)
                {
                    ImGui.Dummy(new Vector2(0, 3f));
                    ImGui.PushStyleColor(ImGuiCol.Button, Theme.BgSurface);
                    if (ImGui.Button(L.T($"+ Invite Zone Players ({fightersCount}/8)##HudInvZone", $"+ Convidar Jogadores da Área ({fightersCount}/8)##HudInvZone"), new Vector2(arenaW, 24f)))
                        mp.InviteZoneToActiveBoss();
                    ImGui.PopStyleColor();
                }
            }
            else
            {
                if (boss.Defeated)
                {
                    ImGui.TextColored(Theme.GreenAccent, L.T(
                        $"★ Reward: +{boss.RewardCoins} Coins  •  +{boss.RewardXp} XP!",
                        $"★ Recompensa: +{boss.RewardCoins} Moedas  •  +{boss.RewardXp} XP!"));
                }
                else
                {
                    ImGui.TextColored(Theme.RedDanger, L.T(
                        "Party wiped! Time Heal & Shield during boss casts!",
                        "Wipe da party! Use Cura & Escudo nos casts do boss!"));
                }

                ImGui.Dummy(new Vector2(0, 3f));

                if (mp.PartyPeerCount < 7)
                {
                    ImGui.Checkbox(L.T("Invite nearby players in zone##HudInvChk", "Convidar jogadores da área##HudInvChk"), ref inviteZoneNext);
                }

                // Compact boss switcher so you can retry or pick a DIFFERENT Mini-Boss!
                ImGui.SetNextItemWidth(arenaW - 118f);
                var curChoice = Math.Clamp(nextBossChoice, 0, MiniBossRenderer.Catalog.Length - 1);
                var curDef = MiniBossRenderer.Catalog[curChoice];
                if (ImGui.BeginCombo("##HudNextBoss", $"Lv.{curDef.Level} {L.T(curDef.NameEn, curDef.NamePt)}"))
                {
                    foreach (var d in MiniBossRenderer.Catalog)
                    {
                        if (ImGui.Selectable($"Lv.{d.Level} {L.T(d.NameEn, d.NamePt)} (+{d.Coins}c)##nb_{d.Type}", nextBossChoice == d.Type))
                            nextBossChoice = d.Type;
                    }
                    ImGui.EndCombo();
                }

                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain);
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark);
                if (ImGui.Button(L.T("Summon!", "Invocar!"), new Vector2(110f, 0)))
                    mp.SummonPartyBoss(curChoice, inviteZoneNext && mp.PartyPeerCount < 7);
                ImGui.PopStyleColor(2);
            }

            // Compact top damage line
            if (boss.Contributors.Count > 0)
            {
                ImGui.Dummy(new Vector2(0, 3f));
                var summary = string.Join("  •  ", boss.Contributors.ConvertAll(c => $"{c.PetName}: {c.Damage:N0}"));
                ImGui.TextColored(Theme.TextMuted, summary);
            }
        }
        finally
        {
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);
            ImGui.PopStyleVar(pushedVars);
            ImGui.PopStyleColor(pushedColors);
        }
    }
}
