using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PetFrenzy.Models;
using PetFrenzy.Services;

namespace PetFrenzy.UI;

public sealed class PetHudWindow : Window
{
    private readonly Action onOpenDashboard;

    public PetHudWindow(Action onOpenDashboard)
        : base("PetFrenzyHUD###PetFrenzyFloatingHud", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.onOpenDashboard = onOpenDashboard;
        RespectCloseHotkey = false;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize;
        if (Plugin.Config.LockHudPosition)
            Flags |= ImGuiWindowFlags.NoMove;
        if (Plugin.Config.TransparentHud)
            Flags |= ImGuiWindowFlags.NoBackground;

        base.PreDraw();
    }

    public override void Draw()
    {
        var pushedColors = Theme.PushTheme();
        var pushedVars = Theme.PushStyleVars();

        try
        {
            var cfg = Plugin.Config;
            var pet = cfg.Pet;
            var sim = Plugin.Sim;
            var scale = Math.Clamp(cfg.HudScale, 0.75f, 1.6f);
            var canvasSize = 112f * scale;

            // Speech bubble above pet
            if (cfg.ShowSpeechBubbles && sim.SpeechTimerSec > 0f && !string.IsNullOrEmpty(sim.SpeechBubble))
            {
                ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.14f, 0.03f, 0.28f, 0.94f));
                ImGui.PushStyleColor(ImGuiCol.Border, Theme.YellowMain);
                var textW = MathF.Min(210f * scale, MathF.Max(110f * scale, ImGui.CalcTextSize(sim.SpeechBubble).X + 18f));
                ImGui.BeginChild("##HudSpeech", new Vector2(textW, 28f), true, ImGuiWindowFlags.NoScrollbar);
                ImGui.TextColored(Theme.YellowMain, sim.SpeechBubble);
                ImGui.EndChild();
                ImGui.PopStyleColor(2);
            }

            // Animated Pet / Egg canvas (click to pet or warm egg!)
            var clicked = PetCanvasRenderer.DrawPetCanvas("##HudPetCanvas", pet, sim.CurrentMood, sim.AnimTimeSec, sim.WobblePulseSec, canvasSize, interactive: true);
            if (clicked)
            {
                sim.PetCompanion();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextColored(Theme.YellowMain, $"{pet.Name} — {L.SpeciesName(pet.Species)}");
                if (!pet.IsHatched)
                {
                    ImGui.TextUnformatted($"{L.T("Egg Hatching", "Chocando Ovo")}: {pet.EggProgress:F0}%");
                    ImGui.TextColored(Theme.TextMuted, L.T("Click to warm the egg!", "Clique para aquecer o ovo!"));
                }
                else
                {
                    ImGui.TextUnformatted($"Lv.{pet.Level} ({L.StageName(pet.Stage)}) • XP {pet.Xp}/{pet.XpForNextLevel}");
                    ImGui.TextColored(Theme.GreenAccent, $"{L.T("Hunger", "Fome")}: {pet.Hunger:F0}%  |  {L.T("Happy", "Feliz")}: {pet.Happiness:F0}%  |  {L.T("Energy", "Energia")}: {pet.Energy:F0}%");
                    ImGui.TextColored(Theme.TextMuted, L.T("Click to pet! Right-click for Dashboard.", "Clique para acariciar! Botão direito abre o Painel."));
                }
                ImGui.EndTooltip();

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    onOpenDashboard();
            }

            // Compact name + level / egg progress bar
            if (!pet.IsHatched)
            {
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Theme.YellowMain);
                ImGui.ProgressBar(pet.EggProgress / 100f, new Vector2(canvasSize, 13f), $"{pet.EggProgress:F0}%");
                ImGui.PopStyleColor();
            }
            else
            {
                var label = $"{pet.Name} Lv.{pet.Level}";
                var ts = ImGui.CalcTextSize(label);
                var padX = MathF.Max(0f, (canvasSize - ts.X) * 0.5f);
                if (padX > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);
                ImGui.TextColored(Theme.TextWhite, label);
            }

            // Quick action buttons
            if (cfg.ShowHudQuickButtons)
            {
                var btnW = MathF.Max(26f, (canvasSize - 12f) / 4f);
                if (!pet.IsHatched)
                {
                    if (ImGui.Button(L.T("Warm Egg", "Aquecer"), new Vector2(canvasSize - 30f, 22f)))
                        sim.WarmEgg(22f);
                    ImGui.SameLine(0, 4f);
                    if (ImGui.Button("⚙", new Vector2(26f, 22f)))
                        onOpenDashboard();
                }
                else
                {
                    if (ImGui.Button("🍖", new Vector2(btnW, 22f))) sim.Feed();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(L.T("Feed snack", "Alimentar"));

                    ImGui.SameLine(0, 4f);
                    if (ImGui.Button("♥", new Vector2(btnW, 22f))) sim.PetCompanion();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(L.T("Pet", "Carinho"));

                    ImGui.SameLine(0, 4f);
                    if (ImGui.Button(sim.ManualNap ? "☀" : "Z", new Vector2(btnW, 22f))) sim.ToggleNap();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(sim.ManualNap ? L.T("Wake up", "Acordar") : L.T("Nap", "Soneca"));

                    ImGui.SameLine(0, 4f);
                    if (ImGui.Button("⚙", new Vector2(btnW, 22f))) onOpenDashboard();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(L.T("Open Dashboard", "Abrir Painel"));
                }
            }
        }
        finally
        {
            ImGui.PopStyleVar(pushedVars);
            ImGui.PopStyleColor(pushedColors);
        }
    }
}
