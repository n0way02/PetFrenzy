using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace PetFrenzy.UI;

/// <summary>
/// FateFrenzy-inspired modern grouped card container with rounded surface, subtle border,
/// hairline row dividers, and right-edge alignment for pill toggles and sliders.
/// </summary>
public sealed class UiCard : IDisposable
{
    private const float PaddingX = 14f;
    private const float PaddingY = 10f;
    private const float CardGap = 12f;

    internal static float ContentLeftEdge { get; private set; }
    internal static float ContentRightEdge { get; private set; }
    internal static bool RowDrawnInCard;

    private readonly Vector2 cardOrigin;
    private readonly float cardWidth;

    public static UiCard Begin(string sectionTitle = "")
    {
        if (!string.IsNullOrEmpty(sectionTitle))
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 2f);
            ImGui.TextColored(Theme.YellowMain, sectionTitle.ToUpperInvariant());
            ImGui.Dummy(new Vector2(0, 4f));
        }

        return new UiCard();
    }

    private UiCard()
    {
        var scale = ImGuiHelpers.GlobalScale;
        cardOrigin = ImGui.GetCursorScreenPos();
        // Leave 4px breathing room on the right so the card border never hugs the scrollbar/edge
        cardWidth = MathF.Max(120f, ImGui.GetContentRegionAvail().X - 4f * scale);
        ContentLeftEdge = cardOrigin.X + PaddingX * scale;
        ContentRightEdge = cardOrigin.X + cardWidth - PaddingX * scale;
        RowDrawnInCard = false;

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        ImGui.SetCursorScreenPos(cardOrigin + new Vector2(PaddingX, PaddingY) * scale);
        ImGui.BeginGroup();
    }

    public void Dispose()
    {
        ImGui.EndGroup();
        var scale = ImGuiHelpers.GlobalScale;
        var cardEnd = new Vector2(cardOrigin.X + cardWidth, ImGui.GetItemRectMax().Y + PaddingY * scale);

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSetCurrent(0);
        var rounding = 10f * scale;
        dl.AddRectFilled(cardOrigin, cardEnd, Theme.ColU32(Theme.BgPanel), rounding);
        dl.AddRect(cardOrigin, cardEnd, Theme.Rgba(0.62f, 0.42f, 0.96f, 0.28f), rounding, ImDrawFlags.None, 1f);
        dl.ChannelsMerge();

        ImGui.SetCursorScreenPos(new Vector2(cardOrigin.X, cardEnd.Y));
        ImGui.Dummy(new Vector2(cardWidth, 0f));
        ImGui.Dummy(new Vector2(0, CardGap * scale));
    }

    public static float InnerWidth => MathF.Max(60f, ContentRightEdge - ContentLeftEdge);
}

/// <summary>
/// FateFrenzy-inspired electric-yellow pill toggle switch and grouped setting rows.
/// </summary>
public static class UiControls
{
    public static bool ToggleSwitch(string id, ref bool value)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var trackW = 40f * scale;
        var trackH = 20f * scale;
        var knobR = (trackH - 5f * scale) * 0.5f;
        var padX = 3f * scale;

        var origin = ImGui.GetCursorScreenPos();
        var end = origin + new Vector2(trackW, trackH);
        var dl = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(origin, end);

        var trackCol = value
            ? (hovered ? new Vector4(1f, 0.94f, 0.35f, 1f) : Theme.YellowMain)
            : (hovered ? new Vector4(0.24f, 0.08f, 0.45f, 1f) : Theme.BgSurface);
        var borderCol = value
            ? Theme.YellowMain
            : (hovered ? Theme.PurpleBright : Theme.BorderSoft);
        var knobCol = value ? Theme.BgDark : Theme.TextMuted;

        dl.AddRectFilled(origin, end, Theme.ColU32(trackCol), trackH * 0.5f);
        dl.AddRect(origin, end, Theme.ColU32(borderCol), trackH * 0.5f);

        var knobX = value ? end.X - knobR - padX : origin.X + knobR + padX;
        var knobY = origin.Y + trackH * 0.5f;
        dl.AddCircleFilled(new Vector2(knobX, knobY), knobR, Theme.ColU32(knobCol));

        ImGui.InvisibleButton($"##tgl_{id}", new Vector2(trackW, trackH));
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            value = !value;
            return true;
        }
        return false;
    }

    public static bool SettingToggleRow(string id, string label, ref bool value, string? subtitle = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var rightEdge = UiCard.ContentRightEdge;
        var rowW = MathF.Max(80f, rightEdge - origin.X);
        var toggleW = 40f * scale;
        var toggleH = 20f * scale;
        var labelMaxW = MathF.Max(60f, rowW - toggleW - 14f * scale);

        if (UiCard.RowDrawnInCard)
        {
            ImGui.GetWindowDrawList().AddLine(
                origin,
                new Vector2(rightEdge, origin.Y),
                Theme.Rgba(0.91f, 0.91f, 0.91f, 0.10f),
                1f);
            origin.Y += 8f * scale;
        }
        UiCard.RowDrawnInCard = true;

        var labelSize = ImGui.CalcTextSize(label, false, labelMaxW);
        var subSize = string.IsNullOrEmpty(subtitle) ? Vector2.Zero : ImGui.CalcTextSize(subtitle, false, labelMaxW);
        var textBlockH = labelSize.Y + (subSize.Y > 0 ? subSize.Y + 2f * scale : 0f);
        var rowH = MathF.Max(28f * scale, textBlockH + 6f * scale);

        var hoveredRow = ImGui.IsMouseHoveringRect(origin, origin + new Vector2(rowW, rowH));

        // Left: wrapped label + optional muted subtitle
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + (rowH - textBlockH) * 0.5f));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + labelMaxW);
        ImGui.TextColored(hoveredRow ? Theme.TextWhite : Theme.TextLight, label);
        if (!string.IsNullOrEmpty(subtitle))
        {
            ImGui.TextColored(Theme.TextMuted, subtitle);
        }
        ImGui.PopTextWrapPos();

        // Right: pill toggle switch centered vertically
        ImGui.SetCursorScreenPos(new Vector2(rightEdge - toggleW, origin.Y + (rowH - toggleH) * 0.5f));
        var changed = ToggleSwitch(id, ref value);

        // Advance cursor cleanly below the row
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + rowH + 6f * scale));
        ImGui.Dummy(new Vector2(rowW, 0f));
        return changed;
    }

    public static bool SettingSliderRow(string id, string label, ref float value, float min, float max, string fmt, string? subtitle = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var rightEdge = UiCard.ContentRightEdge;
        var rowW = MathF.Max(80f, rightEdge - origin.X);
        var sliderW = MathF.Min(190f * scale, rowW * 0.42f);
        var labelMaxW = MathF.Max(60f, rowW - sliderW - 14f * scale);

        if (UiCard.RowDrawnInCard)
        {
            ImGui.GetWindowDrawList().AddLine(
                origin,
                new Vector2(rightEdge, origin.Y),
                Theme.Rgba(0.91f, 0.91f, 0.91f, 0.10f),
                1f);
            origin.Y += 8f * scale;
        }
        UiCard.RowDrawnInCard = true;

        var labelSize = ImGui.CalcTextSize(label, false, labelMaxW);
        var subSize = string.IsNullOrEmpty(subtitle) ? Vector2.Zero : ImGui.CalcTextSize(subtitle, false, labelMaxW);
        var textBlockH = labelSize.Y + (subSize.Y > 0 ? subSize.Y + 2f * scale : 0f);
        var frameH = ImGui.GetFrameHeight();
        var rowH = MathF.Max(MathF.Max(30f * scale, frameH + 4f * scale), textBlockH + 6f * scale);

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + (rowH - textBlockH) * 0.5f));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + labelMaxW);
        ImGui.TextColored(Theme.TextLight, label);
        if (!string.IsNullOrEmpty(subtitle))
            ImGui.TextColored(Theme.TextMuted, subtitle);
        ImGui.PopTextWrapPos();

        ImGui.SetCursorScreenPos(new Vector2(rightEdge - sliderW, origin.Y + (rowH - frameH) * 0.5f));
        ImGui.SetNextItemWidth(sliderW);
        var changed = ImGui.SliderFloat($"##sld_{id}", ref value, min, max, fmt);

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + rowH + 6f * scale));
        ImGui.Dummy(new Vector2(rowW, 0f));
        return changed;
    }

    public static void InsetNote(string text, Vector4? textCol = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var boxW = MathF.Max(80f, UiCard.ContentRightEdge - origin.X);
        var padX = 10f * scale;
        var padY = 7f * scale;
        var wrapW = MathF.Max(40f, boxW - padX * 2f);

        var textSize = ImGui.CalcTextSize(text, false, wrapW);
        var boxEnd = new Vector2(origin.X + boxW, origin.Y + textSize.Y + padY * 2f);

        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(origin, boxEnd, Theme.ColU32(Theme.BgDark), 6f * scale);
        dl.AddRect(origin, boxEnd, Theme.Rgba(0.91f, 0.91f, 0.91f, 0.12f), 6f * scale);

        ImGui.SetCursorScreenPos(origin + new Vector2(padX, padY));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrapW);
        ImGui.TextColored(textCol ?? Theme.TextMuted, text);
        ImGui.PopTextWrapPos();

        ImGui.SetCursorScreenPos(new Vector2(origin.X, boxEnd.Y + 4f * scale));
        ImGui.Dummy(new Vector2(boxW, 0f));
    }

    public static void TextWrappedColored(Vector4 col, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, col);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }
}
