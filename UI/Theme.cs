using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PetFrenzy.UI;

public static class Theme
{
    public static readonly Vector4 BgDark = new(0.067f, 0.012f, 0.141f, 0.96f);        // #110324
    public static readonly Vector4 BgPanel = new(0.106f, 0.020f, 0.220f, 0.98f);       // #1B0538
    public static readonly Vector4 BgSurface = new(0.137f, 0.031f, 0.278f, 1.00f);     // #230847
    public static readonly Vector4 PurpleMain = new(0.392f, 0.063f, 0.788f, 1.00f);    // #6410C9
    public static readonly Vector4 PurpleBright = new(0.486f, 0.165f, 0.910f, 1.00f);  // #7C2AE8
    public static readonly Vector4 PurpleSoft = new(0.392f, 0.063f, 0.788f, 0.35f);
    public static readonly Vector4 YellowMain = new(1.000f, 0.902f, 0.000f, 1.00f);    // #FFE600
    public static readonly Vector4 TextLight = new(0.914f, 0.914f, 0.914f, 1.00f);     // #E9E9E9
    public static readonly Vector4 TextWhite = new(1.000f, 1.000f, 1.000f, 1.00f);
    public static readonly Vector4 TextMuted = new(0.760f, 0.710f, 0.870f, 1.00f);
    public static readonly Vector4 BorderSoft = new(0.914f, 0.914f, 0.914f, 0.16f);
    public static readonly Vector4 GreenAccent = new(0.290f, 0.871f, 0.502f, 1.00f);
    public static readonly Vector4 PinkAccent = new(0.980f, 0.440f, 0.620f, 1.00f);
    public static readonly Vector4 CyanAccent = new(0.220f, 0.780f, 0.970f, 1.00f);
    public static readonly Vector4 OrangeAccent = new(0.990f, 0.620f, 0.240f, 1.00f);
    public static readonly Vector4 RedDanger = new(0.937f, 0.267f, 0.267f, 1.00f);

    public static uint ColU32(Vector4 v) => ImGui.ColorConvertFloat4ToU32(v);
    public static uint Rgba(float r, float g, float b, float a = 1f) => ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, a));

    public static int PushTheme()
    {
        var colors = 0;
        void PushC(ImGuiCol col, Vector4 val) { ImGui.PushStyleColor(col, val); colors++; }

        PushC(ImGuiCol.WindowBg, BgDark);
        PushC(ImGuiCol.ChildBg, BgPanel);
        PushC(ImGuiCol.PopupBg, BgPanel);
        PushC(ImGuiCol.Border, BorderSoft);
        PushC(ImGuiCol.FrameBg, BgSurface);
        PushC(ImGuiCol.FrameBgHovered, new Vector4(0.22f, 0.06f, 0.42f, 1f));
        PushC(ImGuiCol.FrameBgActive, PurpleMain);
        PushC(ImGuiCol.TitleBg, BgPanel);
        PushC(ImGuiCol.TitleBgActive, PurpleMain);
        PushC(ImGuiCol.Button, PurpleMain);
        PushC(ImGuiCol.ButtonHovered, PurpleBright);
        PushC(ImGuiCol.ButtonActive, new Vector4(0.31f, 0.04f, 0.65f, 1f));
        PushC(ImGuiCol.Header, PurpleSoft);
        PushC(ImGuiCol.HeaderHovered, PurpleMain);
        PushC(ImGuiCol.HeaderActive, PurpleBright);
        PushC(ImGuiCol.Tab, BgSurface);
        PushC(ImGuiCol.TabHovered, PurpleBright);
        PushC(ImGuiCol.TabActive, PurpleMain);
        PushC(ImGuiCol.CheckMark, YellowMain);
        PushC(ImGuiCol.SliderGrab, YellowMain);
        PushC(ImGuiCol.SliderGrabActive, TextWhite);
        PushC(ImGuiCol.PlotHistogram, YellowMain);
        PushC(ImGuiCol.Text, TextLight);
        PushC(ImGuiCol.TextDisabled, TextMuted);

        return colors;
    }

    public static int PushStyleVars()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12f, 10f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, 5f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 6f));
        return 7;
    }
}
