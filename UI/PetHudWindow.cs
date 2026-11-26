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

            // Fixed-height reserved area for the speech bubble so the pet NEVER shifts vertically!
            var bubbleSlotH = cfg.ShowSpeechBubbles ? 38f * scale : 0f;
            var bubbleOrigin = ImGui.GetCursorScreenPos();
            if (bubbleSlotH > 0f)
            {
                ImGui.Dummy(new Vector2(canvasSize, bubbleSlotH));
            }

            // Animated Pet / Egg canvas (always anchored in the exact same position!)
            var weaponStyle = cfg.ShowJobWeapon ? sim.CurrentJob.Weapon : JobWeaponStyle.None;
            var clicked = PetCanvasRenderer.DrawPetCanvas("##HudPetCanvas", pet, sim.CurrentMood, sim.AnimTimeSec, sim.WobblePulseSec, canvasSize, interactive: true, weaponStyle: weaponStyle, isWeaponDrawn: sim.IsWeaponDrawn);
            if (clicked)
            {
                sim.PetCompanion();
            }

            // Draw sleek rounded speech bubble with pointer tail on top of the reserved slot
            if (cfg.ShowSpeechBubbles && sim.SpeechTimerSec > 0f && !string.IsNullOrEmpty(sim.SpeechBubble))
            {
                var alpha = Math.Clamp(sim.SpeechTimerSec / 0.45f, 0f, 1f);
                var dl = ImGui.GetWindowDrawList();
                dl.PushClipRectFullScreen();
                var textSize = ImGui.CalcTextSize(sim.SpeechBubble);
                var padX = 11f * scale;
                var padY = 5f * scale;
                var boxW = MathF.Max(68f * scale, textSize.X + padX * 2f);
                var boxH = textSize.Y + padY * 2f;

                var centerX = bubbleOrigin.X + canvasSize * 0.5f;
                var tailH = 7f * scale;
                var boxMaxY = bubbleOrigin.Y + bubbleSlotH - tailH - 2f * scale;
                var boxMin = new Vector2(centerX - boxW * 0.5f, boxMaxY - boxH);
                var boxMax = new Vector2(centerX + boxW * 0.5f, boxMaxY);

                // Soft shadow
                dl.AddRectFilled(boxMin + new Vector2(0, 2f), boxMax + new Vector2(0, 2f),
                    Theme.Rgba(0.02f, 0.00f, 0.06f, 0.55f * alpha), 11f * scale);

                // Speech bubble body
                dl.AddRectFilled(boxMin, boxMax, Theme.Rgba(0.13f, 0.03f, 0.27f, 0.95f * alpha), 11f * scale);

                // Little triangular speech pointer tail pointing down to the pet
                var t1 = new Vector2(centerX - 6f * scale, boxMaxY - 1f);
                var t2 = new Vector2(centerX + 6f * scale, boxMaxY - 1f);
                var t3 = new Vector2(centerX, boxMaxY + tailH);
                dl.AddTriangleFilled(t1, t2, t3, Theme.Rgba(0.13f, 0.03f, 0.27f, 0.95f * alpha));

                // Crisp yellow outline around pill + tail sides
                var borderCol = Theme.Rgba(1.00f, 0.90f, 0.00f, 0.92f * alpha);
                dl.AddRect(boxMin, boxMax, borderCol, 11f * scale, ImDrawFlags.None, 1.5f);
                dl.AddLine(t1, t3, borderCol, 1.5f);
                dl.AddLine(t2, t3, borderCol, 1.5f);

                // Centered crisp text
                var textPos = new Vector2(centerX - textSize.X * 0.5f, boxMin.Y + (boxH - textSize.Y) * 0.5f);
                dl.AddText(textPos, Theme.Rgba(1.00f, 0.94f, 0.45f, alpha), sim.SpeechBubble);
                dl.PopClipRect();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                var jobTag = !string.IsNullOrEmpty(sim.CurrentJob.Abbrev) ? $" [{sim.CurrentJob.Abbrev}]" : string.Empty;
                ImGui.TextColored(Theme.YellowMain, $"{pet.Name}{jobTag} — {L.SpeciesName(pet.Species)}  |  {pet.Coins} {L.T("Coins", "Moedas")}");
                if (!pet.IsHatched)
                {
                    ImGui.TextUnformatted($"{L.T("Egg Hatching", "Chocando Ovo")}: {pet.EggProgress:F0}%");
                    ImGui.TextColored(Theme.TextMuted, L.T("Click to warm the egg!", "Clique para aquecer o ovo!"));
                }
                else
                {
                    ImGui.TextUnformatted($"Lv.{pet.Level} ({L.StageName(pet.Stage)}) • XP {pet.Xp}/{pet.XpForNextLevel}");
                    if (pet.IsHibernating)
                    {
                        ImGui.TextColored(Theme.YellowMain, L.T("Zzz HIBERNATING (No XP/Coins) — Click to wake up!", "Zzz HIBERNANDO (Sem XP/Moedas) — Clique para acordar!"));
                    }
                    var hungerCol = pet.Hunger <= 20f ? Theme.RedDanger : Theme.OrangeAccent;
                    ImGui.TextColored(hungerCol, $"{L.T("Hunger", "Fome")}: {pet.Hunger:F0}%  |  {L.T("Happy", "Feliz")}: {pet.Happiness:F0}%  |  {L.T("Energy", "Energia")}: {pet.Energy:F0}%");
                    if (sim.ActiveDuty is { } duty)
                    {
                        ImGui.TextColored(Theme.CyanAccent, $"{L.T("Active", "Ativo")}: {duty.DutyName} [{L.T(duty.Rule.LabelEn, duty.Rule.LabelPt)}] (+{duty.Rule.ClearCoins} Coins)");
                    }
                    ImGui.TextColored(Theme.TextMuted, L.T("Click to pet! Right-click for Dashboard & Food Shop.", "Clique para acariciar! Botão direito abre Painel & Loja."));
                }
                ImGui.EndTooltip();

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    onOpenDashboard();
            }

            // Compact name + Coins / egg progress bar
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

                // Mini hunger bar right under name so you always know when to feed!
                var hungerCol = pet.Hunger <= 20f ? Theme.RedDanger : (pet.Hunger <= 45f ? Theme.OrangeAccent : Theme.GreenAccent);
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, hungerCol);
                ImGui.ProgressBar(Math.Clamp(pet.Hunger / 100f, 0f, 1f), new Vector2(canvasSize, 7f), string.Empty);
                ImGui.PopStyleColor();
            }

            // Quick action buttons
            if (cfg.ShowHudQuickButtons)
            {
                var btnW = MathF.Max(18f, (canvasSize - 12f) / 4f);
                if (!pet.IsHatched)
                {
                    if (ImGui.Button(L.T("Warm Egg", "Aquecer"), new Vector2(canvasSize - 28f, 22f)))
                        sim.WarmEgg(22f);
                    ImGui.SameLine(0, 4f);
                    if (ImGui.Button("≡", new Vector2(24f, 22f)))
                        onOpenDashboard();
                }
                else
                {
                    var basic = FoodCatalog.Menu[0];
                    if (ImGui.Button("+", new Vector2(btnW, 22f))) sim.FeedQuickBasic();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(L.T($"Feed {basic.NameEn} ({basic.CostCoins} Coins, +{basic.HungerRestore:F0}% Hunger)",
                            $"Dar {basic.NamePt} ({basic.CostCoins} Moedas, +{basic.HungerRestore:F0}% Fome)"));

                    ImGui.SameLine(0, 4f);
                    if (ImGui.Button("♥", new Vector2(btnW, 22f))) sim.PetCompanion();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(L.T("Pet (Free)", "Carinho (Grátis)"));

                    ImGui.SameLine(0, 4f);
                    if (ImGui.Button(sim.ManualNap ? "★" : "Z", new Vector2(btnW, 22f))) sim.ToggleNap();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(sim.ManualNap ? L.T("Wake up", "Acordar") : L.T("Nap", "Soneca"));

                    ImGui.SameLine(0, 4f);
                    if (ImGui.Button("≡", new Vector2(btnW, 22f))) onOpenDashboard();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(L.T("Open Dashboard & Food Shop", "Abrir Painel & Loja de Comida"));
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
