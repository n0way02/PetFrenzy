using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using PetFrenzy.Models;

namespace PetFrenzy.UI;

/// <summary>
/// Renders the animated Eorzea Tamagotchi (Egg + 4 species + accessories + mood particles)
/// directly with ImGui's DrawList at 60 FPS.
/// </summary>
public static class PetCanvasRenderer
{
    private static void AddEllipseFilled(this ImDrawListPtr dl, Vector2 center, Vector2 radius, uint col, int segments = 28)
    {
        dl.PathClear();
        for (var i = 0; i < segments; i++)
        {
            var a = (i / (float)segments) * MathF.PI * 2f;
            dl.PathLineTo(new Vector2(center.X + MathF.Cos(a) * radius.X, center.Y + MathF.Sin(a) * radius.Y));
        }
        dl.PathFillConvex(col);
    }

    private static void AddEllipse(this ImDrawListPtr dl, Vector2 center, Vector2 radius, uint col, float rot = 0f, int segments = 28, float thickness = 1.5f)
    {
        dl.PathClear();
        for (var i = 0; i < segments; i++)
        {
            var a = (i / (float)segments) * MathF.PI * 2f + rot;
            dl.PathLineTo(new Vector2(center.X + MathF.Cos(a) * radius.X, center.Y + MathF.Sin(a) * radius.Y));
        }
        dl.PathStroke(col, ImDrawFlags.Closed, thickness);
    }
    public static bool DrawPetCanvas(
        string id,
        PetStateData pet,
        PetMood mood,
        float timeSec,
        float wobblePulse,
        float sizePx,
        bool interactive = true,
        JobWeaponStyle weaponStyle = JobWeaponStyle.None,
        bool isWeaponDrawn = false)
    {
        ImGui.InvisibleButton(id, new Vector2(sizePx, sizePx));
        var clicked = interactive && ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var hovered = interactive && ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var min = ImGui.GetItemRectMin();
        DrawPetInDrawList(ImGui.GetWindowDrawList(), min, pet, mood, timeSec, wobblePulse, sizePx, weaponStyle, isWeaponDrawn, hovered);
        return clicked;
    }

    public static void DrawPetInDrawList(
        ImDrawListPtr dl,
        Vector2 min,
        PetStateData pet,
        PetMood mood,
        float timeSec,
        float wobblePulse,
        float sizePx,
        JobWeaponStyle weaponStyle = JobWeaponStyle.None,
        bool isWeaponDrawn = false,
        bool hovered = false)
    {
        var center = min + new Vector2(sizePx * 0.5f, sizePx * 0.56f);
        var s = sizePx / 120f;

        // Ground shadow
        var shadowW = 30f * s;
        var shadowH = 8f * s;
        var shadowPos = new Vector2(center.X, min.Y + sizePx * 0.87f);
        dl.AddEllipseFilled(shadowPos, new Vector2(shadowW, shadowH), Theme.Rgba(0.03f, 0.01f, 0.08f, 0.55f));

        // Legendary aura (Lv. 10+)
        if (pet.IsHatched && pet.Stage == PetStage.Legendary)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(timeSec * 3.5f);
            dl.AddCircleFilled(center, (44f + pulse * 4f) * s, Theme.Rgba(1.0f, 0.90f, 0.0f, 0.11f + pulse * 0.07f));
            dl.AddCircle(center, (42f + pulse * 3f) * s, Theme.Rgba(1.0f, 0.90f, 0.0f, 0.35f), 32, 1.5f * s);
        }
        else if (hovered)
        {
            dl.AddCircleFilled(center, 42f * s, Theme.Rgba(0.39f, 0.06f, 0.79f, 0.16f));
        }

        if (!pet.IsHatched || mood == PetMood.Hatching)
        {
            DrawEgg(dl, center, s, pet.Species, pet.EggProgress, mood == PetMood.Hatching, timeSec, wobblePulse);
            return;
        }

        // Compute motion offset from mood
        var bobY = 0f;
        var shakeX = 0f;
        var stageScale = pet.Stage switch
        {
            PetStage.Baby => 0.86f,
            PetStage.Young => 0.98f,
            _ => 1.06f,
        };
        var ps = s * stageScale;

        switch (mood)
        {
            case PetMood.Celebrating:
                bobY = -MathF.Abs(MathF.Sin(timeSec * 7.5f)) * 14f * s;
                break;
            case PetMood.Cheering:
                bobY = -MathF.Abs(MathF.Sin(timeSec * 6.5f)) * 7f * s;
                break;
            case PetMood.Working:
                bobY = -MathF.Abs(MathF.Sin(timeSec * 4.5f)) * 3.5f * s;
                break;
            case PetMood.Petted:
                bobY = -MathF.Sin(timeSec * 8.0f) * 4f * s;
                break;
            case PetMood.Panic:
                shakeX = MathF.Sin(timeSec * 42f) * 3.5f * s;
                bobY = -2f * s;
                break;
            case PetMood.Sleeping:
                bobY = 5f * s + MathF.Sin(timeSec * 1.8f) * 1.5f * s;
                break;
            case PetMood.Crying:
                bobY = 3f * s;
                shakeX = MathF.Sin(timeSec * 14f) * 1.5f * s;
                break;
            default:
                bobY = MathF.Sin(timeSec * 3.0f) * 3.0f * s;
                break;
        }

        var petCenter = center + new Vector2(shakeX, bobY);

        // Draw sheathed weapon on back (or floating Nouliths) BEHIND the pet body (unclipped so big weapons stand out!)
        if (weaponStyle != JobWeaponStyle.None)
        {
            dl.PushClipRectFullScreen();
            JobWeaponRenderer.DrawBack(dl, petCenter, ps, weaponStyle, pet.EquippedWeaponSkin, isWeaponDrawn, timeSec);
            dl.PopClipRect();
        }

        // Draw back-layer outfits (Capes, Robe Mantle, Seraph Wings, Astral Dragon Wings) BEHIND pet body!
        if (pet.EquippedOutfit is PetOutfit.HeroCape or PetOutfit.ArchmageRobe or PetOutfit.SeraphWings or PetOutfit.AstralWings)
        {
            dl.PushClipRectFullScreen();
            DrawOutfitBack(dl, petCenter, ps, pet.Species, pet.EquippedOutfit, timeSec);
            dl.PopClipRect();
        }

        var (defBody, defAccent, defDetail) = PetStateData.DefaultPaletteFor(pet.Species);
        var cBody = Theme.ColU32(pet.UseCustomColors ? pet.CustomBodyColor : defBody);
        var cAccent = Theme.ColU32(pet.UseCustomColors ? pet.CustomAccentColor : defAccent);
        var cDetail = Theme.ColU32(pet.UseCustomColors ? pet.CustomDetailColor : defDetail);

        switch (pet.Species)
        {
            case PetSpecies.Carbuncle:
                DrawCarbuncle(dl, petCenter, ps, mood, timeSec, cBody, cAccent, cDetail);
                break;
            case PetSpecies.FatCat:
                DrawFatCat(dl, petCenter, ps, mood, timeSec, cBody, cAccent, cDetail);
                break;
            case PetSpecies.Moogle:
                DrawMoogle(dl, petCenter, ps, mood, timeSec, cBody, cAccent, cDetail);
                break;
            case PetSpecies.Namazu:
                DrawNamazu(dl, petCenter, ps, mood, timeSec, cBody, cAccent, cDetail);
                break;
            case PetSpecies.FluffyKitty:
                dl.PushClipRectFullScreen();
                DrawFluffyKitty(dl, petCenter, ps, mood, timeSec, cBody, cAccent, cDetail);
                dl.PopClipRect();
                break;
            case PetSpecies.ShepherdDog:
                dl.PushClipRectFullScreen();
                DrawShepherdDog(dl, petCenter, ps, mood, timeSec, cBody, cAccent, cDetail);
                dl.PopClipRect();
                break;
            case PetSpecies.Deviruchi:
                dl.PushClipRectFullScreen();
                DrawDeviruchi(dl, petCenter, ps, mood, timeSec, cBody, cAccent, cDetail, showTrident: weaponStyle == JobWeaponStyle.None || !isWeaponDrawn);
                dl.PopClipRect();
                break;
        }

        // Draw front-layer outfits (Scarf, Red Necktie, Tuxedo, Hero Clasp, Knight Armor, Archmage Robe) ON pet body!
        if (pet.EquippedOutfit is PetOutfit.CozyScarf or PetOutfit.RedNecktie or PetOutfit.DapperTux or PetOutfit.HeroCape or PetOutfit.KnightArmor or PetOutfit.ArchmageRobe)
        {
            dl.PushClipRectFullScreen();
            DrawOutfitFront(dl, petCenter, ps, pet.Species, pet.EquippedOutfit, timeSec);
            dl.PopClipRect();
        }

        // Draw equipped hat / accessory on top of head
        if (pet.EquippedAccessory != PetAccessory.None)
        {
            dl.PushClipRectFullScreen();
            DrawAccessory(dl, petCenter, ps, pet.Species, pet.EquippedAccessory, timeSec);
            dl.PopClipRect();
        }

        // Draw unsheathed combat weapon or active craft/gather tool IN FRONT of the pet!
        if (weaponStyle != JobWeaponStyle.None)
        {
            dl.PushClipRectFullScreen();
            JobWeaponRenderer.DrawFront(dl, petCenter, ps, weaponStyle, pet.EquippedWeaponSkin, isWeaponDrawn, timeSec);
            dl.PopClipRect();
        }

        // Draw mood particles (hearts, Zzz, confetti, tears, sweat)
        DrawMoodParticles(dl, petCenter, s, mood, timeSec);
    }

    private static void DrawEgg(ImDrawListPtr dl, Vector2 center, float s, PetSpecies sp, float progress, bool hatching, float t, float wobblePulse)
    {
        var wobbleAmp = hatching ? 7f : (wobblePulse > 0f ? 5f : (progress > 65f ? 2.5f : 1.0f));
        var wobbleFreq = hatching ? 32f : (wobblePulse > 0f ? 22f : 4.5f);
        var dx = MathF.Sin(t * wobbleFreq) * wobbleAmp * s;
        var dy = hatching ? -MathF.Abs(MathF.Sin(t * 12f)) * 8f * s : MathF.Sin(t * 2.5f) * 1.5f * s;
        var pos = center + new Vector2(dx, dy);

        var (shellCol, spotCol) = sp switch
        {
            PetSpecies.Carbuncle => (Theme.Rgba(0.75f, 0.96f, 0.92f), Theme.Rgba(0.18f, 0.80f, 0.72f)),
            PetSpecies.ShepherdDog => (Theme.Rgba(0.84f, 0.66f, 0.42f), Theme.Rgba(0.18f, 0.14f, 0.14f)),
            PetSpecies.FatCat => (Theme.Rgba(0.95f, 0.92f, 0.86f), Theme.Rgba(0.55f, 0.52f, 0.60f)),
            PetSpecies.Moogle => (Theme.Rgba(0.98f, 0.96f, 1.00f), Theme.Rgba(0.95f, 0.45f, 0.65f)),
            PetSpecies.FluffyKitty => (Theme.Rgba(0.88f, 0.81f, 0.72f), Theme.Rgba(0.16f, 0.13f, 0.15f)),
            PetSpecies.Deviruchi   => (Theme.Rgba(0.46f, 0.32f, 0.52f), Theme.Rgba(0.18f, 0.09f, 0.24f)),
            _ => (Theme.Rgba(1.00f, 0.94f, 0.74f), Theme.Rgba(0.95f, 0.35f, 0.25f)),
        };

        var outline = Theme.Rgba(0.11f, 0.02f, 0.22f);

        // Glow behind egg when hatching
        if (hatching)
        {
            dl.AddCircleFilled(pos, 46f * s, Theme.Rgba(1.0f, 0.90f, 0.0f, 0.32f));
        }

        // Egg oval
        dl.AddEllipseFilled(pos, new Vector2(26f * s, 33f * s), shellCol);
        dl.AddCircleFilled(pos + new Vector2(-10f * s, -6f * s), 6f * s, spotCol);
        dl.AddCircleFilled(pos + new Vector2(11f * s, 5f * s), 7f * s, spotCol);
        dl.AddCircleFilled(pos + new Vector2(-4f * s, 14f * s), 4.5f * s, spotCol);
        // Gloss highlight
        dl.AddEllipseFilled(pos + new Vector2(-9f * s, -15f * s), new Vector2(4.5f * s, 8f * s), Theme.Rgba(1f, 1f, 1f, 0.55f));
        dl.AddEllipse(pos, new Vector2(26f * s, 33f * s), outline, 0f, 32, 2.5f * s);

        // Cracks at 33%, 66%, and hatching!
        if (progress >= 33f || hatching)
        {
            dl.AddLine(pos + new Vector2(-12f * s, -2f * s), pos + new Vector2(-2f * s, 4f * s), outline, 2.2f * s);
            dl.AddLine(pos + new Vector2(-2f * s, 4f * s), pos + new Vector2(5f * s, -3f * s), outline, 2.2f * s);
        }
        if (progress >= 66f || hatching)
        {
            dl.AddLine(pos + new Vector2(5f * s, -3f * s), pos + new Vector2(14f * s, 3f * s), outline, 2.2f * s);
            dl.AddLine(pos + new Vector2(-2f * s, 4f * s), pos + new Vector2(-6f * s, 14f * s), outline, 2.0f * s);
        }

        if (hatching)
        {
            DrawMoodParticles(dl, pos, s, PetMood.Celebrating, t * 1.4f);
        }
    }

    private static void DrawCarbuncle(ImDrawListPtr dl, Vector2 c, float s, PetMood mood, float t, uint bodyCol, uint earInner, uint detailCol)
    {
        var outline = Theme.Rgba(0.10f, 0.02f, 0.22f);
        var tailWag = MathF.Sin(t * 5.5f) * 6f * s;

        // Fluffy tail
        dl.AddEllipseFilled(c + new Vector2(28f * s, 8f * s + tailWag * 0.4f), new Vector2(14f * s, 9f * s), bodyCol);
        dl.AddEllipse(c + new Vector2(28f * s, 8f * s + tailWag * 0.4f), new Vector2(14f * s, 9f * s), outline, 0f, 20, 2f * s);

        // Long fox-like ears
        var earBounce = MathF.Sin(t * 4f) * 2f * s;
        dl.AddTriangleFilled(c + new Vector2(-22f * s, -12f * s), c + new Vector2(-34f * s, -44f * s + earBounce), c + new Vector2(-6f * s, -22f * s), bodyCol);
        dl.AddTriangleFilled(c + new Vector2(-20f * s, -16f * s), c + new Vector2(-29f * s, -37f * s + earBounce), c + new Vector2(-10f * s, -22f * s), earInner);
        dl.AddTriangle(c + new Vector2(-22f * s, -12f * s), c + new Vector2(-34f * s, -44f * s + earBounce), c + new Vector2(-6f * s, -22f * s), outline, 2f * s);

        dl.AddTriangleFilled(c + new Vector2(22f * s, -12f * s), c + new Vector2(34f * s, -44f * s - earBounce), c + new Vector2(6f * s, -22f * s), bodyCol);
        dl.AddTriangleFilled(c + new Vector2(20f * s, -16f * s), c + new Vector2(29f * s, -37f * s - earBounce), c + new Vector2(10f * s, -22f * s), earInner);
        dl.AddTriangle(c + new Vector2(22f * s, -12f * s), c + new Vector2(34f * s, -44f * s - earBounce), c + new Vector2(6f * s, -22f * s), outline, 2f * s);

        // Round body/head
        dl.AddEllipseFilled(c + new Vector2(0, 4f * s), new Vector2(27f * s, 24f * s), bodyCol);
        dl.AddEllipse(c + new Vector2(0, 4f * s), new Vector2(27f * s, 24f * s), outline, 0f, 32, 2.2f * s);

        // Forehead ruby/custom gem
        dl.AddQuadFilled(
            c + new Vector2(0, -17f * s),
            c + new Vector2(5f * s, -11f * s),
            c + new Vector2(0, -5f * s),
            c + new Vector2(-5f * s, -11f * s),
            detailCol);
        dl.AddCircleFilled(c + new Vector2(-1f * s, -12f * s), 1.5f * s, Theme.Rgba(1f, 0.92f, 0.4f));

        DrawCuteFace(dl, c + new Vector2(0, 5f * s), s, mood, t, detailCol);
    }

    private static void DrawFatCat(ImDrawListPtr dl, Vector2 c, float s, PetMood mood, float t, uint bodyCol, uint stripeCol, uint detailCol)
    {
        var outline = Theme.Rgba(0.12f, 0.04f, 0.20f);
        var tailSway = MathF.Sin(t * 3.2f) * 5f * s;

        // Tail
        dl.AddEllipseFilled(c + new Vector2(31f * s, 12f * s + tailSway * 0.4f), new Vector2(11f * s, 6f * s), stripeCol);
        dl.AddEllipse(c + new Vector2(31f * s, 12f * s + tailSway * 0.4f), new Vector2(11f * s, 6f * s), outline, 0f, 20, 2f * s);

        // Small cat ears
        dl.AddTriangleFilled(c + new Vector2(-24f * s, -10f * s), c + new Vector2(-20f * s, -30f * s), c + new Vector2(-6f * s, -18f * s), stripeCol);
        dl.AddTriangle(c + new Vector2(-24f * s, -10f * s), c + new Vector2(-20f * s, -30f * s), c + new Vector2(-6f * s, -18f * s), outline, 2f * s);
        dl.AddTriangleFilled(c + new Vector2(24f * s, -10f * s), c + new Vector2(20f * s, -30f * s), c + new Vector2(6f * s, -18f * s), stripeCol);
        dl.AddTriangle(c + new Vector2(24f * s, -10f * s), c + new Vector2(20f * s, -30f * s), c + new Vector2(6f * s, -18f * s), outline, 2f * s);

        // Extra chubby round body
        dl.AddEllipseFilled(c + new Vector2(0, 6f * s), new Vector2(33f * s, 26f * s), bodyCol);
        // Forehead stripes
        dl.AddLine(c + new Vector2(-4f * s, -17f * s), c + new Vector2(-4f * s, -10f * s), stripeCol, 2.5f * s);
        dl.AddLine(c + new Vector2(0, -18f * s), c + new Vector2(0, -9f * s), stripeCol, 2.5f * s);
        dl.AddLine(c + new Vector2(4f * s, -17f * s), c + new Vector2(4f * s, -10f * s), stripeCol, 2.5f * s);
        dl.AddEllipse(c + new Vector2(0, 6f * s), new Vector2(33f * s, 26f * s), outline, 0f, 32, 2.3f * s);

        // Whiskers
        dl.AddLine(c + new Vector2(-22f * s, 4f * s), c + new Vector2(-34f * s, 2f * s), outline, 1.5f * s);
        dl.AddLine(c + new Vector2(-22f * s, 9f * s), c + new Vector2(-34f * s, 11f * s), outline, 1.5f * s);
        dl.AddLine(c + new Vector2(22f * s, 4f * s), c + new Vector2(34f * s, 2f * s), outline, 1.5f * s);
        dl.AddLine(c + new Vector2(22f * s, 9f * s), c + new Vector2(34f * s, 11f * s), outline, 1.5f * s);

        DrawCuteFace(dl, c + new Vector2(0, 4f * s), s, mood, t, detailCol);
    }

    private static void DrawMoogle(ImDrawListPtr dl, Vector2 c, float s, PetMood mood, float t, uint bodyCol, uint wingCol, uint pomCol)
    {
        var outline = Theme.Rgba(0.11f, 0.02f, 0.22f);
        var flap = MathF.Sin(t * 9f) * 6f * s;

        // Little bat wings
        dl.AddTriangleFilled(c + new Vector2(-24f * s, 4f * s), c + new Vector2(-42f * s, -10f * s + flap), c + new Vector2(-34f * s, 14f * s + flap * 0.5f), wingCol);
        dl.AddTriangleFilled(c + new Vector2(24f * s, 4f * s), c + new Vector2(42f * s, -10f * s + flap), c + new Vector2(34f * s, 14f * s + flap * 0.5f), wingCol);

        // Bouncy pom-pom antenna
        var pomOff = new Vector2(MathF.Sin(t * 4.5f) * 4f * s, -40f * s + MathF.Cos(t * 4.5f) * 2f * s);
        dl.AddLine(c + new Vector2(0, -20f * s), c + pomOff, outline, 2.2f * s);
        dl.AddCircleFilled(c + pomOff, 8f * s, Theme.Rgba(1f, 0.9f, 0f, 0.26f));
        dl.AddCircleFilled(c + pomOff, 5.5f * s, pomCol);
        dl.AddCircle(c + pomOff, 5.5f * s, outline, 16, 1.6f * s);

        // Fluffy body
        dl.AddEllipseFilled(c + new Vector2(0, 4f * s), new Vector2(27f * s, 25f * s), bodyCol);
        dl.AddEllipse(c + new Vector2(0, 4f * s), new Vector2(27f * s, 25f * s), outline, 0f, 32, 2.2f * s);

        // Cute button nose
        dl.AddCircleFilled(c + new Vector2(0, 7f * s), 4.2f * s, Theme.Rgba(0.95f, 0.28f, 0.38f));

        DrawCuteFace(dl, c + new Vector2(0, 1f * s), s, mood, t, pomCol, skipMouth: false);
    }

    private static void DrawNamazu(ImDrawListPtr dl, Vector2 c, float s, PetMood mood, float t, uint bodyCol, uint scarfCol, uint detailCol)
    {
        var bellyCol = Theme.Rgba(1.00f, 0.96f, 0.84f, 0.55f);
        var outline = Theme.Rgba(0.14f, 0.06f, 0.16f);
        var wiggle = MathF.Sin(t * 6f) * 4f * s;

        // Round catfish body
        dl.AddEllipseFilled(c + new Vector2(0, 4f * s), new Vector2(30f * s, 25f * s), bodyCol);
        dl.AddEllipseFilled(c + new Vector2(0, 11f * s), new Vector2(21f * s, 14f * s), bellyCol);
        dl.AddEllipse(c + new Vector2(0, 4f * s), new Vector2(30f * s, 25f * s), outline, 0f, 32, 2.3f * s);

        // Festive scarf
        dl.AddRectFilled(c + new Vector2(-20f * s, 20f * s), c + new Vector2(20f * s, 27f * s), scarfCol, 4f * s);

        // Long wiggling catfish whiskers
        dl.AddLine(c + new Vector2(-24f * s, 5f * s), c + new Vector2(-42f * s, 1f * s + wiggle), outline, 2.4f * s);
        dl.AddLine(c + new Vector2(-23f * s, 11f * s), c + new Vector2(-39f * s, 14f * s - wiggle), outline, 2.0f * s);
        dl.AddLine(c + new Vector2(24f * s, 5f * s), c + new Vector2(42f * s, 1f * s - wiggle), outline, 2.4f * s);
        dl.AddLine(c + new Vector2(23f * s, 11f * s), c + new Vector2(39f * s, 14f * s + wiggle), outline, 2.0f * s);

        DrawCuteFace(dl, c + new Vector2(0, 2f * s), s, mood, t, detailCol);
    }

    private static void DrawFluffyKitty(ImDrawListPtr dl, Vector2 c, float s, PetMood mood, float t, uint maneCol, uint darkFurCol, uint eyeBlueCol)
    {
        var outline = Theme.Rgba(0.06f, 0.04f, 0.08f);
        var earInner = Theme.Rgba(0.28f, 0.24f, 0.27f);
        var softCream = Theme.Rgba(0.94f, 0.89f, 0.81f);
        var tailSway = MathF.Sin(t * 3.0f) * 5.5f * s;

        // Big, super-fluffy black tail curling gracefully up to the right
        var t1 = c + new Vector2(26f * s, 14f * s + tailSway * 0.25f);
        var t2 = c + new Vector2(36f * s, 7f * s + tailSway * 0.50f);
        var t3 = c + new Vector2(43f * s, -2f * s + tailSway * 0.80f);
        var t4 = c + new Vector2(41f * s, -12f * s + tailSway);
        dl.AddCircleFilled(t1, 9.5f * s, outline);
        dl.AddCircleFilled(t2, 10.5f * s, outline);
        dl.AddCircleFilled(t3, 10.0f * s, outline);
        dl.AddCircleFilled(t4, 8.5f * s, outline);
        dl.AddCircleFilled(t1, 7.8f * s, darkFurCol);
        dl.AddCircleFilled(t2, 8.8f * s, darkFurCol);
        dl.AddCircleFilled(t3, 8.4f * s, darkFurCol);
        dl.AddCircleFilled(t4, 7.0f * s, darkFurCol);

        // Pointed dark cat ears with inner fluff
        var earTwitch = MathF.Sin(t * 2.8f) * 1.2f * s;
        dl.AddTriangleFilled(c + new Vector2(-24f * s, -9f * s), c + new Vector2(-21f * s, -32f * s + earTwitch), c + new Vector2(-6f * s, -18f * s), darkFurCol);
        dl.AddTriangleFilled(c + new Vector2(-20f * s, -13f * s), c + new Vector2(-19f * s, -26f * s + earTwitch), c + new Vector2(-10f * s, -17f * s), earInner);
        dl.AddTriangle(c + new Vector2(-24f * s, -9f * s), c + new Vector2(-21f * s, -32f * s + earTwitch), c + new Vector2(-6f * s, -18f * s), outline, 2.0f * s);

        dl.AddTriangleFilled(c + new Vector2(24f * s, -9f * s), c + new Vector2(21f * s, -32f * s - earTwitch), c + new Vector2(6f * s, -18f * s), darkFurCol);
        dl.AddTriangleFilled(c + new Vector2(20f * s, -13f * s), c + new Vector2(19f * s, -26f * s - earTwitch), c + new Vector2(10f * s, -17f * s), earInner);
        dl.AddTriangle(c + new Vector2(24f * s, -9f * s), c + new Vector2(21f * s, -32f * s - earTwitch), c + new Vector2(6f * s, -18f * s), outline, 2.0f * s);

        // Fluffy cream mane / body
        dl.AddEllipseFilled(c + new Vector2(0, 5f * s), new Vector2(30f * s, 25f * s), maneCol);
        // Fluffy cream chest ruff
        dl.AddEllipseFilled(c + new Vector2(0, 16f * s), new Vector2(20f * s, 11f * s), softCream);
        dl.AddEllipse(c + new Vector2(0, 5f * s), new Vector2(30f * s, 25f * s), outline, 0f, 32, 2.2f * s);

        // Little dark seal-point paws peeking at bottom
        dl.AddEllipseFilled(c + new Vector2(-13f * s, 27f * s), new Vector2(6.5f * s, 4.2f * s), darkFurCol);
        dl.AddEllipse(c + new Vector2(-13f * s, 27f * s), new Vector2(6.5f * s, 4.2f * s), outline, 0f, 14, 1.5f * s);
        dl.AddEllipseFilled(c + new Vector2(13f * s, 27f * s), new Vector2(6.5f * s, 4.2f * s), darkFurCol);
        dl.AddEllipse(c + new Vector2(13f * s, 27f * s), new Vector2(6.5f * s, 4.2f * s), outline, 0f, 14, 1.5f * s);

        // Signature dark chocolate-black Siamese face mask
        dl.AddEllipseFilled(c + new Vector2(0, -1f * s), new Vector2(21.5f * s, 16.5f * s), darkFurCol);
        dl.AddEllipseFilled(c + new Vector2(0, -9f * s), new Vector2(15f * s, 9f * s), darkFurCol);

        // Soft cream whiskers
        dl.AddLine(c + new Vector2(-18f * s, 2f * s), c + new Vector2(-33f * s, 0f * s), softCream, 1.4f * s);
        dl.AddLine(c + new Vector2(-18f * s, 6f * s), c + new Vector2(-32f * s, 8f * s), softCream, 1.4f * s);
        dl.AddLine(c + new Vector2(18f * s, 2f * s), c + new Vector2(33f * s, 0f * s), softCream, 1.4f * s);
        dl.AddLine(c + new Vector2(18f * s, 6f * s), c + new Vector2(32f * s, 8f * s), softCream, 1.4f * s);

        // Striking icy-blue eyes & cute face adapted for dark mask
        var fc = c + new Vector2(0, 0f * s);
        var isBlinking = (t % 4.2f) > 4.02f;
        var leftEye = fc + new Vector2(-8.5f * s, -2.5f * s);
        var rightEye = fc + new Vector2(8.5f * s, -2.5f * s);
        var pupil = Theme.Rgba(0.06f, 0.04f, 0.10f);
        var white = Theme.Rgba(1f, 1f, 1f);

        if (mood == PetMood.Sleeping || isBlinking)
        {
            dl.AddLine(leftEye + new Vector2(-4f * s, 0), leftEye + new Vector2(4f * s, 0), softCream, 2.0f * s);
            dl.AddLine(rightEye + new Vector2(-4f * s, 0), rightEye + new Vector2(4f * s, 0), softCream, 2.0f * s);
        }
        else if (mood is PetMood.Happy or PetMood.Petted or PetMood.Celebrating)
        {
            dl.AddLine(leftEye + new Vector2(-4f * s, 2f * s), leftEye + new Vector2(0, -3f * s), eyeBlueCol, 2.3f * s);
            dl.AddLine(leftEye + new Vector2(0, -3f * s), leftEye + new Vector2(4f * s, 2f * s), eyeBlueCol, 2.3f * s);
            dl.AddLine(rightEye + new Vector2(-4f * s, 2f * s), rightEye + new Vector2(0, -3f * s), eyeBlueCol, 2.3f * s);
            dl.AddLine(rightEye + new Vector2(0, -3f * s), rightEye + new Vector2(4f * s, 2f * s), eyeBlueCol, 2.3f * s);
        }
        else if (mood == PetMood.Crying)
        {
            dl.AddLine(leftEye + new Vector2(-3.5f * s, -3f * s), leftEye + new Vector2(3.5f * s, 3f * s), eyeBlueCol, 2.2f * s);
            dl.AddLine(leftEye + new Vector2(-3.5f * s, 3f * s), leftEye + new Vector2(3.5f * s, -3f * s), eyeBlueCol, 2.2f * s);
            dl.AddLine(rightEye + new Vector2(-3.5f * s, -3f * s), rightEye + new Vector2(3.5f * s, 3f * s), eyeBlueCol, 2.2f * s);
            dl.AddLine(rightEye + new Vector2(-3.5f * s, 3f * s), rightEye + new Vector2(3.5f * s, -3f * s), eyeBlueCol, 2.2f * s);
        }
        else
        {
            // Big expressive icy-blue Siamese eyes with dark pupil & white shine
            var eyeR = mood == PetMood.Panic ? 5.2f * s : 4.5f * s;
            dl.AddCircleFilled(leftEye, eyeR, eyeBlueCol);
            dl.AddCircleFilled(leftEye, eyeR * 0.48f, pupil);
            dl.AddCircleFilled(leftEye + new Vector2(-1.4f * s, -1.4f * s), 1.4f * s, white);

            dl.AddCircleFilled(rightEye, eyeR, eyeBlueCol);
            dl.AddCircleFilled(rightEye, eyeR * 0.48f, pupil);
            dl.AddCircleFilled(rightEye + new Vector2(-1.4f * s, -1.4f * s), 1.4f * s, white);

            if (mood == PetMood.Cheering)
            {
                dl.AddLine(leftEye + new Vector2(-5.5f * s, -7.2f * s), leftEye + new Vector2(4.5f * s, -3.5f * s), softCream, 2.3f * s);
                dl.AddLine(rightEye + new Vector2(5.5f * s, -7.2f * s), rightEye + new Vector2(-4.5f * s, -3.5f * s), softCream, 2.3f * s);
            }
        }

        // Cute little nose & ":3" smile
        dl.AddTriangleFilled(fc + new Vector2(-2.2f * s, 3.2f * s), fc + new Vector2(2.2f * s, 3.2f * s), fc + new Vector2(0, 5.4f * s), Theme.Rgba(0.45f, 0.34f, 0.38f));
        dl.AddLine(fc + new Vector2(-3.5f * s, 6.2f * s), fc + new Vector2(0, 8.4f * s), softCream, 1.5f * s);
        dl.AddLine(fc + new Vector2(0, 8.4f * s), fc + new Vector2(3.5f * s, 6.2f * s), softCream, 1.5f * s);
    }

    private static void DrawShepherdDog(ImDrawListPtr dl, Vector2 c, float s, PetMood mood, float t, uint tanFur, uint blackMantle, uint amberEye)
    {
        var outline = Theme.Rgba(0.10f, 0.04f, 0.08f);
        var tanV = ImGui.ColorConvertU32ToFloat4(tanFur);
        var creamChest = Theme.Rgba(MathF.Min(1f, tanV.X + 0.16f), MathF.Min(1f, tanV.Y + 0.18f), MathF.Min(1f, tanV.Z + 0.20f));
        var earInner = Theme.Rgba(MathF.Min(1f, tanV.X + 0.10f), MathF.Min(1f, tanV.Y + 0.10f), MathF.Min(1f, tanV.Z + 0.10f));
        var warmShadow = Theme.Rgba(tanV.X * 0.78f, tanV.Y * 0.76f, tanV.Z * 0.74f);
        var tonguePink = Theme.Rgba(0.97f, 0.50f, 0.62f);

        // Bushy shepherd tail wagging joyfully behind the body!
        var wagSpeed = mood is PetMood.Happy or PetMood.Petted or PetMood.Celebrating or PetMood.Cheering ? 9.0f : 3.6f;
        var wag = MathF.Sin(t * wagSpeed) * 7f * s;
        var tailBase = c + new Vector2(24f * s, 12f * s);
        var tailMid = c + new Vector2(36f * s + wag * 0.5f, 2f * s);
        var tailTip = c + new Vector2(40f * s + wag, -11f * s);
        dl.AddCircleFilled(tailBase + new Vector2(5f * s, -2f * s), 7.5f * s, tanFur);
        dl.AddCircleFilled(tailMid, 8.2f * s, tanFur);
        dl.AddCircleFilled(tailTip, 6.8f * s, blackMantle);
        dl.AddCircle(tailMid, 8.2f * s, outline, 14, 1.4f * s);
        dl.AddCircle(tailTip, 6.8f * s, outline, 14, 1.4f * s);

        // Tall, pointy fluffy German Shepherd ears (black outside, warm tan fluff inside)
        var earTwitch = MathF.Sin(t * 2.6f) * 1.4f * s;
        var lEar1 = c + new Vector2(-23f * s, -11f * s);
        var lEar2 = c + new Vector2(-20f * s - earTwitch, -39f * s);
        var lEar3 = c + new Vector2(-6f * s, -18f * s);
        dl.AddTriangleFilled(lEar1, lEar2, lEar3, blackMantle);
        dl.AddTriangleFilled(c + new Vector2(-19f * s, -14f * s), c + new Vector2(-18f * s - earTwitch * 0.7f, -32f * s), c + new Vector2(-10f * s, -17f * s), earInner);
        dl.AddTriangle(lEar1, lEar2, lEar3, outline, 2.0f * s);

        var rEar1 = c + new Vector2(23f * s, -11f * s);
        var rEar2 = c + new Vector2(20f * s + earTwitch, -39f * s);
        var rEar3 = c + new Vector2(6f * s, -18f * s);
        dl.AddTriangleFilled(rEar1, rEar2, rEar3, blackMantle);
        dl.AddTriangleFilled(c + new Vector2(19f * s, -14f * s), c + new Vector2(18f * s + earTwitch * 0.7f, -32f * s), c + new Vector2(10f * s, -17f * s), earInner);
        dl.AddTriangle(rEar1, rEar2, rEar3, outline, 2.0f * s);

        // Main fluffy body (rich golden-tan fur + black saddle/mantle on upper back/shoulders)
        dl.AddEllipseFilled(c + new Vector2(0, 5f * s), new Vector2(31f * s, 25.5f * s), tanFur);
        // Dark saddle / shoulder ruff peeking along top & sides
        dl.AddEllipseFilled(c + new Vector2(0, -5f * s), new Vector2(27f * s, 14f * s), blackMantle);
        // Fluffy golden-tan face & cheek mane
        dl.AddEllipseFilled(c + new Vector2(0, 1f * s), new Vector2(24f * s, 19.5f * s), tanFur);
        dl.AddCircleFilled(c + new Vector2(-22f * s, 7f * s), 9.5f * s, tanFur);
        dl.AddCircleFilled(c + new Vector2(22f * s, 7f * s), 9.5f * s, tanFur);
        // Fluffy lighter mane ruff on the chest (matches the photo's soft cream neck fur!)
        dl.AddEllipseFilled(c + new Vector2(0, 18f * s), new Vector2(17f * s, 10.5f * s), creamChest);
        dl.AddCircleFilled(c + new Vector2(-9f * s, 17f * s), 6.5f * s, creamChest);
        dl.AddCircleFilled(c + new Vector2(9f * s, 17f * s), 6.5f * s, creamChest);
        dl.AddEllipse(c + new Vector2(0, 5f * s), new Vector2(31f * s, 25.5f * s), outline, 0f, 32, 2.1f * s);

        // Fluffy tan front paws resting neatly
        dl.AddEllipseFilled(c + new Vector2(-13f * s, 26.5f * s), new Vector2(7.5f * s, 4.5f * s), warmShadow);
        dl.AddEllipse(c + new Vector2(-13f * s, 26.5f * s), new Vector2(7.5f * s, 4.5f * s), outline, 0f, 14, 1.5f * s);
        dl.AddEllipseFilled(c + new Vector2(13f * s, 26.5f * s), new Vector2(7.5f * s, 4.5f * s), warmShadow);
        dl.AddEllipse(c + new Vector2(13f * s, 26.5f * s), new Vector2(7.5f * s, 4.5f * s), outline, 0f, 14, 1.5f * s);

        // Characteristic dark shepherd muzzle mask
        var fc = c + new Vector2(0, 1f * s);
        dl.AddEllipseFilled(fc + new Vector2(0, 6.2f * s), new Vector2(11.5f * s, 7.8f * s), blackMantle);
        // Little tan eyebrows above eyes
        dl.AddEllipseFilled(fc + new Vector2(-9.5f * s, -8.5f * s), new Vector2(3.6f * s, 2.0f * s), creamChest);
        dl.AddEllipseFilled(fc + new Vector2(9.5f * s, -8.5f * s), new Vector2(3.6f * s, 2.0f * s), creamChest);

        // Expressive amber / honey eyes
        var isBlinking = (t % 4.4f) > 4.22f;
        var leftEye = fc + new Vector2(-9.5f * s, -2.5f * s);
        var rightEye = fc + new Vector2(9.5f * s, -2.5f * s);

        if (mood == PetMood.Sleeping || isBlinking)
        {
            dl.AddLine(leftEye + new Vector2(-4f * s, 0), leftEye + new Vector2(4f * s, 0), outline, 2.2f * s);
            dl.AddLine(rightEye + new Vector2(-4f * s, 0), rightEye + new Vector2(4f * s, 0), outline, 2.2f * s);
        }
        else if (mood is PetMood.Happy or PetMood.Petted or PetMood.Celebrating)
        {
            dl.AddLine(leftEye + new Vector2(-4f * s, 2f * s), leftEye + new Vector2(0, -3f * s), outline, 2.3f * s);
            dl.AddLine(leftEye + new Vector2(0, -3f * s), leftEye + new Vector2(4f * s, 2f * s), outline, 2.3f * s);
            dl.AddLine(rightEye + new Vector2(-4f * s, 2f * s), rightEye + new Vector2(0, -3f * s), outline, 2.3f * s);
            dl.AddLine(rightEye + new Vector2(0, -3f * s), rightEye + new Vector2(4f * s, 2f * s), outline, 2.3f * s);
        }
        else if (mood == PetMood.Crying)
        {
            dl.AddLine(leftEye + new Vector2(-3.5f * s, -3f * s), leftEye + new Vector2(3.5f * s, 3f * s), outline, 2.2f * s);
            dl.AddLine(leftEye + new Vector2(-3.5f * s, 3f * s), leftEye + new Vector2(3.5f * s, -3f * s), outline, 2.2f * s);
            dl.AddLine(rightEye + new Vector2(-3.5f * s, -3f * s), rightEye + new Vector2(3.5f * s, 3f * s), outline, 2.2f * s);
            dl.AddLine(rightEye + new Vector2(-3.5f * s, 3f * s), rightEye + new Vector2(3.5f * s, -3f * s), outline, 2.2f * s);
        }
        else if (mood == PetMood.Panic)
        {
            dl.AddCircleFilled(leftEye, 5.0f * s, Theme.Rgba(1f, 1f, 1f));
            dl.AddCircle(leftEye, 5.0f * s, outline, 16, 1.7f * s);
            dl.AddCircleFilled(leftEye, 2.0f * s, outline);
            dl.AddCircleFilled(rightEye, 5.0f * s, Theme.Rgba(1f, 1f, 1f));
            dl.AddCircle(rightEye, 5.0f * s, outline, 16, 1.7f * s);
            dl.AddCircleFilled(rightEye, 2.0f * s, outline);
        }
        else
        {
            // Warm amber iris + dark pupil + sparkle highlight
            dl.AddCircleFilled(leftEye, 4.3f * s, amberEye);
            dl.AddCircleFilled(leftEye, 2.3f * s, outline);
            dl.AddCircleFilled(leftEye + new Vector2(-1.2f * s, -1.3f * s), 1.2f * s, Theme.Rgba(1f, 1f, 1f));
            dl.AddCircle(leftEye, 4.3f * s, outline, 14, 1.2f * s);

            dl.AddCircleFilled(rightEye, 4.3f * s, amberEye);
            dl.AddCircleFilled(rightEye, 2.3f * s, outline);
            dl.AddCircleFilled(rightEye + new Vector2(-1.2f * s, -1.3f * s), 1.2f * s, Theme.Rgba(1f, 1f, 1f));
            dl.AddCircle(rightEye, 4.3f * s, outline, 14, 1.2f * s);

            if (mood == PetMood.Cheering)
            {
                // Fierce guard-dog eyebrows in combat!
                dl.AddLine(leftEye + new Vector2(-5.5f * s, -7.2f * s), leftEye + new Vector2(4.5f * s, -3.2f * s), outline, 2.4f * s);
                dl.AddLine(rightEye + new Vector2(5.5f * s, -7.2f * s), rightEye + new Vector2(-4.5f * s, -3.2f * s), outline, 2.4f * s);
            }
        }

        // Shiny black dog nose
        dl.AddEllipseFilled(fc + new Vector2(0, 3.4f * s), new Vector2(4.2f * s, 2.8f * s), Theme.Rgba(0.05f, 0.04f, 0.06f));
        dl.AddCircleFilled(fc + new Vector2(-1.2f * s, 2.5f * s), 1.0f * s, Theme.Rgba(0.85f, 0.85f, 0.90f, 0.75f));

        // Happy pink doggy tongue sticking out (just like the photo!) when cheerful/idle/excited
        if (mood is PetMood.Idle or PetMood.Happy or PetMood.Petted or PetMood.Celebrating or PetMood.Cheering)
        {
            var pant = MathF.Sin(t * 7.5f) * 0.9f * s;
            dl.AddEllipseFilled(fc + new Vector2(0, 10.6f * s + pant), new Vector2(3.4f * s, 4.4f * s), tonguePink);
            dl.AddEllipse(fc + new Vector2(0, 10.6f * s + pant), new Vector2(3.4f * s, 4.4f * s), outline, 0f, 12, 1.2f * s);
            dl.AddLine(fc + new Vector2(0, 7.6f * s), fc + new Vector2(0, 12.2f * s + pant), Theme.Rgba(0.82f, 0.30f, 0.44f), 1.1f * s);
        }

        // Cute doggy muzzle smile lines
        dl.AddLine(fc + new Vector2(0, 6.0f * s), fc + new Vector2(-4.5f * s, 8.6f * s), creamChest, 1.5f * s);
        dl.AddLine(fc + new Vector2(0, 6.0f * s), fc + new Vector2(4.5f * s, 8.6f * s), creamChest, 1.5f * s);
    }

    /// <summary>
    /// Draws a chubby, mischievous Ragnarok-inspired chibi Deviruchi:
    /// pointed devil ears, flapping bat wings, spade-tipped devil tail, glowing yellow eyes,
    /// rosy cheeks, cheeky grin, and an optional iconic little black trident!
    /// </summary>
    private static void DrawDeviruchi(ImDrawListPtr dl, Vector2 c, float s, PetMood mood, float t, uint bodyCol, uint wingCol, uint eyeCol, bool showTrident)
    {
        var outline = Theme.Rgba(0.08f, 0.03f, 0.12f);
        var bodyV = ImGui.ColorConvertU32ToFloat4(bodyCol);
        var highlightCol = Theme.Rgba(
            MathF.Min(1f, bodyV.X + 0.14f),
            MathF.Min(1f, bodyV.Y + 0.11f),
            MathF.Min(1f, bodyV.Z + 0.14f),
            0.55f);
        var shadowCol = Theme.Rgba(bodyV.X * 0.76f, bodyV.Y * 0.74f, bodyV.Z * 0.80f, 1f);

        var wingFlap = mood == PetMood.Sleeping ? 0f : MathF.Sin(t * 7.5f) * 5.2f * s;
        var tailWag = mood == PetMood.Sleeping ? 0f : MathF.Sin(t * 4.8f) * 4.0f * s;

        // 1) Devil tail with pointed spade tip (bottom-left behind body)
        var tBase = c + new Vector2(-21f * s, 18f * s);
        var tMid = c + new Vector2(-33f * s, 14f * s + tailWag * 0.4f);
        var tTip = c + new Vector2(-36f * s, 2f * s + tailWag);
        dl.AddLine(tBase, tMid, outline, 5.4f * s);
        dl.AddLine(tMid, tTip, outline, 5.0f * s);
        dl.AddLine(tBase, tMid, wingCol, 3.0f * s);
        dl.AddLine(tMid, tTip, wingCol, 2.8f * s);
        // Spade / arrow tip
        var sp1 = tTip + new Vector2(-6.5f * s, 1.5f * s);
        var sp2 = tTip + new Vector2(1.5f * s, -8.5f * s);
        var sp3 = tTip + new Vector2(6.0f * s, 2.5f * s);
        dl.AddTriangleFilled(sp1, sp2, sp3, wingCol);
        dl.AddTriangle(sp1, sp2, sp3, outline, 1.8f * s);

        // 2) Flapping scalloped bat wings on left & right sides
        void DrawBatWing(float dir)
        {
            var root = c + new Vector2(dir * 22f * s, -7f * s);
            var tip = c + new Vector2(dir * 44f * s, -16f * s + wingFlap);
            var mid1 = c + new Vector2(dir * 37f * s, -2f * s + wingFlap * 0.65f);
            var mid2 = c + new Vector2(dir * 29f * s, 4f * s + wingFlap * 0.35f);
            var low = c + new Vector2(dir * 21f * s, 1f * s);

            dl.AddTriangleFilled(root, tip, mid1, wingCol);
            dl.AddTriangleFilled(root, mid1, mid2, wingCol);
            dl.AddTriangleFilled(root, mid2, low, wingCol);

            dl.AddLine(root, tip, outline, 2.2f * s);
            dl.AddLine(tip, mid1, outline, 2.0f * s);
            dl.AddLine(mid1, mid2, outline, 2.0f * s);
            dl.AddLine(mid2, low, outline, 1.9f * s);
        }
        DrawBatWing(-1f);
        DrawBatWing(1f);

        // 3) Cute stubby legs at bottom
        dl.AddEllipseFilled(c + new Vector2(-15f * s, 27f * s), new Vector2(7.5f * s, 6.2f * s), shadowCol);
        dl.AddEllipse(c + new Vector2(-15f * s, 27f * s), new Vector2(7.5f * s, 6.2f * s), outline, 0f, 16, 2.0f * s);
        dl.AddEllipseFilled(c + new Vector2(15f * s, 27f * s), new Vector2(7.5f * s, 6.2f * s), shadowCol);
        dl.AddEllipse(c + new Vector2(15f * s, 27f * s), new Vector2(7.5f * s, 6.2f * s), outline, 0f, 16, 2.0f * s);

        // 4) Stubby little side paws
        dl.AddEllipseFilled(c + new Vector2(-26f * s, 6f * s), new Vector2(6.5f * s, 4.6f * s), bodyCol);
        dl.AddEllipse(c + new Vector2(-26f * s, 6f * s), new Vector2(6.5f * s, 4.6f * s), outline, 0f, 14, 1.9f * s);
        dl.AddEllipseFilled(c + new Vector2(26f * s, 6f * s), new Vector2(6.5f * s, 4.6f * s), bodyCol);
        dl.AddEllipse(c + new Vector2(26f * s, 6f * s), new Vector2(6.5f * s, 4.6f * s), outline, 0f, 14, 1.9f * s);

        // 5) Iconic chubby rounded-rectangle Deviruchi blob body!
        var bodyMin = c + new Vector2(-24.5f * s, -22f * s);
        var bodyMax = c + new Vector2(24.5f * s, 28f * s);
        dl.AddRectFilled(bodyMin, bodyMax, bodyCol, 15f * s);
        // Subtle chubby lower shading + soft crown highlight
        dl.AddEllipseFilled(c + new Vector2(0, -15f * s), new Vector2(13f * s, 4.8f * s), highlightCol);
        dl.AddRect(bodyMin, bodyMax, outline, 15f * s, ImDrawFlags.None, 2.3f * s);

        // 6) Two cute pointed devil/cat ears rising from top corners
        var earTwitch = MathF.Sin(t * 3.5f) * 1.2f * s;
        var lE1 = c + new Vector2(-22f * s, -15f * s);
        var lE2 = c + new Vector2(-20f * s, -34f * s + earTwitch);
        var lE3 = c + new Vector2(-8f * s, -21f * s);
        dl.AddTriangleFilled(lE1, lE2, lE3, bodyCol);
        dl.AddLine(lE1, lE2, outline, 2.2f * s);
        dl.AddLine(lE2, lE3, outline, 2.2f * s);

        var rE1 = c + new Vector2(22f * s, -15f * s);
        var rE2 = c + new Vector2(20f * s, -34f * s - earTwitch);
        var rE3 = c + new Vector2(8f * s, -21f * s);
        dl.AddTriangleFilled(rE1, rE2, rE3, bodyCol);
        dl.AddLine(rE1, rE2, outline, 2.2f * s);
        dl.AddLine(rE2, rE3, outline, 2.2f * s);

        // 7) Iconic little black Deviruchi Trident held in left paw (when no drawn job weapon overlaps it!)
        if (showTrident)
        {
            var jab = mood == PetMood.Cheering ? MathF.Sin(t * 8f) * 2.5f * s : MathF.Sin(t * 2.8f) * 1.2f * s;
            var staffBot = c + new Vector2(-22f * s, 16f * s + jab);
            var staffTop = c + new Vector2(-34f * s, -20f * s + jab);
            dl.AddLine(staffBot, staffTop, outline, 2.8f * s);

            // Trident fork & 3 sharp prongs
            var forkBase = c + new Vector2(-32.5f * s, -15f * s + jab);
            var lProngBase = forkBase + new Vector2(-7.5f * s, -1.5f * s);
            var rProngBase = forkBase + new Vector2(6.5f * s, -4.5f * s);
            dl.AddLine(lProngBase, rProngBase, outline, 2.4f * s);

            var lTip = lProngBase + new Vector2(-3.5f * s, -11.5f * s);
            var mTip = staffTop + new Vector2(-3.5f * s, -10.5f * s);
            var rTip = rProngBase + new Vector2(-2.0f * s, -11.5f * s);
            dl.AddLine(lProngBase, lTip, outline, 2.2f * s);
            dl.AddLine(staffTop, mTip, outline, 2.4f * s);
            dl.AddLine(rProngBase, rTip, outline, 2.2f * s);

            // Tiny arrowheads on each prong
            dl.AddTriangleFilled(lTip, lTip + new Vector2(-2.2f * s, 4.0f * s), lTip + new Vector2(3.0f * s, 2.8f * s), outline);
            dl.AddTriangleFilled(mTip, mTip + new Vector2(-2.6f * s, 4.2f * s), mTip + new Vector2(3.0f * s, 3.2f * s), outline);
            dl.AddTriangleFilled(rTip, rTip + new Vector2(-2.5f * s, 3.8f * s), rTip + new Vector2(2.6f * s, 3.4f * s), outline);
        }

        // 8) Mischievous glowing eyes, rosy cheeks & cheeky grin!
        var fc = c + new Vector2(0, -1f * s);
        var pinkBlush = Theme.Rgba(0.96f, 0.48f, 0.68f, 0.50f);
        dl.AddEllipseFilled(fc + new Vector2(-14.5f * s, 3.5f * s), new Vector2(4.2f * s, 2.6f * s), pinkBlush);
        dl.AddEllipseFilled(fc + new Vector2(14.5f * s, 3.5f * s), new Vector2(4.2f * s, 2.6f * s), pinkBlush);

        var isBlinking = (t % 4.4f) > 4.22f;
        var lEye = fc + new Vector2(-9.5f * s, -2.5f * s);
        var rEye = fc + new Vector2(9.5f * s, -2.5f * s);

        if (mood == PetMood.Sleeping || isBlinking)
        {
            dl.AddLine(lEye + new Vector2(-4f * s, 0), lEye + new Vector2(4f * s, 0), eyeCol, 2.2f * s);
            dl.AddLine(rEye + new Vector2(-4f * s, 0), rEye + new Vector2(4f * s, 0), eyeCol, 2.2f * s);
        }
        else if (mood is PetMood.Happy or PetMood.Petted or PetMood.Celebrating)
        {
            // Cute glowing "^ ^" happy devil eyes!
            dl.AddLine(lEye + new Vector2(-4.2f * s, 2f * s), lEye + new Vector2(0, -3f * s), eyeCol, 2.4f * s);
            dl.AddLine(lEye + new Vector2(0, -3f * s), lEye + new Vector2(4.2f * s, 2f * s), eyeCol, 2.4f * s);
            dl.AddLine(rEye + new Vector2(-4.2f * s, 2f * s), rEye + new Vector2(0, -3f * s), eyeCol, 2.4f * s);
            dl.AddLine(rEye + new Vector2(0, -3f * s), rEye + new Vector2(4.2f * s, 2f * s), eyeCol, 2.4f * s);
        }
        else if (mood == PetMood.Crying)
        {
            dl.AddLine(lEye + new Vector2(-3.5f * s, -3f * s), lEye + new Vector2(3.5f * s, 3f * s), eyeCol, 2.2f * s);
            dl.AddLine(lEye + new Vector2(-3.5f * s, 3f * s), lEye + new Vector2(3.5f * s, -3f * s), eyeCol, 2.2f * s);
            dl.AddLine(rEye + new Vector2(-3.5f * s, -3f * s), rEye + new Vector2(3.5f * s, 3f * s), eyeCol, 2.2f * s);
            dl.AddLine(rEye + new Vector2(-3.5f * s, 3f * s), rEye + new Vector2(3.5f * s, -3f * s), eyeCol, 2.2f * s);
        }
        else
        {
            // Iconic glowing yellow vertical oval eyes!
            dl.AddEllipseFilled(lEye, new Vector2(2.6f * s, 3.8f * s), eyeCol);
            dl.AddEllipseFilled(rEye, new Vector2(2.6f * s, 3.8f * s), eyeCol);
            if (mood == PetMood.Cheering)
            {
                // Fierce little battle eyebrows
                dl.AddLine(lEye + new Vector2(-5f * s, -7f * s), lEye + new Vector2(4.5f * s, -3.5f * s), outline, 2.4f * s);
                dl.AddLine(rEye + new Vector2(5f * s, -7f * s), rEye + new Vector2(-4.5f * s, -3.5f * s), outline, 2.4f * s);
            }
        }

        // Cheeky glowing smile with cute little fang/drool!
        if (mood is PetMood.Panic or PetMood.Crying)
        {
            dl.AddCircle(fc + new Vector2(0, 8f * s), 3.4f * s, eyeCol, 14, 1.8f * s);
        }
        else if (mood is PetMood.Hungry or PetMood.Grumpy)
        {
            dl.AddLine(fc + new Vector2(-6f * s, 7.5f * s), fc + new Vector2(6f * s, 7.5f * s), eyeCol, 2.2f * s);
        }
        else
        {
            // Classic wide cheeky Deviruchi grin + tiny cute fang on the right!
            dl.AddLine(fc + new Vector2(-9f * s, 6.5f * s), fc + new Vector2(9f * s, 7.2f * s), eyeCol, 2.4f * s);
            dl.AddTriangleFilled(
                fc + new Vector2(4.5f * s, 7.0f * s),
                fc + new Vector2(8.5f * s, 7.0f * s),
                fc + new Vector2(6.5f * s, 11.8f * s),
                eyeCol);
        }
    }

    private static void DrawCuteFace(ImDrawListPtr dl, Vector2 fc, float s, PetMood mood, float t, uint detailCol, bool skipMouth = false)
    {
        var dark = Theme.Rgba(0.10f, 0.02f, 0.18f);
        var blushV = ImGui.ColorConvertU32ToFloat4(detailCol);
        var blush = Theme.Rgba(blushV.X, blushV.Y, blushV.Z, 0.42f);

        // Rosy cheeks
        dl.AddCircleFilled(fc + new Vector2(-15f * s, 4f * s), 4.2f * s, blush);
        dl.AddCircleFilled(fc + new Vector2(15f * s, 4f * s), 4.2f * s, blush);

        var isBlinking = (t % 4.2f) > 4.02f;
        var leftEye = fc + new Vector2(-9f * s, -2f * s);
        var rightEye = fc + new Vector2(9f * s, -2f * s);

        if (mood == PetMood.Sleeping || isBlinking)
        {
            dl.AddLine(leftEye + new Vector2(-4f * s, 0), leftEye + new Vector2(4f * s, 0), dark, 2.2f * s);
            dl.AddLine(rightEye + new Vector2(-4f * s, 0), rightEye + new Vector2(4f * s, 0), dark, 2.2f * s);
        }
        else if (mood is PetMood.Happy or PetMood.Petted or PetMood.Celebrating)
        {
            // Happy "^ ^" eyes
            dl.AddLine(leftEye + new Vector2(-4f * s, 2f * s), leftEye + new Vector2(0, -3f * s), dark, 2.2f * s);
            dl.AddLine(leftEye + new Vector2(0, -3f * s), leftEye + new Vector2(4f * s, 2f * s), dark, 2.2f * s);
            dl.AddLine(rightEye + new Vector2(-4f * s, 2f * s), rightEye + new Vector2(0, -3f * s), dark, 2.2f * s);
            dl.AddLine(rightEye + new Vector2(0, -3f * s), rightEye + new Vector2(4f * s, 2f * s), dark, 2.2f * s);
        }
        else if (mood == PetMood.Crying)
        {
            // "X X" / sad eyes
            dl.AddLine(leftEye + new Vector2(-3.5f * s, -3f * s), leftEye + new Vector2(3.5f * s, 3f * s), dark, 2.2f * s);
            dl.AddLine(leftEye + new Vector2(-3.5f * s, 3f * s), leftEye + new Vector2(3.5f * s, -3f * s), dark, 2.2f * s);
            dl.AddLine(rightEye + new Vector2(-3.5f * s, -3f * s), rightEye + new Vector2(3.5f * s, 3f * s), dark, 2.2f * s);
            dl.AddLine(rightEye + new Vector2(-3.5f * s, 3f * s), rightEye + new Vector2(3.5f * s, -3f * s), dark, 2.2f * s);
        }
        else if (mood == PetMood.Panic)
        {
            // Wide shocked eyes
            dl.AddCircleFilled(leftEye, 5.2f * s, Theme.Rgba(1f, 1f, 1f));
            dl.AddCircle(leftEye, 5.2f * s, dark, 16, 1.8f * s);
            dl.AddCircleFilled(leftEye, 2.0f * s, dark);
            dl.AddCircleFilled(rightEye, 5.2f * s, Theme.Rgba(1f, 1f, 1f));
            dl.AddCircle(rightEye, 5.2f * s, dark, 16, 1.8f * s);
            dl.AddCircleFilled(rightEye, 2.0f * s, dark);
        }
        else if (mood == PetMood.Cheering)
        {
            // Fierce / angry combat eyes + furrowed eyebrows (\ /)
            dl.AddCircleFilled(leftEye, 3.8f * s, dark);
            dl.AddCircleFilled(leftEye + new Vector2(-1.0f * s, -1.0f * s), 1.2f * s, Theme.Rgba(1f, 1f, 1f));
            dl.AddCircleFilled(rightEye, 3.8f * s, dark);
            dl.AddCircleFilled(rightEye + new Vector2(-1.0f * s, -1.0f * s), 1.2f * s, Theme.Rgba(1f, 1f, 1f));
            // Slanted angry eyebrows
            dl.AddLine(leftEye + new Vector2(-5.5f * s, -7.5f * s), leftEye + new Vector2(4.5f * s, -3.5f * s), dark, 2.5f * s);
            dl.AddLine(rightEye + new Vector2(5.5f * s, -7.5f * s), rightEye + new Vector2(-4.5f * s, -3.5f * s), dark, 2.5f * s);
        }
        else
        {
            // Cute shiny dot eyes (also used for Working / Crafting / Gathering — focused & friendly!)
            dl.AddCircleFilled(leftEye, 3.8f * s, dark);
            dl.AddCircleFilled(leftEye + new Vector2(-1.2f * s, -1.2f * s), 1.3f * s, Theme.Rgba(1f, 1f, 1f));
            dl.AddCircleFilled(rightEye, 3.8f * s, dark);
            dl.AddCircleFilled(rightEye + new Vector2(-1.2f * s, -1.2f * s), 1.3f * s, Theme.Rgba(1f, 1f, 1f));
        }

        if (skipMouth) return;

        // Mouth
        if (mood == PetMood.Cheering)
        {
            // Determined battle shout / fierce grin!
            dl.AddTriangleFilled(fc + new Vector2(-4.5f * s, 6f * s), fc + new Vector2(4.5f * s, 6f * s), fc + new Vector2(0, 11.5f * s), dark);
        }
        else if (mood is PetMood.Panic or PetMood.Crying)
        {
            dl.AddCircle(fc + new Vector2(0, 8f * s), 3.6f * s, dark, 14, 1.8f * s);
        }
        else if (mood is PetMood.Hungry or PetMood.Grumpy)
        {
            dl.AddLine(fc + new Vector2(-4f * s, 9f * s), fc + new Vector2(4f * s, 9f * s), dark, 2.0f * s);
        }
        else
        {
            // Cute ":3" smile
            dl.AddLine(fc + new Vector2(-4f * s, 6f * s), fc + new Vector2(0, 9f * s), dark, 1.8f * s);
            dl.AddLine(fc + new Vector2(0, 9f * s), fc + new Vector2(4f * s, 6f * s), dark, 1.8f * s);
        }
    }

    private static void DrawAccessory(ImDrawListPtr dl, Vector2 c, float s, PetSpecies sp, PetAccessory acc, float t)
    {
        var headTop = c + new Vector2(0, -20f * s);
        var outline = Theme.Rgba(0.10f, 0.02f, 0.22f);

        switch (acc)
        {
            case PetAccessory.Sunglasses:
            {
                var yOff = sp == PetSpecies.Moogle ? -1f * s : 2f * s;
                var gy = c.Y + yOff;
                dl.AddRectFilled(new Vector2(c.X - 18f * s, gy - 4f * s), new Vector2(c.X - 2f * s, gy + 4f * s), Theme.Rgba(0.08f, 0.02f, 0.16f), 2f * s);
                dl.AddRectFilled(new Vector2(c.X + 2f * s, gy - 4f * s), new Vector2(c.X + 18f * s, gy + 4f * s), Theme.Rgba(0.08f, 0.02f, 0.16f), 2f * s);
                dl.AddLine(new Vector2(c.X - 2f * s, gy - 1f * s), new Vector2(c.X + 2f * s, gy - 1f * s), Theme.ColU32(Theme.YellowMain), 2f * s);
                dl.AddRect(new Vector2(c.X - 18f * s, gy - 4f * s), new Vector2(c.X - 2f * s, gy + 4f * s), Theme.ColU32(Theme.YellowMain), 2f * s, ImDrawFlags.None, 1.5f * s);
                dl.AddRect(new Vector2(c.X + 2f * s, gy - 4f * s), new Vector2(c.X + 18f * s, gy + 4f * s), Theme.ColU32(Theme.YellowMain), 2f * s, ImDrawFlags.None, 1.5f * s);
                break;
            }
            case PetAccessory.PartyHat:
            {
                dl.AddTriangleFilled(headTop + new Vector2(-11f * s, 2f * s), headTop + new Vector2(0, -22f * s), headTop + new Vector2(11f * s, 2f * s), Theme.ColU32(Theme.PurpleMain));
                dl.AddTriangle(headTop + new Vector2(-11f * s, 2f * s), headTop + new Vector2(0, -22f * s), headTop + new Vector2(11f * s, 2f * s), outline, 1.8f * s);
                dl.AddCircleFilled(headTop + new Vector2(0, -23f * s), 4f * s, Theme.ColU32(Theme.YellowMain));
                break;
            }
            case PetAccessory.RoyalCrown:
            {
                var gold = Theme.ColU32(Theme.YellowMain);
                dl.AddRectFilled(headTop + new Vector2(-12f * s, -4f * s), headTop + new Vector2(12f * s, 3f * s), gold, 2f * s);
                dl.AddTriangleFilled(headTop + new Vector2(-12f * s, -4f * s), headTop + new Vector2(-10f * s, -14f * s), headTop + new Vector2(-3f * s, -4f * s), gold);
                dl.AddTriangleFilled(headTop + new Vector2(-5f * s, -4f * s), headTop + new Vector2(0, -17f * s), headTop + new Vector2(5f * s, -4f * s), gold);
                dl.AddTriangleFilled(headTop + new Vector2(3f * s, -4f * s), headTop + new Vector2(10f * s, -14f * s), headTop + new Vector2(12f * s, -4f * s), gold);
                dl.AddCircleFilled(headTop + new Vector2(0, -1f * s), 2.5f * s, Theme.ColU32(Theme.PurpleMain));
                break;
            }
            case PetAccessory.GoldenHalo:
            {
                var hoverY = MathF.Sin(t * 3.5f) * 2f * s;
                dl.AddEllipse(headTop + new Vector2(0, -14f * s + hoverY), new Vector2(16f * s, 5f * s), Theme.ColU32(Theme.YellowMain), 0f, 24, 3f * s);
                break;
            }
            case PetAccessory.WizardHat:
            {
                var purple = Theme.ColU32(Theme.PurpleMain);
                var yellow = Theme.ColU32(Theme.YellowMain);
                dl.AddEllipseFilled(headTop + new Vector2(0, 1f * s), new Vector2(22f * s, 5.5f * s), purple);
                dl.AddTriangleFilled(headTop + new Vector2(-13f * s, 0), headTop + new Vector2(4f * s, -27f * s), headTop + new Vector2(13f * s, 0), purple);
                dl.AddLine(headTop + new Vector2(-12f * s, -2f * s), headTop + new Vector2(12f * s, -2f * s), yellow, 3f * s);
                dl.AddCircleFilled(headTop + new Vector2(4f * s, -27f * s), 3.2f * s, yellow);
                break;
            }
            case PetAccessory.CuteBow:
            {
                var pink = Theme.ColU32(Theme.PinkAccent);
                dl.AddTriangleFilled(headTop + new Vector2(-15f * s, -8f * s), headTop + new Vector2(-15f * s, 4f * s), headTop + new Vector2(0, -2f * s), pink);
                dl.AddTriangleFilled(headTop + new Vector2(15f * s, -8f * s), headTop + new Vector2(15f * s, 4f * s), headTop + new Vector2(0, -2f * s), pink);
                dl.AddCircleFilled(headTop + new Vector2(0, -2f * s), 4.2f * s, Theme.ColU32(Theme.YellowMain));
                break;
            }
            case PetAccessory.Bandana:
            {
                var (bc, rx, ry) = GetBodyMetrics(sp, c, s);
                var red = Theme.Rgba(0.90f, 0.20f, 0.28f);
                var bandY = bc.Y - ry * 0.62f;
                var halfW = rx * 0.80f;
                dl.AddRectFilled(new Vector2(bc.X - halfW, bandY - 3f * s), new Vector2(bc.X + halfW, bandY + 3.2f * s), red, 3f * s);
                dl.AddRect(new Vector2(bc.X - halfW, bandY - 3f * s), new Vector2(bc.X + halfW, bandY + 3.2f * s), outline, 3f * s, ImDrawFlags.None, 1.3f * s);
                dl.AddTriangleFilled(new Vector2(bc.X + halfW - 2f * s, bandY), new Vector2(bc.X + halfW + 11f * s, bandY - 4f * s), new Vector2(bc.X + halfW + 9f * s, bandY + 6f * s), red);
                dl.AddCircleFilled(new Vector2(bc.X, bandY), 2.8f * s, Theme.ColU32(Theme.YellowMain));
                break;
            }
            case PetAccessory.ChefHat:
            {
                var white = Theme.Rgba(0.98f, 0.98f, 1.0f);
                dl.AddRectFilled(headTop + new Vector2(-13f * s, -9f * s), headTop + new Vector2(13f * s, 2f * s), white, 2f * s);
                dl.AddCircleFilled(headTop + new Vector2(-9f * s, -12f * s), 8f * s, white);
                dl.AddCircleFilled(headTop + new Vector2(0, -16f * s), 9.5f * s, white);
                dl.AddCircleFilled(headTop + new Vector2(9f * s, -12f * s), 8f * s, white);
                dl.AddLine(headTop + new Vector2(-13f * s, 1f * s), headTop + new Vector2(13f * s, 1f * s), outline, 1.8f * s);
                break;
            }
            case PetAccessory.WitchHat:
            {
                var darkP = Theme.Rgba(0.16f, 0.04f, 0.28f);
                var pink = Theme.ColU32(Theme.PinkAccent);
                dl.AddEllipseFilled(headTop + new Vector2(0, 2f * s), new Vector2(25f * s, 5.5f * s), darkP);
                dl.AddTriangleFilled(headTop + new Vector2(-14f * s, 1f * s), headTop + new Vector2(-4f * s, -26f * s), headTop + new Vector2(14f * s, 1f * s), darkP);
                dl.AddLine(headTop + new Vector2(-13f * s, -1f * s), headTop + new Vector2(13f * s, -1f * s), pink, 3.2f * s);
                break;
            }
            case PetAccessory.DemonHorns:
            {
                var crimson = Theme.Rgba(0.86f, 0.14f, 0.24f);
                dl.AddTriangleFilled(headTop + new Vector2(-16f * s, 3f * s), headTop + new Vector2(-24f * s, -18f * s), headTop + new Vector2(-7f * s, 0), crimson);
                dl.AddTriangle(headTop + new Vector2(-16f * s, 3f * s), headTop + new Vector2(-24f * s, -18f * s), headTop + new Vector2(-7f * s, 0), outline, 1.8f * s);
                dl.AddTriangleFilled(headTop + new Vector2(16f * s, 3f * s), headTop + new Vector2(24f * s, -18f * s), headTop + new Vector2(7f * s, 0), crimson);
                dl.AddTriangle(headTop + new Vector2(16f * s, 3f * s), headTop + new Vector2(24f * s, -18f * s), headTop + new Vector2(7f * s, 0), outline, 1.8f * s);
                break;
            }
            case PetAccessory.CelestialDiadem:
            {
                var gold = Theme.ColU32(Theme.YellowMain);
                var cyan = Theme.ColU32(Theme.CyanAccent);
                dl.AddEllipse(headTop + new Vector2(0, -10f * s), new Vector2(20f * s, 5.5f * s), gold, 0f, 24, 2.8f * s);
                dl.AddQuadFilled(headTop + new Vector2(0, -22f * s), headTop + new Vector2(5f * s, -13f * s), headTop + new Vector2(0, -6f * s), headTop + new Vector2(-5f * s, -13f * s), cyan);
                for (var i = 0; i < 3; i++)
                {
                    var a = t * 2.4f + i * (MathF.PI * 2f / 3f);
                    var spPos = headTop + new Vector2(MathF.Cos(a) * 24f * s, -13f * s + MathF.Sin(a) * 6f * s);
                    dl.AddCircleFilled(spPos, 2.5f * s, i % 2 == 0 ? gold : cyan);
                }
                break;
            }
        }
    }

    private static (Vector2 BodyCenter, float Rx, float Ry) GetBodyMetrics(PetSpecies sp, Vector2 c, float s) => sp switch
    {
        PetSpecies.Carbuncle => (c + new Vector2(0, 4f * s), 27f * s, 24f * s),
        PetSpecies.FatCat => (c + new Vector2(0, 6f * s), 33f * s, 26f * s),
        PetSpecies.Moogle => (c + new Vector2(0, 4f * s), 27f * s, 25f * s),
        PetSpecies.FluffyKitty => (c + new Vector2(0, 5f * s), 30f * s, 25f * s),
        PetSpecies.ShepherdDog => (c + new Vector2(0, 5f * s), 31f * s, 25.5f * s),
        PetSpecies.Deviruchi   => (c + new Vector2(0, 4f * s), 25.5f * s, 24.5f * s),
        _ => (c + new Vector2(0, 4f * s), 30f * s, 25f * s),
    };

    /// <summary>
    /// Draws a form-fitting garment hugging the exact lower curve of the pet's round body ellipse.
    /// </summary>
    private static void DrawLowerBodyGarment(
        ImDrawListPtr dl,
        Vector2 bc,
        float rx,
        float ry,
        float startAngle,
        float neckDip,
        uint fillCol,
        uint outlineCol,
        float outlineThickness,
        uint? topTrimCol = null,
        float trimThickness = 0f)
    {
        const int segs = 20;
        var endAngle = MathF.PI - startAngle;
        var topLeft = new Vector2(bc.X + MathF.Cos(endAngle) * rx, bc.Y + MathF.Sin(endAngle) * ry);
        var topRight = new Vector2(bc.X + MathF.Cos(startAngle) * rx, bc.Y + MathF.Sin(startAngle) * ry);
        var neckMidY = topLeft.Y + neckDip;

        // Fill with thin vertical quads between curved neckline and exact lower ellipse arc
        for (var i = 0; i < segs; i++)
        {
            var t0 = i / (float)segs;
            var t1 = (i + 1) / (float)segs;
            var a0 = startAngle + (endAngle - startAngle) * t0;
            var a1 = startAngle + (endAngle - startAngle) * t1;

            var b0 = new Vector2(bc.X + MathF.Cos(a0) * rx, bc.Y + MathF.Sin(a0) * ry);
            var b1 = new Vector2(bc.X + MathF.Cos(a1) * rx, bc.Y + MathF.Sin(a1) * ry);

            // Smooth parabolic neckline dip (0 at edges, neckDip at center)
            var d0 = 4f * t0 * (1f - t0) * neckDip;
            var d1 = 4f * t1 * (1f - t1) * neckDip;
            var n0 = new Vector2(b0.X, MathF.Min(b0.Y, topRight.Y + d0));
            var n1 = new Vector2(b1.X, MathF.Min(b1.Y, topRight.Y + d1));

            dl.AddQuadFilled(n0, n1, b1, b0, fillCol);
        }

        // Outer body-hugging bottom rim outline
        dl.PathClear();
        for (var i = 0; i <= segs; i++)
        {
            var t = i / (float)segs;
            var a = startAngle + (endAngle - startAngle) * t;
            dl.PathLineTo(new Vector2(bc.X + MathF.Cos(a) * rx, bc.Y + MathF.Sin(a) * ry));
        }
        dl.PathStroke(outlineCol, ImDrawFlags.None, outlineThickness);

        // Top neckline stroke (trim or outline)
        dl.PathClear();
        for (var i = 0; i <= segs; i++)
        {
            var t = i / (float)segs;
            var a = startAngle + (endAngle - startAngle) * t;
            var bx = bc.X + MathF.Cos(a) * rx;
            var by = bc.Y + MathF.Sin(a) * ry;
            var ny = MathF.Min(by, topRight.Y + 4f * t * (1f - t) * neckDip);
            dl.PathLineTo(new Vector2(bx, ny));
        }
        dl.PathStroke(topTrimCol ?? outlineCol, ImDrawFlags.None, topTrimCol.HasValue ? trimThickness : outlineThickness);
        _ = neckMidY;
    }

    private static void DrawOutfitBack(ImDrawListPtr dl, Vector2 c, float s, PetSpecies sp, PetOutfit outfit, float t)
    {
        var (bc, rx, ry) = GetBodyMetrics(sp, c, s);
        var outline = Theme.Rgba(0.10f, 0.02f, 0.22f);

        switch (outfit)
        {
            case PetOutfit.HeroCape:
            {
                var sway = MathF.Sin(t * 3.5f) * 4f * s;
                var crimson = Theme.Rgba(0.84f, 0.14f, 0.24f);
                var gold = Theme.ColU32(Theme.YellowMain);
                var p1 = bc + new Vector2(-rx * 0.86f, ry * 0.08f);
                var p2 = bc + new Vector2(rx * 0.86f, ry * 0.08f);
                var p3 = bc + new Vector2(rx * 1.18f + sway, ry * 1.18f);
                var p4 = bc + new Vector2(-rx * 1.18f + sway, ry * 1.18f);
                dl.AddQuadFilled(p1, p2, p3, p4, crimson);
                dl.AddQuad(p1, p2, p3, p4, outline, 2.0f * s);
                dl.AddLine(p4 + new Vector2(2f * s, -2f * s), p3 + new Vector2(-2f * s, -2f * s), gold, 2.6f * s);
                break;
            }
            case PetOutfit.ArchmageRobe:
            {
                // Royal high-collared mage mantle peeking behind the pet's shoulders
                var darkPurple = Theme.Rgba(0.28f, 0.04f, 0.56f);
                var gold = Theme.ColU32(Theme.YellowMain);
                var p1 = bc + new Vector2(-rx * 1.14f, -ry * 0.18f);
                var p2 = bc + new Vector2(rx * 1.14f, -ry * 0.18f);
                var p3 = bc + new Vector2(rx * 1.02f, ry * 0.86f);
                var p4 = bc + new Vector2(-rx * 1.02f, ry * 0.86f);
                dl.AddQuadFilled(p1, p2, p3, p4, darkPurple);
                dl.AddLine(p1, p4, gold, 2.2f * s);
                dl.AddLine(p2, p3, gold, 2.2f * s);
                dl.AddLine(p1, bc + new Vector2(-rx * 0.72f, -ry * 0.34f), gold, 2.2f * s);
                dl.AddLine(p2, bc + new Vector2(rx * 0.72f, -ry * 0.34f), gold, 2.2f * s);
                break;
            }
            case PetOutfit.SeraphWings:
            {
                var flap = MathF.Sin(t * 5.0f) * 6.5f * s;
                var white = Theme.Rgba(0.98f, 0.98f, 1.0f);
                var paleGold = Theme.Rgba(1.0f, 0.95f, 0.72f);
                var gold = Theme.ColU32(Theme.YellowMain);
                var anchorX = rx * 0.72f;
                // Left layered angel wing (3 feather tiers)
                for (var side = -1; side <= 1; side += 2)
                {
                    var root = bc + new Vector2(side * anchorX, 0f);
                    var tip1 = bc + new Vector2(side * (rx + 28f * s), -24f * s + flap);
                    var tip2 = bc + new Vector2(side * (rx + 23f * s), -4f * s + flap * 0.7f);
                    var tip3 = bc + new Vector2(side * (rx + 15f * s), 13f * s + flap * 0.4f);
                    var low = bc + new Vector2(side * (anchorX + 3f * s), 14f * s);

                    dl.AddTriangleFilled(root, tip3, low, paleGold);
                    dl.AddTriangle(root, tip3, low, gold, 1.6f * s);
                    dl.AddTriangleFilled(root, tip2, tip3, white);
                    dl.AddTriangle(root, tip2, tip3, gold, 1.6f * s);
                    dl.AddTriangleFilled(root, tip1, tip2, white);
                    dl.AddTriangle(root, tip1, tip2, gold, 2.0f * s);
                }
                break;
            }
            case PetOutfit.AstralWings:
            {
                var flap = MathF.Sin(t * 5.8f) * 7.5f * s;
                var pulse = 0.5f + 0.5f * MathF.Sin(t * 4f);
                var voidCol = Theme.Rgba(0.32f, 0.08f, 0.72f);
                var innerCol = Theme.Rgba(0.16f, 0.04f, 0.42f);
                var glowCol = Theme.Rgba(1.0f, 0.90f, 0.0f, 0.90f);
                var cyanGlow = Theme.Rgba(0.25f, 0.86f, 1.0f, 0.28f + pulse * 0.22f);
                var anchorX = rx * 0.70f;

                // Radiant aura behind dragon wings
                dl.AddCircleFilled(bc, (rx + 16f * s + pulse * 4f * s), cyanGlow);

                for (var side = -1; side <= 1; side += 2)
                {
                    var root = bc + new Vector2(side * anchorX, -2f * s);
                    var elbow = bc + new Vector2(side * (rx + 16f * s), -20f * s + flap * 0.7f);
                    var wingTip = bc + new Vector2(side * (rx + 32f * s), -30f * s + flap);
                    var midTip = bc + new Vector2(side * (rx + 26f * s), 2f * s + flap * 0.5f);
                    var lowTip = bc + new Vector2(side * (rx + 12f * s), 18f * s + flap * 0.25f);

                    dl.AddQuadFilled(root, wingTip, midTip, lowTip, voidCol);
                    dl.AddTriangleFilled(root, midTip, lowTip, innerCol);
                    // Glowing golden bone ribs
                    dl.AddLine(root, elbow, glowCol, 2.6f * s);
                    dl.AddLine(elbow, wingTip, glowCol, 2.6f * s);
                    dl.AddLine(elbow, midTip, glowCol, 1.8f * s);
                    dl.AddLine(root, lowTip, glowCol, 1.6f * s);
                    dl.AddCircleFilled(wingTip, 2.8f * s, Theme.ColU32(Theme.CyanAccent));
                }
                break;
            }
        }
    }

    private static void DrawOutfitFront(ImDrawListPtr dl, Vector2 c, float s, PetSpecies sp, PetOutfit outfit, float t)
    {
        var (bc, rx, ry) = GetBodyMetrics(sp, c, s);
        var gold = Theme.ColU32(Theme.YellowMain);
        var purple = Theme.ColU32(Theme.PurpleMain);
        var dark = Theme.Rgba(0.11f, 0.04f, 0.20f);
        var outline = Theme.Rgba(0.10f, 0.02f, 0.22f);

        switch (outfit)
        {
            case PetOutfit.CozyScarf:
            {
                // Curved scarf wrapping cleanly around the lower curve of the pet
                var red = Theme.Rgba(0.88f, 0.20f, 0.30f);
                const int segs = 16;
                var aStart = 0.20f * MathF.PI;
                var aEnd = 0.80f * MathF.PI;
                var bandThick = 6.2f * s;

                for (var i = 0; i < segs; i++)
                {
                    var t0 = i / (float)segs;
                    var t1 = (i + 1) / (float)segs;
                    var a0 = aStart + (aEnd - aStart) * t0;
                    var a1 = aStart + (aEnd - aStart) * t1;

                    var o0 = new Vector2(bc.X + MathF.Cos(a0) * (rx + 0.8f * s), bc.Y + MathF.Sin(a0) * (ry + 0.8f * s));
                    var o1 = new Vector2(bc.X + MathF.Cos(a1) * (rx + 0.8f * s), bc.Y + MathF.Sin(a1) * (ry + 0.8f * s));
                    var i0 = new Vector2(o0.X, o0.Y - bandThick);
                    var i1 = new Vector2(o1.X, o1.Y - bandThick);
                    dl.AddQuadFilled(i0, i1, o1, o0, red);
                }
                // Gold stripe along the scarf curve
                dl.PathClear();
                for (var i = 0; i <= segs; i++)
                {
                    var a = aStart + (aEnd - aStart) * (i / (float)segs);
                    dl.PathLineTo(new Vector2(bc.X + MathF.Cos(a) * rx, bc.Y + MathF.Sin(a) * ry - bandThick * 0.45f));
                }
                dl.PathStroke(gold, ImDrawFlags.None, 1.8f * s);

                // Hanging scarf tail on the right with gentle sway
                var sway = MathF.Sin(t * 3.5f) * 2.5f * s;
                var knot = new Vector2(bc.X + rx * 0.42f, bc.Y + ry * 0.80f);
                var tail1 = knot + new Vector2(-3.5f * s, 0f);
                var tail2 = knot + new Vector2(4.0f * s, 0f);
                var tail3 = knot + new Vector2(6.0f * s + sway, 13f * s);
                var tail4 = knot + new Vector2(-2.0f * s + sway, 13f * s);
                dl.AddQuadFilled(tail1, tail2, tail3, tail4, red);
                dl.AddQuad(tail1, tail2, tail3, tail4, outline, 1.5f * s);
                dl.AddCircleFilled(knot, 3.8f * s, red);
                dl.AddCircle(knot, 3.8f * s, outline, 12, 1.4f * s);
                break;
            }
            case PetOutfit.RedNecktie:
            {
                // Sleek red collar band + classic bright red necktie (Lv. 15 outfit!)
                var tieRed = Theme.Rgba(0.90f, 0.11f, 0.18f);
                var tieShade = Theme.Rgba(0.68f, 0.06f, 0.12f);
                var sway = MathF.Sin(t * 3.4f) * 1.8f * s;

                // Subtle neck strap hugging the chest
                var neckY = bc.Y + ry * 0.42f;
                dl.AddLine(new Vector2(bc.X - rx * 0.56f, neckY - 2f * s), new Vector2(bc.X, neckY + 3f * s), tieShade, 3.0f * s);
                dl.AddLine(new Vector2(bc.X + rx * 0.56f, neckY - 2f * s), new Vector2(bc.X, neckY + 3f * s), tieShade, 3.0f * s);

                // Hanging red tie blade with pointed tip
                var knotPos = new Vector2(bc.X, neckY + 3.2f * s);
                var bTopL = knotPos + new Vector2(-3.2f * s, 1.5f * s);
                var bTopR = knotPos + new Vector2(3.2f * s, 1.5f * s);
                var bMidR = knotPos + new Vector2(6.0f * s + sway * 0.7f, 15.5f * s);
                var bTip  = knotPos + new Vector2(sway, 21.5f * s);
                var bMidL = knotPos + new Vector2(-6.0f * s + sway * 0.7f, 15.5f * s);

                dl.AddQuadFilled(bTopL, bTopR, bMidR, bMidL, tieRed);
                dl.AddTriangleFilled(bMidL, bMidR, bTip, tieRed);
                // Crisp outline around tie blade
                dl.AddLine(bTopL, bMidL, outline, 1.5f * s);
                dl.AddLine(bMidL, bTip, outline, 1.5f * s);
                dl.AddLine(bTip, bMidR, outline, 1.5f * s);
                dl.AddLine(bMidR, bTopR, outline, 1.5f * s);
                // Subtle sheen highlight along the left edge
                dl.AddLine(bTopL + new Vector2(1.4f * s, 2f * s), bMidL + new Vector2(2.2f * s, -1.5f * s), Theme.Rgba(1f, 0.55f, 0.60f, 0.55f), 1.4f * s);

                // Windsor knot on top
                var k1 = knotPos + new Vector2(-4.4f * s, -2.5f * s);
                var k2 = knotPos + new Vector2(4.4f * s, -2.5f * s);
                var k3 = knotPos + new Vector2(3.0f * s, 3.2f * s);
                var k4 = knotPos + new Vector2(-3.0f * s, 3.2f * s);
                dl.AddQuadFilled(k1, k2, k3, k4, tieRed);
                dl.AddQuad(k1, k2, k3, k4, outline, 1.5f * s);
                break;
            }
            case PetOutfit.DapperTux:
            {
                // Sleek curved tuxedo jacket hugging the lower oval belly
                DrawLowerBodyGarment(dl, bc, rx + 0.5f * s, ry + 0.5f * s, 0.22f * MathF.PI, 4.5f * s, dark, outline, 2.0f * s);

                // Crisp white V-shirt in the center
                var topY = bc.Y + ry * MathF.Sin(0.22f * MathF.PI) + 1.5f * s;
                var botY = bc.Y + ry * 0.96f;
                dl.AddTriangleFilled(
                    new Vector2(bc.X - 8.5f * s, topY),
                    new Vector2(bc.X + 8.5f * s, topY),
                    new Vector2(bc.X, botY - 2f * s),
                    Theme.Rgba(0.98f, 0.98f, 1.0f));

                // Satin lapels
                var lapel = Theme.Rgba(0.22f, 0.14f, 0.34f);
                dl.AddLine(new Vector2(bc.X - 9.5f * s, topY), new Vector2(bc.X - 1f * s, botY - 2f * s), lapel, 2.2f * s);
                dl.AddLine(new Vector2(bc.X + 9.5f * s, topY), new Vector2(bc.X + 1f * s, botY - 2f * s), lapel, 2.2f * s);

                // Tiny red bowtie right at the collar
                var bowY = topY + 2.5f * s;
                var red = Theme.Rgba(0.92f, 0.18f, 0.28f);
                dl.AddTriangleFilled(new Vector2(bc.X - 6.5f * s, bowY - 3.2f * s), new Vector2(bc.X - 6.5f * s, bowY + 3.2f * s), new Vector2(bc.X, bowY), red);
                dl.AddTriangleFilled(new Vector2(bc.X + 6.5f * s, bowY - 3.2f * s), new Vector2(bc.X + 6.5f * s, bowY + 3.2f * s), new Vector2(bc.X, bowY), red);
                dl.AddCircleFilled(new Vector2(bc.X, bowY), 1.8f * s, gold);
                break;
            }
            case PetOutfit.HeroCape:
            {
                // Curved crimson collar clasp fastening the cape cleanly onto the pet
                var crimson = Theme.Rgba(0.84f, 0.14f, 0.24f);
                var pL = new Vector2(bc.X - rx * 0.72f, bc.Y + ry * 0.56f);
                var pR = new Vector2(bc.X + rx * 0.72f, bc.Y + ry * 0.56f);
                var pMid = new Vector2(bc.X, bc.Y + ry * 0.76f);
                dl.AddLine(pL, pMid, crimson, 3.4f * s);
                dl.AddLine(pMid, pR, crimson, 3.4f * s);
                dl.AddCircleFilled(pL, 3.2f * s, gold);
                dl.AddCircleFilled(pR, 3.2f * s, gold);
                dl.AddCircle(pL, 3.2f * s, outline, 12, 1.3f * s);
                dl.AddCircle(pR, 3.2f * s, outline, 12, 1.3f * s);
                break;
            }
            case PetOutfit.KnightArmor:
            {
                // Curved steel breastplate hugging the lower body
                var silver = Theme.Rgba(0.82f, 0.86f, 0.94f);
                var steelShade = Theme.Rgba(0.62f, 0.67f, 0.78f);
                DrawLowerBodyGarment(dl, bc, rx + 0.6f * s, ry + 0.6f * s, 0.20f * MathF.PI, 4f * s, silver, outline, 2.0f * s, gold, 2.2f * s);

                // Central chest ridge & cyan crest
                var topY = bc.Y + ry * MathF.Sin(0.20f * MathF.PI) + 4f * s;
                var botY = bc.Y + ry * 0.96f;
                var midY = (topY + botY) * 0.5f;
                dl.AddLine(new Vector2(bc.X, topY), new Vector2(bc.X, botY), steelShade, 1.8f * s);
                dl.AddQuadFilled(
                    new Vector2(bc.X, midY - 4.2f * s),
                    new Vector2(bc.X + 4.2f * s, midY),
                    new Vector2(bc.X, midY + 4.2f * s),
                    new Vector2(bc.X - 4.2f * s, midY),
                    Theme.ColU32(Theme.CyanAccent));
                dl.AddQuad(
                    new Vector2(bc.X, midY - 4.2f * s),
                    new Vector2(bc.X + 4.2f * s, midY),
                    new Vector2(bc.X, midY + 4.2f * s),
                    new Vector2(bc.X - 4.2f * s, midY),
                    gold, 1.4f * s);

                // Fitted shoulder pauldrons hugging the exact sides of the pet
                for (var side = -1; side <= 1; side += 2)
                {
                    var pCenter = bc + new Vector2(side * (rx * 0.86f), ry * 0.44f);
                    dl.AddEllipseFilled(pCenter, new Vector2(8.5f * s, 6.2f * s), silver);
                    dl.AddEllipse(pCenter, new Vector2(8.5f * s, 6.2f * s), gold, 0f, 16, 1.8f * s);
                }
                break;
            }
            case PetOutfit.ArchmageRobe:
            {
                // Curved royal purple robe hugging the exact lower oval of the pet
                DrawLowerBodyGarment(dl, bc, rx + 0.6f * s, ry + 0.6f * s, 0.19f * MathF.PI, 5.5f * s, purple, outline, 2.0f * s, gold, 2.4f * s);

                // Ornate golden stole lines + central arcane brooch
                var topY = bc.Y + ry * MathF.Sin(0.19f * MathF.PI) + 5.5f * s;
                var botY = bc.Y + ry * 0.96f;
                var midY = (topY + botY) * 0.5f;
                dl.AddLine(new Vector2(bc.X - 7f * s, topY), new Vector2(bc.X - 4f * s, botY - 1f * s), gold, 1.8f * s);
                dl.AddLine(new Vector2(bc.X + 7f * s, topY), new Vector2(bc.X + 4f * s, botY - 1f * s), gold, 1.8f * s);

                // Arcane brooch with cyan crystal
                dl.AddQuadFilled(
                    new Vector2(bc.X, midY - 4.8f * s),
                    new Vector2(bc.X + 4.8f * s, midY),
                    new Vector2(bc.X, midY + 4.8f * s),
                    new Vector2(bc.X - 4.8f * s, midY),
                    gold);
                dl.AddCircleFilled(new Vector2(bc.X, midY), 2.1f * s, Theme.ColU32(Theme.CyanAccent));
                break;
            }
        }
    }

    private static void DrawMoodParticles(ImDrawListPtr dl, Vector2 c, float s, PetMood mood, float t)
    {
        switch (mood)
        {
            case PetMood.Sleeping:
            {
                for (var i = 0; i < 3; i++)
                {
                    var phase = (t * 0.6f + i * 0.33f) % 1f;
                    var p = c + new Vector2((18f + phase * 14f) * s, (-18f - phase * 26f) * s);
                    dl.AddText(p, Theme.Rgba(0.85f, 0.80f, 1.0f, 1f - phase), i == 0 ? "Z" : "z");
                }
                break;
            }
            case PetMood.Petted:
            {
                for (var i = 0; i < 4; i++)
                {
                    var phase = (t * 0.9f + i * 0.25f) % 1f;
                    var xOff = MathF.Sin(i * 2.1f + t * 3f) * 22f * s;
                    var p = c + new Vector2(xOff, (-16f - phase * 30f) * s);
                    dl.AddText(p, Theme.Rgba(1.0f, 0.38f, 0.58f, 1f - phase), "♥");
                }
                break;
            }
            case PetMood.Celebrating:
            case PetMood.Cheering:
            case PetMood.Working:
            {
                var count = mood == PetMood.Celebrating ? 8 : 4;
                for (var i = 0; i < count; i++)
                {
                    var angle = (i / (float)count) * MathF.PI * 2f + t * 1.6f;
                    var dist = (30f + MathF.Sin(t * 5f + i) * 7f) * s;
                    var p = c + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist - 6f * s);
                    var col = i % 2 == 0 ? Theme.ColU32(Theme.YellowMain) : Theme.ColU32(Theme.PurpleBright);
                    dl.AddCircleFilled(p, 2.8f * s, col);
                }
                break;
            }
            case PetMood.Panic:
            {
                var drop = (t * 2.2f) % 1f;
                dl.AddCircleFilled(c + new Vector2(-26f * s, (-14f + drop * 16f) * s), 3f * s, Theme.ColU32(Theme.CyanAccent));
                dl.AddCircleFilled(c + new Vector2(26f * s, (-10f + ((drop + 0.5f) % 1f) * 16f) * s), 3f * s, Theme.ColU32(Theme.CyanAccent));
                break;
            }
            case PetMood.Crying:
            {
                var drop = (t * 1.8f) % 1f;
                dl.AddCircleFilled(c + new Vector2(-10f * s, (8f + drop * 16f) * s), 2.8f * s, Theme.ColU32(Theme.CyanAccent));
                dl.AddCircleFilled(c + new Vector2(10f * s, (8f + ((drop + 0.5f) % 1f) * 16f) * s), 2.8f * s, Theme.ColU32(Theme.CyanAccent));
                break;
            }
        }
    }
}
