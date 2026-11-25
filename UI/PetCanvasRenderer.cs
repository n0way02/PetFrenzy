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
    public static bool DrawPetCanvas(string id, PetStateData pet, PetMood mood, float timeSec, float wobblePulse, float sizePx, bool interactive = true)
    {
        ImGui.InvisibleButton(id, new Vector2(sizePx, sizePx));
        var clicked = interactive && ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var hovered = interactive && ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var min = ImGui.GetItemRectMin();
        var center = min + new Vector2(sizePx * 0.5f, sizePx * 0.56f);
        var s = sizePx / 120f;
        var dl = ImGui.GetWindowDrawList();

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
            return clicked;
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
                bobY = -MathF.Abs(MathF.Sin(timeSec * 6.0f)) * 9f * s;
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

        switch (pet.Species)
        {
            case PetSpecies.Carbuncle:
                DrawCarbuncle(dl, petCenter, ps, mood, timeSec);
                break;
            case PetSpecies.FatCat:
                DrawFatCat(dl, petCenter, ps, mood, timeSec);
                break;
            case PetSpecies.Moogle:
                DrawMoogle(dl, petCenter, ps, mood, timeSec);
                break;
            case PetSpecies.Namazu:
                DrawNamazu(dl, petCenter, ps, mood, timeSec);
                break;
        }

        // Draw equipped accessory on top of head
        if (pet.EquippedAccessory != PetAccessory.None)
        {
            DrawAccessory(dl, petCenter, ps, pet.Species, pet.EquippedAccessory, timeSec);
        }

        // Draw mood particles (hearts, Zzz, confetti, tears, sweat)
        DrawMoodParticles(dl, petCenter, s, mood, timeSec);

        return clicked;
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
            PetSpecies.FatCat => (Theme.Rgba(0.95f, 0.92f, 0.86f), Theme.Rgba(0.55f, 0.52f, 0.60f)),
            PetSpecies.Moogle => (Theme.Rgba(0.98f, 0.96f, 1.00f), Theme.Rgba(0.95f, 0.45f, 0.65f)),
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

    private static void DrawCarbuncle(ImDrawListPtr dl, Vector2 c, float s, PetMood mood, float t)
    {
        var bodyCol = Theme.Rgba(0.24f, 0.88f, 0.82f);
        var earInner = Theme.Rgba(0.70f, 0.98f, 0.94f);
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

        // Forehead ruby/gold gem
        dl.AddQuadFilled(
            c + new Vector2(0, -17f * s),
            c + new Vector2(5f * s, -11f * s),
            c + new Vector2(0, -5f * s),
            c + new Vector2(-5f * s, -11f * s),
            Theme.Rgba(1.0f, 0.25f, 0.35f));
        dl.AddCircleFilled(c + new Vector2(-1f * s, -12f * s), 1.5f * s, Theme.Rgba(1f, 0.92f, 0.4f));

        DrawCuteFace(dl, c + new Vector2(0, 5f * s), s, mood, t);
    }

    private static void DrawFatCat(ImDrawListPtr dl, Vector2 c, float s, PetMood mood, float t)
    {
        var bodyCol = Theme.Rgba(0.95f, 0.93f, 0.89f);
        var stripeCol = Theme.Rgba(0.62f, 0.59f, 0.66f);
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

        DrawCuteFace(dl, c + new Vector2(0, 4f * s), s, mood, t);
    }

    private static void DrawMoogle(ImDrawListPtr dl, Vector2 c, float s, PetMood mood, float t)
    {
        var bodyCol = Theme.Rgba(0.98f, 0.97f, 1.00f);
        var wingCol = Theme.Rgba(0.45f, 0.12f, 0.78f);
        var pomCol = Theme.Rgba(1.00f, 0.90f, 0.00f);
        var outline = Theme.Rgba(0.11f, 0.02f, 0.22f);
        var flap = MathF.Sin(t * 9f) * 6f * s;

        // Little purple bat wings
        dl.AddTriangleFilled(c + new Vector2(-24f * s, 4f * s), c + new Vector2(-42f * s, -10f * s + flap), c + new Vector2(-34f * s, 14f * s + flap * 0.5f), wingCol);
        dl.AddTriangleFilled(c + new Vector2(24f * s, 4f * s), c + new Vector2(42f * s, -10f * s + flap), c + new Vector2(34f * s, 14f * s + flap * 0.5f), wingCol);

        // Bouncy pom-pom antenna
        var pomOff = new Vector2(MathF.Sin(t * 4.5f) * 4f * s, -40f * s + MathF.Cos(t * 4.5f) * 2f * s);
        dl.AddLine(c + new Vector2(0, -20f * s), c + pomOff, outline, 2.2f * s);
        dl.AddCircleFilled(c + pomOff, 8f * s, Theme.Rgba(1f, 0.9f, 0f, 0.30f));
        dl.AddCircleFilled(c + pomOff, 5.5f * s, pomCol);
        dl.AddCircle(c + pomOff, 5.5f * s, outline, 16, 1.6f * s);

        // Fluffy white body
        dl.AddEllipseFilled(c + new Vector2(0, 4f * s), new Vector2(27f * s, 25f * s), bodyCol);
        dl.AddEllipse(c + new Vector2(0, 4f * s), new Vector2(27f * s, 25f * s), outline, 0f, 32, 2.2f * s);

        // Cute red button nose
        dl.AddCircleFilled(c + new Vector2(0, 7f * s), 4.2f * s, Theme.Rgba(0.95f, 0.28f, 0.38f));

        DrawCuteFace(dl, c + new Vector2(0, 1f * s), s, mood, t, skipMouth: false);
    }

    private static void DrawNamazu(ImDrawListPtr dl, Vector2 c, float s, PetMood mood, float t)
    {
        var bodyCol = Theme.Rgba(0.98f, 0.89f, 0.62f);
        var bellyCol = Theme.Rgba(1.00f, 0.96f, 0.84f);
        var scarfCol = Theme.Rgba(0.88f, 0.20f, 0.26f);
        var outline = Theme.Rgba(0.14f, 0.06f, 0.16f);
        var wiggle = MathF.Sin(t * 6f) * 4f * s;

        // Round golden catfish body
        dl.AddEllipseFilled(c + new Vector2(0, 4f * s), new Vector2(30f * s, 25f * s), bodyCol);
        dl.AddEllipseFilled(c + new Vector2(0, 11f * s), new Vector2(21f * s, 14f * s), bellyCol);
        dl.AddEllipse(c + new Vector2(0, 4f * s), new Vector2(30f * s, 25f * s), outline, 0f, 32, 2.3f * s);

        // Festive red scarf
        dl.AddRectFilled(c + new Vector2(-20f * s, 20f * s), c + new Vector2(20f * s, 27f * s), scarfCol, 4f * s);

        // Long wiggling catfish whiskers
        dl.AddLine(c + new Vector2(-24f * s, 5f * s), c + new Vector2(-42f * s, 1f * s + wiggle), outline, 2.4f * s);
        dl.AddLine(c + new Vector2(-23f * s, 11f * s), c + new Vector2(-39f * s, 14f * s - wiggle), outline, 2.0f * s);
        dl.AddLine(c + new Vector2(24f * s, 5f * s), c + new Vector2(42f * s, 1f * s - wiggle), outline, 2.4f * s);
        dl.AddLine(c + new Vector2(23f * s, 11f * s), c + new Vector2(39f * s, 14f * s + wiggle), outline, 2.0f * s);

        DrawCuteFace(dl, c + new Vector2(0, 2f * s), s, mood, t);
    }

    private static void DrawCuteFace(ImDrawListPtr dl, Vector2 fc, float s, PetMood mood, float t, bool skipMouth = false)
    {
        var dark = Theme.Rgba(0.10f, 0.02f, 0.18f);
        var blush = Theme.Rgba(1.0f, 0.45f, 0.62f, 0.42f);

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
        else
        {
            // Cute shiny dot eyes
            dl.AddCircleFilled(leftEye, 3.8f * s, dark);
            dl.AddCircleFilled(leftEye + new Vector2(-1.2f * s, -1.2f * s), 1.3f * s, Theme.Rgba(1f, 1f, 1f));
            dl.AddCircleFilled(rightEye, 3.8f * s, dark);
            dl.AddCircleFilled(rightEye + new Vector2(-1.2f * s, -1.2f * s), 1.3f * s, Theme.Rgba(1f, 1f, 1f));
        }

        if (skipMouth) return;

        // Mouth
        if (mood is PetMood.Panic or PetMood.Crying)
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
