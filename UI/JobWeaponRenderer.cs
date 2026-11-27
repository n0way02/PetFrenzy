using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using PetFrenzy.Models;

namespace PetFrenzy.UI;

/// <summary>
/// Renders FFXIV Job weapons and DoH/DoL tools in two layers:
/// - DrawBack: strapped boldly & diagonally on the pet's back while out of combat / idle.
/// - DrawFront: unsheathed in front of the pet with live attack, cast, craft, or gather animations.
/// Supports authentic FFXIV Ultimate Weapon Skins (UCoB, UWU, TEA, DSR, TOP, FRU) with 60 FPS VFX!
/// </summary>
public static class JobWeaponRenderer
{
    public static void DrawBack(ImDrawListPtr dl, Vector2 petCenter, float s, JobWeaponStyle style, WeaponSkin skin, bool isDrawn, float t)
    {
        if (style == JobWeaponStyle.None) return;

        if (style == JobWeaponStyle.Nouliths)
        {
            DrawNouliths(dl, petCenter, s, skin, isDrawn, t);
            return;
        }

        if (isDrawn) return;

        var bs = s * 1.48f;
        var isSheathedSword = style is JobWeaponStyle.Greatsword
            or JobWeaponStyle.Gunblade
            or JobWeaponStyle.SwordShield
            or JobWeaponStyle.Katana
            or JobWeaponStyle.RapierFocus
            or JobWeaponStyle.TwinBlades
            or JobWeaponStyle.TwinDaggers;

        var anchor = style switch
        {
            JobWeaponStyle.Book or JobWeaponStyle.StarGlobe or JobWeaponStyle.Chakrams or JobWeaponStyle.Potion
                => petCenter + new Vector2(-40f * s, -10f * s),
            JobWeaponStyle.Fists
                => petCenter + new Vector2(-36f * s, 4f * s),
            _ when isSheathedSword
                => petCenter + new Vector2(-4f * s, -8f * s),
            _ => petCenter + new Vector2(-8f * s, -5f * s),
        };

        // Swords on the back hang hilt-up / blade-down (just like in FFXIV!)
        var angle = isSheathedSword ? (MathF.PI - 0.64f) : -0.64f;
        DrawWeaponShape(dl, anchor, bs, style, skin, angle: angle, drawn: false, t);
    }

    public static void DrawFront(ImDrawListPtr dl, Vector2 petCenter, float s, JobWeaponStyle style, WeaponSkin skin, bool isDrawn, float t)
    {
        if (style == JobWeaponStyle.None || !isDrawn) return;
        if (style == JobWeaponStyle.Nouliths) return;

        switch (style)
        {
            case JobWeaponStyle.SwordShield:
            {
                var swing = MathF.Sin(t * 8.5f) * 0.55f;
                DrawWeaponShape(dl, petCenter + new Vector2(26f * s, 8f * s), s * 1.1f, style, skin, angle: -0.35f + swing, drawn: true, t);
                DrawShield(dl, petCenter + new Vector2(-25f * s, 10f * s + MathF.Sin(t * 5f) * 1.5f * s), s * 1.1f, skin, t);
                return;
            }
            case JobWeaponStyle.Fists:
            {
                var punchL = MathF.Max(0f, MathF.Sin(t * 10f)) * 9f * s;
                var punchR = MathF.Max(0f, -MathF.Sin(t * 10f)) * 9f * s;
                DrawFistKnuckle(dl, petCenter + new Vector2(-24f * s - punchL, 10f * s), s * 1.1f, skin, t);
                DrawFistKnuckle(dl, petCenter + new Vector2(24f * s + punchR, 10f * s), s * 1.1f, skin, t);
                return;
            }
            case JobWeaponStyle.TwinDaggers:
            case JobWeaponStyle.TwinBlades:
            {
                var swL = MathF.Sin(t * 10f) * 0.65f;
                var swR = MathF.Cos(t * 10f) * 0.65f;
                DrawWeaponShape(dl, petCenter + new Vector2(-24f * s, 10f * s), s * 1.1f, style, skin, angle: -0.8f + swL, drawn: true, t);
                DrawWeaponShape(dl, petCenter + new Vector2(24f * s, 10f * s), s * 1.1f, style, skin, angle: 0.45f + swR, drawn: true, t);
                return;
            }
            case JobWeaponStyle.Chakrams:
            {
                DrawChakram(dl, petCenter + new Vector2(-26f * s, 8f * s + MathF.Sin(t * 7f) * 4f * s), s * 1.1f, skin, t * 7f);
                DrawChakram(dl, petCenter + new Vector2(26f * s, 8f * s - MathF.Sin(t * 7f) * 4f * s), s * 1.1f, skin, -t * 7f);
                return;
            }
            case JobWeaponStyle.Lance:
            {
                var thrust = MathF.Sin(t * 9f) * 7f * s;
                DrawWeaponShape(dl, petCenter + new Vector2(22f * s + thrust, 8f * s), s * 1.12f, style, skin, angle: 1.32f, drawn: true, t);
                return;
            }
            case JobWeaponStyle.Bow:
            case JobWeaponStyle.Gun:
            {
                var recoil = MathF.Max(0f, MathF.Sin(t * 7f)) * 3f * s;
                DrawWeaponShape(dl, petCenter + new Vector2(24f * s - recoil, 8f * s), s * 1.12f, style, skin, angle: 0.05f, drawn: true, t);
                return;
            }
            case JobWeaponStyle.FishingRod:
            {
                var bob = MathF.Sin(t * 2.6f) * 0.12f;
                DrawWeaponShape(dl, petCenter + new Vector2(22f * s, 10f * s), s * 1.1f, style, skin, angle: 0.55f + bob, drawn: true, t);
                return;
            }
            case JobWeaponStyle.Hammer:
            case JobWeaponStyle.Pickaxe:
            case JobWeaponStyle.Hatchet:
            {
                var chop = MathF.Sin(t * 7.5f) * 0.72f;
                DrawWeaponShape(dl, petCenter + new Vector2(24f * s, 8f * s), s * 1.1f, style, skin, angle: 0.15f + chop, drawn: true, t);
                return;
            }
            case JobWeaponStyle.FryingPan:
            {
                var toss = MathF.Sin(t * 6.5f) * 0.28f;
                DrawWeaponShape(dl, petCenter + new Vector2(24f * s, 12f * s), s * 1.1f, style, skin, angle: 1.15f + toss, drawn: true, t);
                return;
            }
            case JobWeaponStyle.Paintbrush:
            {
                var stroke = MathF.Sin(t * 6.5f) * 0.55f;
                DrawWeaponShape(dl, petCenter + new Vector2(24f * s, 8f * s), s * 1.1f, style, skin, angle: 0.25f + stroke, drawn: true, t);
                return;
            }
            default:
            {
                var swing = MathF.Sin(t * 7.5f) * 0.45f;
                DrawWeaponShape(dl, petCenter + new Vector2(24f * s, 8f * s), s * 1.12f, style, skin, angle: 0.15f + swing, drawn: true, t);
                return;
            }
        }
    }

    private static Vector2 R(Vector2 origin, float lx, float ly, float angle, float s)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return origin + new Vector2((lx * cos - ly * sin) * s, (lx * sin + ly * cos) * s);
    }

    private static void DrawUltimateAura(ImDrawListPtr dl, Vector2 o, float angle, float s, WeaponSkin skin, bool drawn, float t, float topY = -30f, float botY = 10f)
    {
        if (skin == WeaponSkin.Default) return;

        var info = WeaponSkinCatalog.Get(skin);
        var pulse = 0.5f + 0.5f * MathF.Sin(t * 5.2f);
        // TOP is famously subtle when sheathed and explodes when drawn!
        var intensity = drawn ? 1.0f : (skin == WeaponSkin.TOP ? 0.35f : 0.62f);

        var pTop = R(o, 0, topY, angle, s);
        var pBot = R(o, 0, botY, angle, s);
        var pMid = (pTop + pBot) * 0.5f;

        // Soft outer glow sheath along the weapon axis
        var glow1 = Theme.Rgba(info.GlowPrimary.X, info.GlowPrimary.Y, info.GlowPrimary.Z, (0.20f + pulse * 0.14f) * intensity);
        var glow2 = Theme.Rgba(info.GlowSecondary.X, info.GlowSecondary.Y, info.GlowSecondary.Z, (0.28f + pulse * 0.16f) * intensity);
        dl.AddLine(pBot, pTop, glow1, (14f + pulse * 3f) * s);
        dl.AddLine(pBot, pTop, glow2, (7.5f + pulse * 1.5f) * s);

        // Signature per-Ultimate particle / geometric VFX
        switch (skin)
        {
            case WeaponSkin.UCoB:
            {
                // Dreadwyrm golden-amber embers drifting off the blade
                var count = drawn ? 6 : 3;
                for (var i = 0; i < count; i++)
                {
                    var frac = ((t * 0.9f + i * (1f / count)) % 1f);
                    var along = Vector2.Lerp(pBot, pTop, frac);
                    var drift = new Vector2(MathF.Sin(t * 4f + i * 2f) * 7f * s, -frac * 8f * s);
                    var col = i % 2 == 0
                        ? Theme.Rgba(1.0f, 0.84f, 0.18f, (1f - frac) * intensity)
                        : Theme.Rgba(1.0f, 0.48f, 0.08f, (1f - frac) * intensity);
                    dl.AddCircleFilled(along + drift, (2.4f - frac * 1.1f) * s, col);
                }
                break;
            }
            case WeaponSkin.UWU:
            {
                // Radiant white / soft sky-blue cascading aether sparkles
                var count = drawn ? 6 : 3;
                for (var i = 0; i < count; i++)
                {
                    var frac = (t * 0.75f + i * (1f / count)) % 1f;
                    var along = Vector2.Lerp(pTop, pBot, frac);
                    var off = new Vector2(MathF.Cos(t * 3.5f + i * 2.2f) * 8f * s, MathF.Sin(t * 3.5f + i) * 5f * s);
                    var pt = along + off;
                    var a = MathF.Sin(frac * MathF.PI) * intensity;
                    var col = i % 2 == 0 ? Theme.Rgba(1f, 1f, 1f, a) : Theme.Rgba(0.55f, 0.88f, 1f, a);
                    dl.AddLine(pt + new Vector2(-3f * s, 0), pt + new Vector2(3f * s, 0), col, 1.3f * s);
                    dl.AddLine(pt + new Vector2(0, -3f * s), pt + new Vector2(0, 3f * s), col, 1.3f * s);
                }
                break;
            }
            case WeaponSkin.TEA:
            {
                // Electric blue + golden-orange spinning Alexander mechanical cog ring!
                var ringCol = Theme.Rgba(0.18f, 0.78f, 1f, 0.72f * intensity);
                var gearCol = Theme.Rgba(1f, 0.68f, 0.14f, 0.80f * intensity);
                var rad = (drawn ? 11f : 8.5f) * s;
                dl.AddCircle(pMid, rad, ringCol, 20, 1.6f * s);
                for (var i = 0; i < 6; i++)
                {
                    var a = t * 2.4f + i * (MathF.PI / 3f);
                    var inner = pMid + new Vector2(MathF.Cos(a) * (rad - 1.5f * s), MathF.Sin(a) * (rad - 1.5f * s));
                    var outer = pMid + new Vector2(MathF.Cos(a) * (rad + 3.2f * s), MathF.Sin(a) * (rad + 3.2f * s));
                    dl.AddLine(inner, outer, gearCol, 2.0f * s);
                }
                break;
            }
            case WeaponSkin.DSR:
            {
                // Thordan holy sky-blue & gold orbiting diamond runes
                var count = drawn ? 4 : 2;
                for (var i = 0; i < count; i++)
                {
                    var a = t * 2.2f + i * (MathF.PI * 2f / count);
                    var rp = pMid + new Vector2(MathF.Cos(a) * 13f * s, MathF.Sin(a) * 10f * s);
                    var col = i % 2 == 0 ? Theme.Rgba(0.50f, 0.84f, 1f, 0.9f * intensity) : Theme.Rgba(1f, 0.88f, 0.36f, 0.9f * intensity);
                    dl.AddQuadFilled(
                        rp + new Vector2(0, -3.5f * s),
                        rp + new Vector2(2.6f * s, 0),
                        rp + new Vector2(0, 3.5f * s),
                        rp + new Vector2(-2.6f * s, 0),
                        col);
                }
                break;
            }
            case WeaponSkin.TOP:
            {
                // Cybernetic magenta/red Omega digital grid segments & glitch bars
                var count = drawn ? 5 : 2;
                for (var i = 0; i < count; i++)
                {
                    var phase = (t * 2.8f + i * 0.7f) % 1f;
                    var pt = Vector2.Lerp(pBot, pTop, (i + 0.5f) / count);
                    var jitter = new Vector2(MathF.Sin(t * 18f + i * 3f) * 6f * s, 0);
                    var col = i % 2 == 0
                        ? Theme.Rgba(1f, 0.20f, 0.68f, (0.9f - phase * 0.5f) * intensity)
                        : Theme.Rgba(1f, 0.16f, 0.28f, (0.9f - phase * 0.5f) * intensity);
                    dl.AddLine(pt + jitter - new Vector2(5f * s, 0), pt + jitter + new Vector2(5f * s, 0), col, 1.8f * s);
                }
                break;
            }
            case WeaponSkin.FRU:
            {
                // Ryne (icy white/cyan) & Gaia (deep void violet) dual-tone crystal rings + stardust
                var ryneCol = Theme.Rgba(0.90f, 0.97f, 1f, 0.82f * intensity);
                var gaiaCol = Theme.Rgba(0.54f, 0.28f, 1f, 0.82f * intensity);
                dl.AddCircle(pTop, (6.5f + pulse * 2f) * s, ryneCol, 18, 1.5f * s);
                dl.AddCircle(pMid, (9.5f + (1f - pulse) * 2f) * s, gaiaCol, 18, 1.5f * s);
                var count = drawn ? 6 : 3;
                for (var i = 0; i < count; i++)
                {
                    var a = t * 2.5f + i * (MathF.PI * 2f / count);
                    var sp = pMid + new Vector2(MathF.Cos(a) * 14f * s, MathF.Sin(a) * 14f * s);
                    dl.AddCircleFilled(sp, 2.0f * s, i % 2 == 0 ? ryneCol : gaiaCol);
                }
                break;
            }
        }
    }

    private static void DrawWeaponShape(ImDrawListPtr dl, Vector2 o, float s, JobWeaponStyle style, WeaponSkin skin, float angle, bool drawn, float t)
    {
        var skinInfo = WeaponSkinCatalog.Get(skin);
        var isUlt = skin != WeaponSkin.Default;

        var dark = Theme.Rgba(0.08f, 0.02f, 0.16f);
        var silver = isUlt ? Theme.ColU32(skinInfo.MetalColor) : Theme.Rgba(0.88f, 0.90f, 0.95f);
        var steel = isUlt ? Theme.ColU32(skinInfo.MetalColor) : Theme.Rgba(0.65f, 0.68f, 0.76f);
        var gold = isUlt ? Theme.ColU32(skinInfo.AccentColor) : Theme.ColU32(Theme.YellowMain);
        var wood = isUlt ? Theme.Rgba(0.22f, 0.18f, 0.28f) : Theme.Rgba(0.58f, 0.36f, 0.20f);
        var crimson = isUlt ? Theme.ColU32(skinInfo.GlowPrimary) : Theme.Rgba(0.92f, 0.22f, 0.30f);
        var purple = isUlt ? Theme.ColU32(skinInfo.GlowSecondary) : Theme.ColU32(Theme.PurpleBright);
        var cyan = isUlt ? Theme.ColU32(skinInfo.GlowPrimary) : Theme.ColU32(Theme.CyanAccent);
        var edgeGlow = isUlt ? Theme.ColU32(skinInfo.GlowPrimary) : Theme.Rgba(1f, 1f, 1f, 0.55f);

        // Render Ultimate glow & particles behind/around the weapon
        if (isUlt && style is not (JobWeaponStyle.Book or JobWeaponStyle.StarGlobe or JobWeaponStyle.Chakrams or JobWeaponStyle.Fists or JobWeaponStyle.Potion))
        {
            DrawUltimateAura(dl, o, angle, s, skin, drawn, t);
        }

        switch (style)
        {
            case JobWeaponStyle.AxeShield:
            {
                // Beastmaster one-handed battle axe + round buckler shield
                dl.AddLine(R(o, 0, 14, angle, s), R(o, 0, -24, angle, s), wood, 3.2f * s);
                dl.AddQuadFilled(
                    R(o, 1, -22, angle, s), R(o, 14, -28, angle, s),
                    R(o, 15, -10, angle, s), R(o, 1, -13, angle, s), silver);
                dl.AddLine(R(o, 14, -28, angle, s), R(o, 15, -10, angle, s), edgeGlow, 2.0f * s);
                dl.AddQuad(
                    R(o, 1, -22, angle, s), R(o, 14, -28, angle, s),
                    R(o, 15, -10, angle, s), R(o, 1, -13, angle, s), dark, 1.4f * s);
                // Round Beastmaster buckler
                var shPos = R(o, -9, -2, angle, s);
                dl.AddCircleFilled(shPos, 8.5f * s, wood);
                dl.AddCircle(shPos, 8.5f * s, gold, 18, 2.2f * s);
                dl.AddCircleFilled(shPos, 2.8f * s, gold);
                break;
            }
            case JobWeaponStyle.Lance:
            {
                dl.AddLine(R(o, 0, 26, angle, s), R(o, 0, -24, angle, s), wood, 3.2f * s);
                // Ornate lance wings + sharp spearhead
                dl.AddTriangleFilled(R(o, -8.5f, -20, angle, s), R(o, 0, -27, angle, s), R(o, 8.5f, -20, angle, s), gold);
                dl.AddTriangleFilled(R(o, -6f, -23, angle, s), R(o, 0, -44, angle, s), R(o, 6f, -23, angle, s), silver);
                dl.AddLine(R(o, 0, -24, angle, s), R(o, 0, -42, angle, s), edgeGlow, 1.8f * s);
                dl.AddTriangle(R(o, -6f, -23, angle, s), R(o, 0, -44, angle, s), R(o, 6f, -23, angle, s), dark, 1.4f * s);
                break;
            }
            case JobWeaponStyle.SwordShield:
            {
                dl.AddLine(R(o, 0, 15, angle, s), R(o, 0, 4, angle, s), wood, 3.2f * s);
                dl.AddCircleFilled(R(o, 0, 16, angle, s), 2.8f * s, gold);
                dl.AddLine(R(o, -8, 4, angle, s), R(o, 8, 4, angle, s), gold, 3.2f * s);
                dl.AddQuadFilled(R(o, -3.4f, 3, angle, s), R(o, 3.4f, 3, angle, s), R(o, 2.4f, -25, angle, s), R(o, -2.4f, -25, angle, s), silver);
                dl.AddTriangleFilled(R(o, -2.4f, -25, angle, s), R(o, 0, -33, angle, s), R(o, 2.4f, -25, angle, s), silver);
                dl.AddLine(R(o, 0, 2, angle, s), R(o, 0, -30, angle, s), edgeGlow, 1.6f * s);
                if (!drawn)
                    DrawShield(dl, o + new Vector2(12f * s, 6f * s), s * 0.85f, skin, t);
                break;
            }
            case JobWeaponStyle.Greataxe:
            {
                dl.AddLine(R(o, 0, 25, angle, s), R(o, 0, -27, angle, s), wood, 3.6f * s);
                dl.AddQuadFilled(R(o, -2, -23, angle, s), R(o, -16, -30, angle, s), R(o, -16, -9, angle, s), R(o, -2, -14, angle, s), steel);
                dl.AddQuadFilled(R(o, 2, -23, angle, s), R(o, 16, -30, angle, s), R(o, 16, -9, angle, s), R(o, 2, -14, angle, s), steel);
                dl.AddLine(R(o, -16, -30, angle, s), R(o, -16, -9, angle, s), crimson, 2.6f * s);
                dl.AddLine(R(o, 16, -30, angle, s), R(o, 16, -9, angle, s), crimson, 2.6f * s);
                dl.AddCircleFilled(R(o, 0, -18, angle, s), 3.2f * s, gold);
                break;
            }
            case JobWeaponStyle.Greatsword:
            {
                dl.AddLine(R(o, 0, 23, angle, s), R(o, 0, 8, angle, s), dark, 4f * s);
                dl.AddCircleFilled(R(o, 0, 24, angle, s), 3.2f * s, gold);
                dl.AddLine(R(o, -11, 8, angle, s), R(o, 11, 8, angle, s), gold, 4.0f * s);
                dl.AddQuadFilled(R(o, -5.5f, 7, angle, s), R(o, 5.5f, 7, angle, s), R(o, 4.2f, -31, angle, s), R(o, -4.2f, -31, angle, s), isUlt ? silver : Theme.Rgba(0.28f, 0.22f, 0.36f));
                dl.AddTriangleFilled(R(o, -4.2f, -31, angle, s), R(o, 0, -41, angle, s), R(o, 4.2f, -31, angle, s), purple);
                dl.AddLine(R(o, 0, 6, angle, s), R(o, 0, -35, angle, s), crimson, 2.2f * s);
                break;
            }
            case JobWeaponStyle.Gunblade:
            {
                dl.AddLine(R(o, 0, 15, angle, s), R(o, 0, 4, angle, s), dark, 3.6f * s);
                dl.AddCircleFilled(R(o, 2.5f, 2, angle, s), 4.8f * s, gold);
                dl.AddQuadFilled(R(o, -3.2f, 0, angle, s), R(o, 4.2f, 0, angle, s), R(o, 3.2f, -29, angle, s), R(o, -2.2f, -34, angle, s), silver);
                dl.AddLine(R(o, -2.6f, 0, angle, s), R(o, -2.2f, -34, angle, s), edgeGlow, 2.0f * s);
                break;
            }
            case JobWeaponStyle.WhiteStaff:
            {
                dl.AddLine(R(o, 0, 25, angle, s), R(o, 0, -24, angle, s), isUlt ? silver : Theme.Rgba(0.95f, 0.93f, 0.88f), 3.2f * s);
                dl.AddCircleFilled(R(o, 0, -29, angle, s), 8f * s, isUlt ? silver : Theme.Rgba(0.98f, 0.96f, 0.90f));
                dl.AddCircleFilled(R(o, 0, -29, angle, s), 4.5f * s, isUlt ? crimson : Theme.ColU32(Theme.GreenAccent));
                dl.AddCircle(R(o, 0, -29, angle, s), 8f * s, gold, 16, 2.0f * s);
                break;
            }
            case JobWeaponStyle.BlackRod:
            {
                dl.AddLine(R(o, 0, 25, angle, s), R(o, 0, -24, angle, s), dark, 3.6f * s);
                var glow = 0.5f + 0.5f * MathF.Sin(t * 7f);
                dl.AddCircle(R(o, 0, -30, angle, s), 8.5f * s, purple, 18, 2.4f * s);
                dl.AddCircleFilled(R(o, 0, -30, angle, s), (4.2f + glow * 2f) * s, isUlt ? crimson : Theme.Rgba(1f, 0.45f, 0.12f));
                break;
            }
            case JobWeaponStyle.Book:
            {
                var bCenter = o + new Vector2(0, MathF.Sin(t * 5f) * 3f * s);
                if (isUlt) DrawUltimateAura(dl, bCenter, 0f, s * 0.7f, skin, drawn, t, -10f, 10f);
                dl.AddRectFilled(bCenter + new Vector2(-10f * s, -8f * s), bCenter + new Vector2(10f * s, 8f * s), purple, 2f * s);
                dl.AddRectFilled(bCenter + new Vector2(-8f * s, -6f * s), bCenter + new Vector2(8f * s, 6f * s), Theme.Rgba(0.98f, 0.95f, 0.86f), 1f * s);
                dl.AddLine(bCenter + new Vector2(0, -6f * s), bCenter + new Vector2(0, 6f * s), gold, 1.8f * s);
                dl.AddRect(bCenter + new Vector2(-10f * s, -8f * s), bCenter + new Vector2(10f * s, 8f * s), gold, 2f * s, ImDrawFlags.None, 1.6f * s);
                break;
            }
            case JobWeaponStyle.StarGlobe:
            {
                var gc = o + new Vector2(0, MathF.Sin(t * 4f) * 3f * s);
                if (isUlt) DrawUltimateAura(dl, gc, 0f, s * 0.7f, skin, drawn, t, -10f, 10f);
                dl.AddCircle(gc, 10f * s, gold, 20, 2.0f * s);
                dl.AddCircleFilled(gc, 4.5f * s, cyan);
                var orb = gc + new Vector2(MathF.Cos(t * 4.5f) * 12f * s, MathF.Sin(t * 4.5f) * 6f * s);
                dl.AddRectFilled(orb + new Vector2(-2.5f * s, -4f * s), orb + new Vector2(2.5f * s, 4f * s), Theme.Rgba(1f, 0.96f, 0.85f));
                break;
            }
            case JobWeaponStyle.Fists:
            {
                DrawFistKnuckle(dl, o, s * 0.95f, skin, t);
                break;
            }
            case JobWeaponStyle.TwinDaggers:
            case JobWeaponStyle.TwinBlades:
            {
                var len = style == JobWeaponStyle.TwinBlades ? 24f : 18f;
                dl.AddLine(R(o, 0, 9, angle, s), R(o, 0, 2, angle, s), dark, 2.8f * s);
                dl.AddLine(R(o, -4.5f, 2, angle, s), R(o, 4.5f, 2, angle, s), gold, 2.4f * s);
                dl.AddTriangleFilled(R(o, -3.2f, 2, angle, s), R(o, 0, -len, angle, s), R(o, 3.2f, 2, angle, s), silver);
                dl.AddLine(R(o, 0, 1, angle, s), R(o, 0, -len + 2f, angle, s), edgeGlow, 1.4f * s);
                break;
            }
            case JobWeaponStyle.Katana:
            {
                dl.AddLine(R(o, 0, 19, angle, s), R(o, 0, 6, angle, s), crimson, 3.2f * s);
                dl.AddLine(R(o, -5.5f, 6, angle, s), R(o, 5.5f, 6, angle, s), gold, 2.6f * s);
                dl.AddLine(R(o, 0, 6, angle, s), R(o, -2.5f, -30, angle, s), silver, 3.0f * s);
                dl.AddLine(R(o, 0.5f, 5, angle, s), R(o, -2f, -30, angle, s), edgeGlow, 1.3f * s);
                break;
            }
            case JobWeaponStyle.Scythe:
            {
                // Long dark reaper shaft + bottom counterweight spike + grip wraps
                dl.AddLine(R(o, 0, 25, angle, s), R(o, 0, -25, angle, s), dark, 3.2f * s);
                dl.AddTriangleFilled(R(o, -2.2f, 24, angle, s), R(o, 2.2f, 24, angle, s), R(o, 0, 31, angle, s), steel);
                dl.AddLine(R(o, -2.0f, 10, angle, s), R(o, 2.0f, 12, angle, s), crimson, 1.8f * s);
                dl.AddLine(R(o, -2.0f, 4, angle, s), R(o, 2.0f, 6, angle, s), crimson, 1.8f * s);

                // Ornate collar + wicked back-spike on the opposite side of the blade
                dl.AddTriangleFilled(R(o, -1, -27, angle, s), R(o, -10, -29, angle, s), R(o, -1, -22, angle, s), steel);
                dl.AddLine(R(o, -1, -27, angle, s), R(o, -10, -29, angle, s), silver, 1.2f * s);

                // Sweeping curved crescent scythe blade (3 segments tapering to a downward-hooked tip!)
                var bO0 = R(o, 0, -27, angle, s);
                var bI0 = R(o, 2, -20.5f, angle, s);
                var bO1 = R(o, 13, -30, angle, s);
                var bI1 = R(o, 11.5f, -24, angle, s);
                var bO2 = R(o, 24.5f, -22.5f, angle, s);
                var bI2 = R(o, 19.5f, -18.5f, angle, s);
                var bTip = R(o, 26.5f, -6.5f, angle, s);

                // Dark metallic blade body
                dl.AddQuadFilled(bO0, bO1, bI1, bI0, steel);
                dl.AddQuadFilled(bO1, bO2, bI2, bI1, steel);
                dl.AddTriangleFilled(bO2, bTip, bI2, steel);

                // Dark spine along the outer curve
                dl.AddLine(bO0, bO1, dark, 2.4f * s);
                dl.AddLine(bO1, bO2, dark, 2.2f * s);
                dl.AddLine(bO2, bTip, dark, 1.8f * s);

                // Razor-sharp glowing inner cutting edge (silver + crimson/void sheen)
                var bladeEdge = skin == WeaponSkin.Default ? crimson : edgeGlow;
                dl.AddLine(bI0, bI1, bladeEdge, 2.2f * s);
                dl.AddLine(bI1, bI2, bladeEdge, 2.0f * s);
                dl.AddLine(bI2, bTip, bladeEdge, 1.8f * s);
                dl.AddLine(bI0, bI1, silver, 1.0f * s);
                dl.AddLine(bI1, bI2, silver, 1.0f * s);
                dl.AddLine(bI2, bTip, silver, 0.9f * s);

                // Glowing void crystal core at the scythe collar
                dl.AddCircleFilled(R(o, 0, -24.5f, angle, s), 3.3f * s, dark);
                dl.AddCircleFilled(R(o, 0, -24.5f, angle, s), 2.1f * s, bladeEdge);
                break;
            }
            case JobWeaponStyle.Bow:
            {
                dl.AddLine(R(o, -4, -23, angle, s), R(o, -4, 23, angle, s), Theme.Rgba(0.95f, 0.95f, 1f, 0.75f), 1.3f * s);
                dl.AddLine(R(o, -4, -23, angle, s), R(o, 11, -10, angle, s), gold, 3.2f * s);
                dl.AddLine(R(o, 11, -10, angle, s), R(o, 11, 10, angle, s), gold, 3.4f * s);
                dl.AddLine(R(o, 11, 10, angle, s), R(o, -4, 23, angle, s), gold, 3.2f * s);
                if (drawn)
                    dl.AddLine(R(o, -6, 0, angle, s), R(o, 17, 0, angle, s), edgeGlow, 2.2f * s);
                break;
            }
            case JobWeaponStyle.Gun:
            {
                dl.AddQuadFilled(R(o, -6, -4.5f, angle, s), R(o, 17, -4.5f, angle, s), R(o, 17, 2, angle, s), R(o, -6, 2, angle, s), steel);
                dl.AddLine(R(o, -5, -1, angle, s), R(o, 16, -1, angle, s), edgeGlow, 1.5f * s);
                dl.AddLine(R(o, -4, 2, angle, s), R(o, -8, 12, angle, s), wood, 4.0f * s);
                if (drawn && MathF.Sin(t * 14f) > 0.65f)
                    dl.AddCircleFilled(R(o, 20, -1, angle, s), 4.8f * s, gold);
                break;
            }
            case JobWeaponStyle.Chakrams:
            {
                DrawChakram(dl, o, s * 0.95f, skin, t * 2f);
                break;
            }
            case JobWeaponStyle.RapierFocus:
            {
                dl.AddLine(R(o, 0, 15, angle, s), R(o, 0, -30, angle, s), silver, 2.4f * s);
                dl.AddCircle(R(o, 0, 6, angle, s), 5f * s, gold, 14, 2.2f * s);
                if (drawn)
                {
                    var fPos = o + new Vector2(-44f * s, -4f * s + MathF.Sin(t * 6f) * 3f * s);
                    dl.AddCircleFilled(fPos, 5.5f * s, crimson);
                    dl.AddCircle(fPos, 8.5f * s, gold, 14, 1.5f * s);
                }
                break;
            }
            case JobWeaponStyle.BlueCane:
            {
                dl.AddLine(R(o, 0, 23, angle, s), R(o, 0, -23, angle, s), isUlt ? silver : Theme.Rgba(0.18f, 0.48f, 0.96f), 3.4f * s);
                dl.AddCircleFilled(R(o, 0, -26, angle, s), 5.8f * s, cyan);
                break;
            }
            case JobWeaponStyle.Paintbrush:
            {
                dl.AddLine(R(o, 0, 24, angle, s), R(o, 0, -18, angle, s), wood, 3.6f * s);
                dl.AddQuadFilled(R(o, -3.5f, -16, angle, s), R(o, 3.5f, -16, angle, s), R(o, 3.5f, -22, angle, s), R(o, -3.5f, -22, angle, s), gold);
                dl.AddTriangleFilled(R(o, -6f, -22, angle, s), R(o, 0, -35, angle, s), R(o, 6f, -22, angle, s), isUlt ? crimson : Theme.ColU32(Theme.PinkAccent));
                if (drawn)
                {
                    dl.AddCircleFilled(R(o, 4, -37, angle, s), 3f * s, cyan);
                    dl.AddCircleFilled(R(o, -5, -34, angle, s), 2.5f * s, gold);
                }
                break;
            }
            // === CRAFTERS (DoH) ===
            case JobWeaponStyle.Hammer:
            {
                dl.AddLine(R(o, 0, 18, angle, s), R(o, 0, -16, angle, s), wood, 3.4f * s);
                dl.AddQuadFilled(R(o, -10, -22, angle, s), R(o, 10, -22, angle, s), R(o, 10, -12, angle, s), R(o, -10, -12, angle, s), steel);
                dl.AddQuad(R(o, -10, -22, angle, s), R(o, 10, -22, angle, s), R(o, 10, -12, angle, s), R(o, -10, -12, angle, s), dark, 1.5f * s);
                break;
            }
            case JobWeaponStyle.Saw:
            {
                dl.AddLine(R(o, 0, 16, angle, s), R(o, 0, 8, angle, s), wood, 4.5f * s);
                dl.AddQuadFilled(R(o, -4, 8, angle, s), R(o, 4, 8, angle, s), R(o, 2.5f, -24, angle, s), R(o, -4, -24, angle, s), silver);
                break;
            }
            case JobWeaponStyle.Knife:
            {
                dl.AddLine(R(o, 0, 14, angle, s), R(o, 0, 2, angle, s), wood, 3.2f * s);
                dl.AddCircleFilled(R(o, 0, -6, angle, s), 9f * s, silver);
                break;
            }
            case JobWeaponStyle.Needle:
            {
                dl.AddLine(R(o, 0, 16, angle, s), R(o, 0, -24, angle, s), gold, 2.4f * s);
                dl.AddCircle(R(o, 0, 14, angle, s), 3.2f * s, gold, 12, 1.6f * s);
                dl.AddLine(R(o, 2, 14, angle, s), R(o, 12, 20 + MathF.Sin(t * 6f) * 4f, angle, s), Theme.ColU32(Theme.PinkAccent), 1.8f * s);
                break;
            }
            case JobWeaponStyle.Potion:
            {
                var pc = o + new Vector2(0, MathF.Sin(t * 6f) * 2.5f * s);
                if (isUlt) DrawUltimateAura(dl, pc, 0f, s * 0.7f, skin, drawn, t, -8f, 8f);
                dl.AddCircleFilled(pc, 9f * s, isUlt ? crimson : Theme.ColU32(Theme.GreenAccent));
                dl.AddCircle(pc, 9f * s, silver, 18, 2f * s);
                dl.AddRectFilled(pc + new Vector2(-3f * s, -14f * s), pc + new Vector2(3f * s, -8f * s), wood);
                break;
            }
            case JobWeaponStyle.FryingPan:
            {
                dl.AddLine(R(o, 0, 16, angle, s), R(o, 0, 2, angle, s), dark, 3.5f * s);
                dl.AddCircleFilled(R(o, 0, -9, angle, s), 11f * s, Theme.Rgba(0.22f, 0.22f, 0.28f));
                dl.AddCircle(R(o, 0, -9, angle, s), 11f * s, silver, 20, 1.8f * s);
                if (drawn)
                    dl.AddCircleFilled(R(o, -2, -10, angle, s), 3.5f * s, gold);
                break;
            }
            // === GATHERERS (DoL) ===
            case JobWeaponStyle.Pickaxe:
            {
                dl.AddLine(R(o, 0, 20, angle, s), R(o, 0, -18, angle, s), wood, 3.4f * s);
                dl.AddLine(R(o, -15, -12, angle, s), R(o, 0, -20, angle, s), steel, 3.6f * s);
                dl.AddLine(R(o, 0, -20, angle, s), R(o, 15, -12, angle, s), steel, 3.6f * s);
                break;
            }
            case JobWeaponStyle.Hatchet:
            {
                dl.AddLine(R(o, 0, 20, angle, s), R(o, 0, -18, angle, s), wood, 3.4f * s);
                dl.AddQuadFilled(R(o, 0, -20, angle, s), R(o, 13, -24, angle, s), R(o, 13, -10, angle, s), R(o, 0, -12, angle, s), silver);
                break;
            }
            case JobWeaponStyle.FishingRod:
            {
                dl.AddLine(R(o, 0, 18, angle, s), R(o, 0, -30, angle, s), wood, 2.8f * s);
                var tip = R(o, 0, -30, angle, s);
                if (drawn)
                {
                    var bobberY = 26f * s + MathF.Sin(t * 4f) * 3f * s;
                    dl.AddLine(tip, tip + new Vector2(8f * s, bobberY), Theme.Rgba(0.92f, 0.95f, 1f, 0.8f), 1.3f * s);
                    dl.AddCircleFilled(tip + new Vector2(8f * s, bobberY), 3.5f * s, crimson);
                }
                break;
            }
        }
    }

    private static void DrawShield(ImDrawListPtr dl, Vector2 c, float s, WeaponSkin skin, float t)
    {
        var info = WeaponSkinCatalog.Get(skin);
        var isUlt = skin != WeaponSkin.Default;
        var gold = isUlt ? Theme.ColU32(info.AccentColor) : Theme.ColU32(Theme.YellowMain);
        var body = isUlt ? Theme.ColU32(info.MetalColor) : Theme.ColU32(Theme.PurpleMain);
        if (isUlt) DrawUltimateAura(dl, c, 0f, s * 0.7f, skin, true, t, -9f, 10f);

        dl.AddQuadFilled(
            c + new Vector2(-8f * s, -10f * s),
            c + new Vector2(8f * s, -10f * s),
            c + new Vector2(7f * s, 4f * s),
            c + new Vector2(-7f * s, 4f * s),
            body);
        dl.AddTriangleFilled(c + new Vector2(-7f * s, 4f * s), c + new Vector2(7f * s, 4f * s), c + new Vector2(0, 13f * s), body);
        dl.AddLine(c + new Vector2(0, -8f * s), c + new Vector2(0, 9f * s), gold, 2f * s);
        dl.AddLine(c + new Vector2(-6f * s, -2f * s), c + new Vector2(6f * s, -2f * s), gold, 2f * s);
    }

    private static void DrawFistKnuckle(ImDrawListPtr dl, Vector2 c, float s, WeaponSkin skin, float t)
    {
        var info = WeaponSkinCatalog.Get(skin);
        var isUlt = skin != WeaponSkin.Default;
        var glow = 0.5f + 0.5f * MathF.Sin(t * 10f);
        var auraCol = isUlt
            ? Theme.Rgba(info.GlowPrimary.X, info.GlowPrimary.Y, info.GlowPrimary.Z, 0.42f)
            : Theme.Rgba(1f, 0.85f, 0.1f, 0.35f);
        dl.AddCircleFilled(c, (9f + glow * 2.5f) * s, auraCol);
        dl.AddCircleFilled(c, 6.2f * s, isUlt ? Theme.ColU32(info.AccentColor) : Theme.ColU32(Theme.YellowMain));
        dl.AddCircle(c, 6.2f * s, Theme.Rgba(0.10f, 0.02f, 0.22f), 14, 1.8f * s);
    }

    private static void DrawChakram(ImDrawListPtr dl, Vector2 c, float s, WeaponSkin skin, float spin)
    {
        var info = WeaponSkinCatalog.Get(skin);
        var isUlt = skin != WeaponSkin.Default;
        var outer = isUlt ? Theme.ColU32(info.GlowPrimary) : Theme.ColU32(Theme.YellowMain);
        var inner = isUlt ? Theme.ColU32(info.GlowSecondary) : Theme.ColU32(Theme.PinkAccent);
        dl.AddCircle(c, 9.5f * s, outer, 18, 3.0f * s);
        dl.AddCircle(c, 5.5f * s, inner, 14, 1.8f * s);
        for (var i = 0; i < 4; i++)
        {
            var a = spin + i * (MathF.PI * 0.5f);
            var p = c + new Vector2(MathF.Cos(a) * 11.5f * s, MathF.Sin(a) * 11.5f * s);
            dl.AddCircleFilled(p, 2.3f * s, Theme.Rgba(1f, 1f, 1f));
        }
    }

    private static void DrawNouliths(ImDrawListPtr dl, Vector2 c, float s, WeaponSkin skin, bool drawn, float t)
    {
        var info = WeaponSkinCatalog.Get(skin);
        var isUlt = skin != WeaponSkin.Default;
        var cyan = isUlt ? Theme.ColU32(info.GlowPrimary) : Theme.ColU32(Theme.CyanAccent);
        var sec = isUlt ? Theme.ColU32(info.GlowSecondary) : cyan;
        var white = isUlt ? Theme.ColU32(info.MetalColor) : Theme.Rgba(0.94f, 0.96f, 1f);
        var spread = drawn ? 39f : 31f;
        var hover = MathF.Sin(t * 4.5f) * 3f * s;

        Vector2[] offsets =
        [
            new(-spread * s, -16f * s + hover),
            new(-spread * 0.9f * s, 2f * s - hover),
            new(spread * s, -16f * s - hover),
            new(spread * 0.9f * s, 2f * s + hover),
        ];

        for (var i = 0; i < offsets.Length; i++)
        {
            var p = c + offsets[i];
            var dir = i < 2 ? -1f : 1f;
            var glowCol = i % 2 == 0 ? cyan : sec;
            if (isUlt) dl.AddCircleFilled(p, 6.5f * s, Theme.Rgba(info.GlowPrimary.X, info.GlowPrimary.Y, info.GlowPrimary.Z, 0.25f));
            dl.AddLine(p + new Vector2(0, -6.5f * s), p + new Vector2(dir * 5.5f * s, 6.5f * s), white, 3.4f * s);
            dl.AddCircleFilled(p, 2.8f * s, glowCol);
            if (drawn && ((int)(t * 8f) + i) % 3 == 0)
            {
                dl.AddLine(p, p + new Vector2(dir * 18f * s, -4f * s), glowCol, 1.8f * s);
            }
        }
    }
}
