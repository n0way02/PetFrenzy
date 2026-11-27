using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using PetFrenzy.Models;
using PetFrenzy.Services;

namespace PetFrenzy.UI;

/// <summary>
/// Renders animated 60 FPS FFXIV Co-op Mini-Bosses (Jumbo Cactuar, King Moggle Mog,
/// King Behemoth, Ultima Chibi) for PetFrenzy's Party Mini-Boss Arena.
/// </summary>
public static class MiniBossRenderer
{
    public static readonly (int Type, int Level, string NameEn, string NamePt, int Coins, int Xp)[] Catalog =
    [
        (0, 20, "Jumbo Cactuar",   "Cactuar Gigante",  60,  75),
        (1, 45, "King Moggle Mog", "Rei Moggle Mog",   85,  110),
        (2, 70, "King Behemoth",   "Rei Behemoth",     120, 160),
        (3, 95, "Ultima Chibi",    "Ultima Chibi",     180, 240),
    ];

    /// <summary>
    /// Draws a clean, non-cramped RPG status bar with a dedicated label/value header row above a sleek glossy bar.
    /// </summary>
    public static void DrawRpgBar(
        float width,
        float barHeight,
        float fraction,
        Vector4 fillCol,
        string leftLabel,
        string rightValue,
        Vector4? leftLabelCol = null,
        Vector4? rightValueCol = null,
        bool glowBorder = false)
    {
        var frac = Math.Clamp(fraction, 0f, 1f);
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var textH = ImGui.GetTextLineHeight();
        var totalH = textH + 4f + barHeight;

        ImGui.Dummy(new Vector2(width, totalH));

        // Top row: crisp left label + right-aligned value (zero overlap or squishing!)
        var lCol = Theme.ColU32(leftLabelCol ?? Theme.TextMuted);
        var rCol = Theme.ColU32(rightValueCol ?? Theme.TextWhite);
        dl.AddText(start, lCol, leftLabel);
        var valSz = ImGui.CalcTextSize(rightValue);
        dl.AddText(new Vector2(start.X + MathF.Max(0f, width - valSz.X), start.Y), rCol, rightValue);

        // Bar track
        var barMin = new Vector2(start.X, start.Y + textH + 3f);
        var barMax = new Vector2(start.X + width, barMin.Y + barHeight);
        var rounding = barHeight * 0.5f;

        dl.AddRectFilled(barMin, barMax, Theme.Rgba(0.05f, 0.01f, 0.11f, 0.96f), rounding);

        if (frac > 0.005f)
        {
            var fillW = MathF.Max(barHeight, width * frac);
            var fillMax = new Vector2(MathF.Min(barMax.X, barMin.X + fillW), barMax.Y);
            dl.AddRectFilled(barMin, fillMax, Theme.ColU32(fillCol), rounding);

            // Subtle glossy top highlight
            var hiMax = new Vector2(fillMax.X, barMin.Y + MathF.Max(2f, barHeight * 0.36f));
            dl.AddRectFilled(barMin + new Vector2(2f, 1f), hiMax - new Vector2(2f, 0f), Theme.Rgba(1f, 1f, 1f, 0.22f), rounding);
        }

        var borderU32 = glowBorder
            ? Theme.ColU32(new Vector4(fillCol.X, fillCol.Y, fillCol.Z, 0.88f))
            : Theme.Rgba(0.91f, 0.91f, 0.96f, 0.16f);
        dl.AddRect(barMin, barMax, borderU32, rounding, ImDrawFlags.None, glowBorder ? 1.5f : 1.0f);
    }

    /// <summary>
    /// Draws a spacious, high-visibility FFXIV-style Boss Cast warning card with pulsing border and progress bar.
    /// </summary>
    public static void DrawBossCastBanner(float width, string castName, float progress, float timeSec, string actionHint)
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var textH = ImGui.GetTextLineHeight();
        var barH = 10f;
        var padX = 10f;
        var padY = 7f;
        var totalH = padY * 2f + textH + 6f + barH;

        ImGui.Dummy(new Vector2(width, totalH));

        var end = start + new Vector2(width, totalH);
        var pulse = 0.5f + 0.5f * MathF.Sin(timeSec * 10f);
        var accent = Vector4.Lerp(Theme.OrangeAccent, Theme.RedDanger, pulse);

        // Dark crimson card + pulsing warning border
        dl.AddRectFilled(start, end, Theme.Rgba(0.24f, 0.04f, 0.08f, 0.94f), 8f);
        dl.AddRect(start, end, Theme.ColU32(accent), 8f, ImDrawFlags.None, 1.8f);

        // Left: "! CAST: 10,000 Needles!" | Right: "[ USE SHIELD! ]"
        var rowPos = start + new Vector2(padX, padY);
        dl.AddText(rowPos, Theme.ColU32(Theme.YellowMain), $"! {castName}");

        var hintSz = ImGui.CalcTextSize(actionHint);
        var hintPos = new Vector2(end.X - padX - hintSz.X, rowPos.Y);
        dl.AddText(hintPos, Theme.ColU32(Theme.CyanAccent), actionHint);

        // Wide cast progress bar
        var barMin = new Vector2(start.X + padX, rowPos.Y + textH + 5f);
        var barW = MathF.Max(20f, width - padX * 2f);
        var barMax = new Vector2(barMin.X + barW, barMin.Y + barH);
        dl.AddRectFilled(barMin, barMax, Theme.Rgba(0.08f, 0.01f, 0.04f, 0.95f), 5f);

        var frac = Math.Clamp(progress, 0f, 1f);
        if (frac > 0.01f)
        {
            var fillMax = new Vector2(barMin.X + MathF.Max(barH, barW * frac), barMax.Y);
            dl.AddRectFilled(barMin, fillMax, Theme.ColU32(accent), 5f);
        }
        dl.AddRect(barMin, barMax, Theme.Rgba(1f, 0.72f, 0.42f, 0.45f), 5f);
    }

    /// <summary>
    /// Draws a recessed pill banner for combat log callouts.
    /// </summary>
    public static void DrawCombatLogPill(float width, string text, Vector4 textCol)
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var textH = ImGui.GetTextLineHeight();
        var totalH = textH + 10f;
        ImGui.Dummy(new Vector2(width, totalH));

        var end = start + new Vector2(width, totalH);
        dl.AddRectFilled(start, end, Theme.Rgba(0.11f, 0.03f, 0.22f, 0.88f), 6f);
        dl.AddRect(start, end, Theme.Rgba(0.91f, 0.91f, 0.96f, 0.12f), 6f);
        dl.AddText(start + new Vector2(9f, 5f), Theme.ColU32(textCol), text);
    }

    /// <summary>
    /// Draws a compact Level badge pill ("Lv.20") and returns its width.
    /// </summary>
    public static void DrawLevelBadge(int level, bool isBossDanger = false)
    {
        var label = $"Lv.{level}";
        var txtSz = ImGui.CalcTextSize(label);
        var padX = 7f;
        var padY = 2f;
        var sz = new Vector2(txtSz.X + padX * 2f, txtSz.Y + padY * 2f);
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(sz);

        var dl = ImGui.GetWindowDrawList();
        var bg = isBossDanger ? Theme.Rgba(0.68f, 0.12f, 0.22f, 0.95f) : Theme.ColU32(Theme.PurpleMain);
        dl.AddRectFilled(pos, pos + sz, bg, 5f);
        dl.AddRect(pos, pos + sz, Theme.Rgba(1f, 0.90f, 0.00f, 0.55f), 5f);
        dl.AddText(pos + new Vector2(padX, padY), Theme.ColU32(Theme.YellowMain), label);
    }

    /// <summary>
    /// Draws the shared live Battle Arena stage: Boss in the center surrounded by your pet and up to 7 party pets
    /// actively lunging with weapons drawn, energy slash beams, shield bubbles, and boss cast danger telegraph!
    /// </summary>
    public static void DrawLiveBattleStage(string bossCanvasId, PartyBossSnapshot boss, float arenaW, float stageH, float timeSec)
    {
        var cfg = Plugin.Config;
        var sim = Plugin.Sim;
        var mp = Plugin.Multiplayer;

        var isBossCasting = !boss.IsEnded && !string.IsNullOrEmpty(boss.BossCastName);
        var partyFrac = boss.PartyMaxHp > 0 ? Math.Clamp((float)boss.PartyHp / boss.PartyMaxHp, 0f, 1f) : 1f;

        var stageOrigin = ImGui.GetCursorScreenPos();
        var stageEnd = stageOrigin + new Vector2(arenaW, stageH);
        var dl = ImGui.GetWindowDrawList();

        // Arena floor backdrop with FFXIV-style concentric raid arena rings!
        dl.AddRectFilled(stageOrigin, stageEnd, Theme.Rgba(0.05f, 0.01f, 0.12f, 0.94f), 10f);
        var bossCenter = stageOrigin + new Vector2(arenaW * 0.50f, stageH * 0.50f);
        dl.AddCircle(bossCenter, MathF.Min(66f, stageH * 0.43f), Theme.Rgba(0.52f, 0.30f, 0.88f, 0.16f), 40, 1.2f);
        dl.AddCircle(bossCenter, MathF.Min(40f, stageH * 0.26f), Theme.Rgba(0.52f, 0.30f, 0.88f, 0.12f), 32, 1.0f);

        var borderCol = boss.Defeated
            ? Theme.Rgba(0.29f, 0.87f, 0.50f, 0.65f)
            : boss.Wiped || isBossCasting
                ? Theme.Rgba(0.95f, 0.25f, 0.28f, 0.80f)
                : Theme.Rgba(0.58f, 0.36f, 0.94f, 0.45f);
        dl.AddRect(stageOrigin, stageEnd, borderCol, 10f, ImDrawFlags.None, 1.4f);

        // Collect combatants: Local pet + up to 7 party/zone-invited pets (full 8-player party!)
        var partyPeers = mp.GetBossFighterPeers(boss);
        var fighters = new List<(PetStateData Pet, JobWeaponStyle Weapon, bool IsLocal)>(8)
        {
            (cfg.Pet, cfg.ShowJobWeapon ? sim.CurrentJob.Weapon : JobWeaponStyle.None, true)
        };
        for (var i = 0; i < partyPeers.Count && fighters.Count < 8; i++)
        {
            var p = partyPeers[i];
            fighters.Add((p.PetData, p.ShowWeapon ? p.Job.Weapon : JobWeaponStyle.None, false));
        }

        // Pulsing red danger telegraph ring around boss while casting a raidwide!
        if (isBossCasting)
        {
            var rPulse = 36f + boss.BossCastProgress * 30f;
            dl.AddCircleFilled(bossCenter, rPulse, Theme.Rgba(0.95f, 0.20f, 0.22f, 0.18f), 28);
            dl.AddCircle(bossCenter, rPulse, Theme.Rgba(1.00f, 0.42f, 0.18f, 0.85f), 28, 2.2f);
        }

        // Draw all attacking pets (up to 8!) in a clean arc around the boss
        var petSize = fighters.Count <= 2 ? 60f : fighters.Count <= 6 ? 48f : 42f;
        var fighterMood = boss.Defeated
            ? PetMood.Celebrating
            : boss.Wiped
                ? PetMood.Crying
                : (isBossCasting || partyFrac < 0.35f)
                    ? PetMood.Panic
                    : PetMood.Cheering;

        var flankScale = Math.Clamp(arenaW / 376f, 0.78f, 1.15f);
        var vertScale = Math.Clamp(stageH / 158f, 0.80f, 1.10f);

        for (var i = 0; i < fighters.Count; i++)
        {
            var (fPet, fWeapon, isLocal) = fighters[i];

            // Place up to 4 fighters on the left flank and 4 on the right flank around the boss!
            var side = (i % 2 == 0) ? -1f : 1f;
            var row = i / 2; // 0, 1, 2, 3
            var baseX = bossCenter.X + side * (row switch { 0 => 122f, 1 => 112f, 2 => 136f, _ => 98f }) * flankScale;
            var baseY = bossCenter.Y + (row switch { 0 => 10f, 1 => -34f, 2 => 46f, _ => -54f }) * vertScale;

            // Rhythmic auto-attack lunge toward the boss (plus strong lunge when local player fires a skill!)
            var phase = (timeSec * 2.4f + i * 1.35f) % (MathF.PI * 2f);
            var autoLunge = boss.IsEnded ? 0f : MathF.Max(0f, MathF.Sin(phase) - 0.55f) * 32f;
            var skillLunge = !boss.IsEnded && isLocal && mp.LocalLungeTimerSec > 0f ? (mp.LocalLungeTimerSec / 0.35f) * 26f : 0f;
            var lunge = MathF.Min(36f, autoLunge + skillLunge);

            var drawCenter = new Vector2(baseX - side * lunge, baseY);
            var minPos = new Vector2(drawCenter.X - petSize * 0.5f, drawCenter.Y - petSize * 0.54f);

            // Attack slash / energy beam VFX when lunging close to the boss!
            if (!boss.IsEnded && lunge > 10f)
            {
                var beamCol = isLocal && mp.LocalLungeTimerSec > 0f
                    ? Theme.ColU32(Theme.YellowMain)
                    : Theme.Rgba(0.45f, 0.85f, 1.0f, 0.72f);
                dl.AddLine(drawCenter, bossCenter + new Vector2(side * 16f, 0f), beamCol, isLocal && mp.LocalLungeTimerSec > 0f ? 3.5f : 2.0f);
            }

            // Cyan mitigation barrier bubble around pets when Shield is active!
            if (!boss.IsEnded && boss.ShieldActive)
            {
                dl.AddCircleFilled(drawCenter, petSize * 0.44f, Theme.Rgba(0.22f, 0.78f, 0.97f, 0.14f), 20);
                dl.AddCircle(drawCenter, petSize * 0.44f, Theme.Rgba(0.22f, 0.88f, 1.00f, 0.72f), 20, 1.8f);
            }

            PetCanvasRenderer.DrawPetInDrawList(
                dl,
                minPos,
                fPet,
                fighterMood,
                timeSec + i * 0.4f,
                0f,
                petSize,
                fWeapon,
                isWeaponDrawn: !boss.IsEnded,
                hovered: false);
        }

        // Draw the Mini-Boss right in the center of the arena
        var bossSize = MathF.Min(114f, stageH * 0.76f);
        ImGui.SetCursorScreenPos(new Vector2(bossCenter.X - bossSize * 0.5f, bossCenter.Y - bossSize * 0.54f));
        DrawBossCanvas(bossCanvasId, boss.BossType, boss.Defeated, timeSec, mp.BossHitShakeSec, bossSize, clickable: false);

        // Reserve stage space cleanly
        ImGui.SetCursorScreenPos(stageOrigin);
        ImGui.Dummy(new Vector2(arenaW, stageH));
    }

    public static bool DrawBossCanvas(string id, int bossType, bool defeated, float timeSec, float hitShakeSec, float sizePx, bool clickable = true)
    {
        ImGui.InvisibleButton(id, new Vector2(sizePx, sizePx));
        var clicked = clickable && !defeated && ImGui.IsItemClicked(ImGuiMouseButton.Left);
        if (clickable && !defeated && ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var min = ImGui.GetItemRectMin();
        var center = min + new Vector2(sizePx * 0.5f, sizePx * 0.56f);
        var s = sizePx / 130f;
        var dl = ImGui.GetWindowDrawList();

        // Arena ground ring + shadow
        dl.AddCircleFilled(new Vector2(center.X, min.Y + sizePx * 0.87f), 36f * s, Theme.Rgba(0.02f, 0.00f, 0.06f, 0.55f));
        var pulse = 0.5f + 0.5f * MathF.Sin(timeSec * 3.8f);
        var auraCol = defeated
            ? Theme.Rgba(0.29f, 0.87f, 0.50f, 0.22f)
            : Theme.Rgba(0.95f, 0.22f, 0.34f, 0.14f + pulse * 0.08f);
        dl.AddCircleFilled(center, (48f + pulse * 3f) * s, auraCol);

        var shakeX = hitShakeSec > 0f ? MathF.Sin(timeSec * 65f) * 6.5f * s * (hitShakeSec / 0.28f) : 0f;
        var bobY = defeated ? 12f * s : MathF.Sin(timeSec * 3.2f) * 4f * s;
        var c = center + new Vector2(shakeX, bobY);

        switch (bossType)
        {
            case 0: DrawJumboCactuar(dl, c, s, defeated, timeSec); break;
            case 1: DrawKingMoggle(dl, c, s, defeated, timeSec); break;
            case 2: DrawKingBehemoth(dl, c, s, defeated, timeSec); break;
            default: DrawUltimaChibi(dl, c, s, defeated, timeSec); break;
        }

        // Flash white ring on hit!
        if (hitShakeSec > 0f)
        {
            var a = Math.Clamp(hitShakeSec / 0.28f, 0f, 1f);
            dl.AddCircle(c, 42f * s, Theme.Rgba(1f, 0.94f, 0.45f, 0.85f * a), 24, 3.2f * s);
        }

        // Victory sparkles if defeated
        if (defeated)
        {
            for (var i = 0; i < 6; i++)
            {
                var ang = timeSec * 2.2f + i * (MathF.PI / 3f);
                var p = center + new Vector2(MathF.Cos(ang) * 44f * s, MathF.Sin(ang) * 28f * s - 10f * s);
                dl.AddCircleFilled(p, 3.2f * s, i % 2 == 0 ? Theme.ColU32(Theme.YellowMain) : Theme.ColU32(Theme.CyanAccent));
            }
        }

        return clicked;
    }

    private static void DrawJumboCactuar(ImDrawListPtr dl, Vector2 c, float s, bool defeated, float t)
    {
        var green = Theme.Rgba(0.24f, 0.78f, 0.36f);
        var darkGreen = Theme.Rgba(0.12f, 0.48f, 0.20f);
        var outline = Theme.Rgba(0.06f, 0.14f, 0.08f);
        var gold = Theme.ColU32(Theme.YellowMain);

        // Classic running cactuar arms!
        var armWiggle = defeated ? 0f : MathF.Sin(t * 6f) * 3f * s;
        dl.AddLine(c + new Vector2(-12f * s, -2f * s), c + new Vector2(-28f * s, -2f * s), green, 9f * s);
        dl.AddLine(c + new Vector2(-28f * s, -2f * s), c + new Vector2(-28f * s, -20f * s + armWiggle), green, 9f * s);
        dl.AddLine(c + new Vector2(12f * s, 2f * s), c + new Vector2(28f * s, 2f * s), green, 9f * s);
        dl.AddLine(c + new Vector2(28f * s, 2f * s), c + new Vector2(28f * s, 18f * s - armWiggle), green, 9f * s);

        // Tall body + spiky needles
        dl.AddRectFilled(c + new Vector2(-13f * s, -30f * s), c + new Vector2(13f * s, 28f * s), green, 12f * s);
        dl.AddRect(c + new Vector2(-13f * s, -30f * s), c + new Vector2(13f * s, 28f * s), outline, 12f * s, ImDrawFlags.None, 2.0f * s);
        dl.AddLine(c + new Vector2(0, -27f * s), c + new Vector2(0, 25f * s), darkGreen, 1.5f * s);

        // Golden mini crown on Jumbo Cactuar
        dl.AddTriangleFilled(c + new Vector2(-9f * s, -29f * s), c + new Vector2(-7f * s, -40f * s), c + new Vector2(-2f * s, -29f * s), gold);
        dl.AddTriangleFilled(c + new Vector2(-3f * s, -29f * s), c + new Vector2(0, -43f * s), c + new Vector2(3f * s, -29f * s), gold);
        dl.AddTriangleFilled(c + new Vector2(2f * s, -29f * s), c + new Vector2(7f * s, -40f * s), c + new Vector2(9f * s, -29f * s), gold);

        // Face (classic shocked O mouth + mustache!)
        if (defeated)
        {
            dl.AddLine(c + new Vector2(-8f * s, -14f * s), c + new Vector2(-3f * s, -9f * s), outline, 2.2f * s);
            dl.AddLine(c + new Vector2(-8f * s, -9f * s), c + new Vector2(-3f * s, -14f * s), outline, 2.2f * s);
            dl.AddLine(c + new Vector2(3f * s, -14f * s), c + new Vector2(8f * s, -9f * s), outline, 2.2f * s);
            dl.AddLine(c + new Vector2(3f * s, -9f * s), c + new Vector2(8f * s, -14f * s), outline, 2.2f * s);
        }
        else
        {
            dl.AddCircleFilled(c + new Vector2(-5.5f * s, -12f * s), 2.8f * s, outline);
            dl.AddCircleFilled(c + new Vector2(5.5f * s, -12f * s), 2.8f * s, outline);
        }
        // Dapper white mustache
        dl.AddLine(c + new Vector2(0, -4f * s), c + new Vector2(-11f * s, -1f * s), Theme.Rgba(0.98f, 0.98f, 1f), 3.0f * s);
        dl.AddLine(c + new Vector2(0, -4f * s), c + new Vector2(11f * s, -1f * s), Theme.Rgba(0.98f, 0.98f, 1f), 3.0f * s);
        dl.AddCircleFilled(c + new Vector2(0, 3f * s), 3.4f * s, outline);
    }

    private static void DrawKingMoggle(ImDrawListPtr dl, Vector2 c, float s, bool defeated, float t)
    {
        var white = Theme.Rgba(0.98f, 0.96f, 1.0f);
        var purple = Theme.ColU32(Theme.PurpleMain);
        var gold = Theme.ColU32(Theme.YellowMain);
        var outline = Theme.Rgba(0.12f, 0.03f, 0.22f);

        // Royal cape behind
        dl.AddTriangleFilled(c + new Vector2(-28f * s, -4f * s), c + new Vector2(28f * s, -4f * s), c + new Vector2(0, 36f * s), purple);
        // Bat wings flapping
        var flap = defeated ? 0f : MathF.Sin(t * 7f) * 5f * s;
        dl.AddTriangleFilled(c + new Vector2(-22f * s, 0), c + new Vector2(-44f * s, -18f * s + flap), c + new Vector2(-34f * s, 10f * s), Theme.Rgba(0.32f, 0.08f, 0.56f));
        dl.AddTriangleFilled(c + new Vector2(22f * s, 0), c + new Vector2(44f * s, -18f * s + flap), c + new Vector2(34f * s, 10f * s), Theme.Rgba(0.32f, 0.08f, 0.56f));

        // Round royal moogle body
        dl.AddCircleFilled(c + new Vector2(0, 4f * s), 27f * s, white);
        dl.AddCircle(c + new Vector2(0, 4f * s), 27f * s, outline, 28, 2.2f * s);

        // Golden pompom + crown!
        var pomY = -36f * s + MathF.Sin(t * 4.5f) * 2.5f * s;
        dl.AddLine(c + new Vector2(0, -21f * s), c + new Vector2(0, pomY), outline, 2.0f * s);
        dl.AddCircleFilled(c + new Vector2(0, pomY), 6.5f * s, gold);
        dl.AddRectFilled(c + new Vector2(-13f * s, -26f * s), c + new Vector2(13f * s, -19f * s), gold, 2f * s);
        dl.AddTriangleFilled(c + new Vector2(-13f * s, -26f * s), c + new Vector2(-9f * s, -35f * s), c + new Vector2(-4f * s, -26f * s), gold);
        dl.AddTriangleFilled(c + new Vector2(4f * s, -26f * s), c + new Vector2(9f * s, -35f * s), c + new Vector2(13f * s, -26f * s), gold);

        // Squinting moogle eyes + pink nose + fluffy beard
        dl.AddLine(c + new Vector2(-13f * s, -2f * s), c + new Vector2(-5f * s, -2f * s), outline, 2.4f * s);
        dl.AddLine(c + new Vector2(5f * s, -2f * s), c + new Vector2(13f * s, -2f * s), outline, 2.4f * s);
        dl.AddCircleFilled(c + new Vector2(0, 4f * s), 5.2f * s, Theme.Rgba(0.95f, 0.35f, 0.52f));
        dl.AddCircleFilled(c + new Vector2(-8f * s, 14f * s), 6f * s, Theme.Rgba(0.92f, 0.90f, 0.96f));
        dl.AddCircleFilled(c + new Vector2(0, 16f * s), 7f * s, Theme.Rgba(0.92f, 0.90f, 0.96f));
        dl.AddCircleFilled(c + new Vector2(8f * s, 14f * s), 6f * s, Theme.Rgba(0.92f, 0.90f, 0.96f));
    }

    private static void DrawKingBehemoth(ImDrawListPtr dl, Vector2 c, float s, bool defeated, float t)
    {
        var bodyCol = Theme.Rgba(0.28f, 0.14f, 0.48f);
        var maneCol = Theme.Rgba(0.48f, 0.24f, 0.78f);
        var hornCol = Theme.Rgba(0.96f, 0.86f, 0.56f);
        var outline = Theme.Rgba(0.08f, 0.02f, 0.16f);

        // Massive curved horns!
        dl.AddTriangleFilled(c + new Vector2(-16f * s, -16f * s), c + new Vector2(-40f * s, -36f * s), c + new Vector2(-24f * s, -6f * s), hornCol);
        dl.AddTriangle(c + new Vector2(-16f * s, -16f * s), c + new Vector2(-40f * s, -36f * s), c + new Vector2(-24f * s, -6f * s), outline, 1.8f * s);
        dl.AddTriangleFilled(c + new Vector2(16f * s, -16f * s), c + new Vector2(40f * s, -36f * s), c + new Vector2(24f * s, -6f * s), hornCol);
        dl.AddTriangle(c + new Vector2(16f * s, -16f * s), c + new Vector2(40f * s, -36f * s), c + new Vector2(24f * s, -6f * s), outline, 1.8f * s);

        // Spiky electric mane
        for (var i = 0; i < 8; i++)
        {
            var ang = i * (MathF.PI * 2f / 8f) + t * (defeated ? 0f : 0.4f);
            dl.AddCircleFilled(c + new Vector2(MathF.Cos(ang) * 26f * s, MathF.Sin(ang) * 24f * s + 2f * s), 9.5f * s, maneCol);
        }

        // Muscular body
        dl.AddCircleFilled(c + new Vector2(0, 4f * s), 26f * s, bodyCol);
        dl.AddCircle(c + new Vector2(0, 4f * s), 26f * s, outline, 28, 2.2f * s);

        // Fierce glowing red eyes + fangs
        var eyeCol = defeated ? Theme.Rgba(0.5f, 0.5f, 0.55f) : Theme.Rgba(1.0f, 0.24f, 0.30f);
        dl.AddCircleFilled(c + new Vector2(-10f * s, -2f * s), 4.2f * s, eyeCol);
        dl.AddCircleFilled(c + new Vector2(10f * s, -2f * s), 4.2f * s, eyeCol);
        dl.AddTriangleFilled(c + new Vector2(-8f * s, 10f * s), c + new Vector2(-4f * s, 18f * s), c + new Vector2(-1f * s, 10f * s), hornCol);
        dl.AddTriangleFilled(c + new Vector2(8f * s, 10f * s), c + new Vector2(4f * s, 18f * s), c + new Vector2(1f * s, 10f * s), hornCol);
    }

    private static void DrawUltimaChibi(ImDrawListPtr dl, Vector2 c, float s, bool defeated, float t)
    {
        var armor = Theme.Rgba(0.14f, 0.14f, 0.20f);
        var gold = Theme.ColU32(Theme.YellowMain);
        var cyan = Theme.ColU32(Theme.CyanAccent);
        var outline = Theme.Rgba(0.04f, 0.04f, 0.08f);

        // Shoulder wing pods
        dl.AddTriangleFilled(c + new Vector2(-18f * s, -8f * s), c + new Vector2(-42f * s, -28f * s), c + new Vector2(-28f * s, 12f * s), armor);
        dl.AddTriangle(c + new Vector2(-18f * s, -8f * s), c + new Vector2(-42f * s, -28f * s), c + new Vector2(-28f * s, 12f * s), gold, 1.8f * s);
        dl.AddTriangleFilled(c + new Vector2(18f * s, -8f * s), c + new Vector2(42f * s, -28f * s), c + new Vector2(28f * s, 12f * s), armor);
        dl.AddTriangle(c + new Vector2(18f * s, -8f * s), c + new Vector2(42f * s, -28f * s), c + new Vector2(28f * s, 12f * s), gold, 1.8f * s);

        // Armored core body
        dl.AddCircleFilled(c + new Vector2(0, 4f * s), 25f * s, armor);
        dl.AddCircle(c + new Vector2(0, 4f * s), 25f * s, gold, 28, 2.2f * s);

        // Glowing Allagan heart core
        var pulse = defeated ? 0.3f : (0.75f + 0.25f * MathF.Sin(t * 6f));
        dl.AddCircleFilled(c + new Vector2(0, 10f * s), 9f * s * pulse, cyan);
        dl.AddCircle(c + new Vector2(0, 10f * s), 10f * s, gold, 18, 1.6f * s);

        // Glowing visor eyes
        dl.AddRectFilled(c + new Vector2(-13f * s, -10f * s), c + new Vector2(13f * s, -3f * s), outline, 3f * s);
        if (!defeated)
            dl.AddLine(c + new Vector2(-10f * s, -6.5f * s), c + new Vector2(10f * s, -6.5f * s), cyan, 3.0f * s);
    }
}
