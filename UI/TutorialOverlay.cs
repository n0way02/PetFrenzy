using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using PetFrenzy.Models;
using PetFrenzy.Services;

namespace PetFrenzy.UI;

/// <summary>
/// Interactive Spotlight / Coach-Mark Guided Tutorial led by Milly (the creator's fluffy kitty).
/// Dims and blocks clicks outside the highlighted UI element while keeping the spotlighted
/// control 100% interactive, with EN / PT-BR selection, Back/Next navigation, Skip, and Replay.
/// </summary>
public sealed class TutorialOverlay
{
    private sealed record StepDef(
        string TargetKey,
        string RequiredTabKey,
        string TitleEn,
        string TitlePt,
        string BodyEn,
        string BodyPt,
        PetMood MillyMood,
        bool IsLanguageChoice = false,
        bool IsFinal = false);

    private static readonly StepDef[] Steps =
    [
        new(
            TargetKey: "",
            RequiredTabKey: "tab_mypet",
            TitleEn: "Meow~ Welcome to PetFrenzy!",
            TitlePt: "Miau~ Bem-vindo ao PetFrenzy!",
            BodyEn: "Hi! I'm Milly, your fluffy guide! Choose your preferred language below and I'll walk you through every feature of your new Eorzea Tamagotchi.",
            BodyPt: "Oi! Eu sou a Milly, sua guia peludinha! Escolha seu idioma abaixo que eu vou te mostrar cada função do seu novo Tamagotchi de Eorzea.",
            MillyMood: PetMood.Happy,
            IsLanguageChoice: true),

        new(
            TargetKey: "mypet_main",
            RequiredTabKey: "tab_mypet",
            TitleEn: "1. Your Companion & Daily Care",
            TitlePt: "1. Seu Mascote e Cuidados Diários",
            BodyEn: "Click your pet for free headpats, rename it, feed it (Hunger drains in 16h — after 12h at 0% it hibernates with a Free Wake-Up Snack!), and use Energy Potions as activities drain Energy!",
            BodyPt: "Clique no pet para carinho grátis, renomeie, alimente (Fome zera em 16h — após 12h em 0% ele hiberna e ganha 1 Comida Grátis!) e use Poções de Energia conforme as atividades gastam Energia!",
            MillyMood: PetMood.Petted),

        new(
            TargetKey: "tab_wardrobe",
            RequiredTabKey: "tab_wardrobe",
            TitleEn: "2. Wardrobe & Custom Colors",
            TitlePt: "2. Guarda-Roupa e Cores Personalizadas",
            BodyEn: "In the Wardrobe tab you equip any Hats, Outfits, Wings, and Weapon Auras you've unlocked — and once you own the Dye Kit, you can customize your pet's body colors in full RGB!",
            BodyPt: "Na aba Guarda-Roupa você equipa os Chapéus, Roupas, Asas e Auras de Armas que desbloqueou — e com o Kit de Cores pode pintar as partes do seu pet em RGB livre!",
            MillyMood: PetMood.Happy),

        new(
            TargetKey: "tab_shop",
            RequiredTabKey: "tab_shop",
            TitleEn: "3. Pet Shop & Live Try-On",
            TitlePt: "3. Loja e Provador ao Vivo",
            BodyEn: "Spend the Pet Coins you earn while playing FFXIV on Food, Hats, Outfits, Wings (up to Lv.100!), Weapon Auras, and the Color Dye Kit. Click 'Try' on any item to preview it live first!",
            BodyPt: "Gaste as Moedas que você ganha jogando FFXIV em Comida, Chapéus, Roupas, Asas (até o Lv.100!), Auras de Armas e Kit de Cores. Clique em 'Ver' para experimentar antes de comprar!",
            MillyMood: PetMood.Happy),

        new(
            TargetKey: "tab_rewards",
            RequiredTabKey: "tab_rewards",
            TitleEn: "4. Earn Rewards From ALL FFXIV Content!",
            TitlePt: "4. Ganhe Recompensas em TODO Conteúdo!",
            BodyEn: "Literally EVERYTHING you do in FFXIV rewards your pet: Roulettes, Dungeons, Trials, Raids, PvP, FATEs, Bozja, Eureka, Occult Crescent, Cosmic Exploration, Crafting, Gathering & online time!",
            BodyPt: "Literalmente TUDO que você faz no FFXIV recompensa o seu pet: Roletas, Dungeons, Trials, Raids, PvP, FATEs, Bozja, Eureka, Occult Crescent, Exploração Cósmica, Craft, Coleta e tempo online!",
            MillyMood: PetMood.Celebrating),

        new(
            TargetKey: "tab_nursery",
            RequiredTabKey: "tab_nursery",
            TitleEn: "5. Egg Nursery (Adopt Anytime)",
            TitlePt: "5. Chocadeira (Adote Quando Quiser)",
            BodyEn: "Want to switch companions? Pick Carbuncle, Fat Cat, Moogle, or Namazu here! You can keep your current Level and all unlocked cosmetics whenever you adopt a new egg.",
            BodyPt: "Quer trocar de mascote? Escolha Carbuncle, Fat Cat, Moogle ou Namazu aqui! Você pode manter seu Nível atual e todos os cosméticos desbloqueados ao adotar um novo ovo.",
            MillyMood: PetMood.Happy),

        new(
            TargetKey: "settings_hud_card",
            RequiredTabKey: "tab_settings",
            TitleEn: "6. Move, Remove Background, Lock & Resize!",
            TitlePt: "6. Mover, Tirar Fundo, Travar e Redimensionar!",
            BodyEn: "Try these switches right now inside the spotlight!\n• Drag your floating pet anywhere on your screen.\n• Turn ON 'Transparent / borderless' to remove the window box.\n• Turn ON 'Lock HUD position' once it's in the perfect spot.\n• Use 'HUD Scale' to make your pet bigger or smaller!",
            BodyPt: "Teste essas opções agora mesmo dentro do destaque!\n• Arraste o pet flutuante para qualquer lugar da tela.\n• Ative 'Fundo transparente / sem borda' para tirar a caixinha.\n• Ative 'Travar posição do HUD' quando deixar no lugar perfeito.\n• Use 'Tamanho do HUD' para aumentar ou diminuir o pet!",
            MillyMood: PetMood.Working),

        new(
            TargetKey: "settings_inter_card",
            RequiredTabKey: "tab_settings",
            TitleEn: "7. Job Weapons, Bubbles & Quick Care",
            TitlePt: "7. Armas da Job, Balões e Botões Rápidos",
            BodyEn: "Here you can toggle quick care buttons under the floating pet, speech bubbles, and automatic Job weapons & crafting/gathering tools (your pet draws its weapon with you in combat!).",
            BodyPt: "Aqui você liga/desliga os botões rápidos abaixo do pet, os balões de fala e o equipamento automático de arma da sua Job / ferramentas de craft e coleta (ele saca a arma junto com você em combate!).",
            MillyMood: PetMood.Cheering),

        new(
            TargetKey: "",
            RequiredTabKey: "tab_mypet",
            TitleEn: "You're All Set! Have Fun! ♥",
            TitlePt: "Tudo Pronto! Boa Aventura! ♥",
            BodyEn: "Use /pf or /petfrenzy to open or close this dashboard, and /pf hud to toggle your floating companion.\n\nYou can replay this tutorial anytime from the 'About' tab. Purrrr~ ♥",
            BodyPt: "Use /pf ou /petfrenzy para abrir ou fechar este painel, e /pf hud para mostrar ou esconder o pet flutuante.\n\nVocê pode rever este tutorial quando quiser na aba 'Sobre'. Ronron~ ♥",
            MillyMood: PetMood.Celebrating,
            IsFinal: true),
    ];

    private readonly Dictionary<string, (Vector2 Min, Vector2 Max)> targets = new(StringComparer.Ordinal);
    private string? pendingTabSwitch;
    private int bringToFrontFrames;

    public bool IsActive { get; private set; }
    public int StepIndex { get; private set; }

    public void Start()
    {
        IsActive = true;
        StepIndex = 0;
        pendingTabSwitch = Steps[0].RequiredTabKey;
        bringToFrontFrames = 2;
    }

    public void Stop(bool markCompleted)
    {
        IsActive = false;
        pendingTabSwitch = null;
        if (markCompleted && !Plugin.Config.TutorialCompleted)
        {
            Plugin.Config.TutorialCompleted = true;
            Plugin.Config.Save();
        }
    }

    public void BeginFrame()
    {
        targets.Clear();
    }

    public void RegisterTarget(string key)
    {
        if (!IsActive) return;
        targets[key] = (ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
    }

    public void RegisterTargetRect(string key, Vector2 min, Vector2 max)
    {
        if (!IsActive) return;
        targets[key] = (min, max);
    }

    /// <summary>
    /// Returns ImGuiTabItemFlags.SetSelected once when the tutorial needs to switch tabs.
    /// </summary>
    public ImGuiTabItemFlags ConsumeTabFlag(string tabKey)
    {
        if (IsActive && string.Equals(pendingTabSwitch, tabKey, StringComparison.Ordinal))
        {
            pendingTabSwitch = null;
            return ImGuiTabItemFlags.SetSelected;
        }
        return ImGuiTabItemFlags.None;
    }

    public void Draw(bool dashboardIsOpen)
    {
        if (!IsActive) return;
        if (!dashboardIsOpen)
        {
            // Keep tutorial paused if user closed the dashboard window
            return;
        }

        var step = Steps[Math.Clamp(StepIndex, 0, Steps.Length - 1)];
        var vp = ImGui.GetMainViewport();
        var vpPos = vp.Pos;
        var vpSize = vp.Size;

        var hasHole = false;
        Vector2 holeMin = default;
        Vector2 holeMax = default;

        if (!string.IsNullOrEmpty(step.TargetKey) && targets.TryGetValue(step.TargetKey, out var rawRect))
        {
            const float pad = 6f;
            holeMin = new Vector2(MathF.Max(vpPos.X, rawRect.Min.X - pad), MathF.Max(vpPos.Y, rawRect.Min.Y - pad));
            holeMax = new Vector2(MathF.Min(vpPos.X + vpSize.X, rawRect.Max.X + pad), MathF.Min(vpPos.Y + vpSize.Y, rawRect.Max.Y + pad));
            hasHole = (holeMax.X - holeMin.X) >= 8f && (holeMax.Y - holeMin.Y) >= 8f;
        }

        // 1. Draw 4 click-blocking dimmed scrim regions around the spotlight cutout (or 1 full scrim if no hole)
        var forceFront = bringToFrontFrames > 0;
        var scrimCol = Theme.Rgba(0.03f, 0.01f, 0.09f, 0.72f);
        if (hasHole)
        {
            DrawScrimBlocker("##TutScrimTop", vpPos, new Vector2(vpSize.X, MathF.Max(0f, holeMin.Y - vpPos.Y)), scrimCol, forceFront);
            DrawScrimBlocker("##TutScrimBot", new Vector2(vpPos.X, holeMax.Y), new Vector2(vpSize.X, MathF.Max(0f, (vpPos.Y + vpSize.Y) - holeMax.Y)), scrimCol, forceFront);
            DrawScrimBlocker("##TutScrimLeft", new Vector2(vpPos.X, holeMin.Y), new Vector2(MathF.Max(0f, holeMin.X - vpPos.X), holeMax.Y - holeMin.Y), scrimCol, forceFront);
            DrawScrimBlocker("##TutScrimRight", new Vector2(holeMax.X, holeMin.Y), new Vector2(MathF.Max(0f, (vpPos.X + vpSize.X) - holeMax.X), holeMax.Y - holeMin.Y), scrimCol, forceFront);

            // Glowing pulsing spotlight border on foreground drawlist
            var fg = ImGui.GetForegroundDrawList();
            var pulse = 0.5f + 0.5f * MathF.Sin(Plugin.Sim.AnimTimeSec * 5.0f);
            fg.AddRect(holeMin - new Vector2(3f, 3f), holeMax + new Vector2(3f, 3f),
                Theme.Rgba(1.0f, 0.90f, 0.0f, 0.28f + pulse * 0.25f), 10f, ImDrawFlags.None, 5f);
            fg.AddRect(holeMin, holeMax, Theme.ColU32(Theme.YellowMain), 8f, ImDrawFlags.None, 2.5f);
        }
        else
        {
            DrawScrimBlocker("##TutScrimFull", vpPos, vpSize, scrimCol, forceFront);
        }

        // 2. Position & draw Milly's floating Guide Card cleanly without overlapping the spotlight hole!
        var cardW = 470f;
        var cardEstH = step.IsLanguageChoice ? 255f : 235f;
        var cardPos = ComputeGuideCardPos(vpPos, vpSize, hasHole, holeMin, holeMax, cardW, cardEstH);

        var pushedColors = Theme.PushTheme();
        var pushedVars = Theme.PushStyleVars();
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.095f, 0.022f, 0.205f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.YellowMain);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 2.0f);

        ImGui.SetNextWindowPos(cardPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(cardW, 0f), ImGuiCond.Always);
        if (forceFront)
        {
            ImGui.SetNextWindowFocus();
            bringToFrontFrames--;
        }

        var cardFlags = ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.AlwaysAutoResize;

        if (ImGui.Begin("##PetFrenzyTutorialGuideCard", cardFlags))
        {
            // Left: Animated Milly mascot!
            ImGui.BeginGroup();
            var milly = new PetStateData
            {
                Species = PetSpecies.FluffyKitty,
                Name = "Milly",
                IsHatched = true,
                Level = 100,
            };
            PetCanvasRenderer.DrawPetCanvas("##TutMilly", milly, step.MillyMood, Plugin.Sim.AnimTimeSec, 0f, 114f, interactive: false);
            ImGui.TextColored(Theme.YellowMain, "   Milly ♥");
            ImGui.EndGroup();

            ImGui.SameLine(0, 14f);

            // Right: Step text & language selector
            ImGui.BeginGroup();
            var textW = cardW - 158f;
            ImGui.TextColored(Theme.CyanAccent, L.T($"GUIDE  •  Step {StepIndex + 1} / {Steps.Length}", $"GUIA  •  Passo {StepIndex + 1} / {Steps.Length}"));

            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textW);
            ImGui.TextColored(Theme.YellowMain, L.T(step.TitleEn, step.TitlePt));
            ImGui.Spacing();
            ImGui.TextColored(Theme.TextWhite, L.T(step.BodyEn, step.BodyPt));
            ImGui.PopTextWrapPos();

            if (step.IsLanguageChoice)
            {
                ImGui.Spacing();
                var langBtnW = (textW - 8f) * 0.5f;
                var isEn = !L.IsPt;
                if (isEn) { ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain); ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark); }
                if (ImGui.Button("English", new Vector2(langBtnW, 28f)))
                {
                    Plugin.Config.Language = "en";
                    Plugin.Config.Save();
                }
                if (isEn) ImGui.PopStyleColor(2);

                ImGui.SameLine(0, 8f);

                var isPt = L.IsPt;
                if (isPt) { ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain); ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark); }
                if (ImGui.Button("Português (BR)", new Vector2(langBtnW, 28f)))
                {
                    Plugin.Config.Language = "pt";
                    Plugin.Config.Save();
                }
                if (isPt) ImGui.PopStyleColor(2);
            }
            ImGui.EndGroup();

            // Full-width footer bar: Skip on left, Back + Next/Start/Finish cleanly right-aligned!
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var footerW = ImGui.GetContentRegionAvail().X;
            const float backW = 86f;
            const float nextW = 108f;
            const float gap = 8f;

            if (ImGui.Button(L.T("Skip Tutorial", "Pular Tutorial"), new Vector2(120f, 28f)))
            {
                Stop(markCompleted: true);
            }

            var rightStart = ImGui.GetCursorPosX() + footerW - (backW + nextW + gap);
            ImGui.SameLine(MathF.Max(136f, rightStart));

            ImGui.BeginDisabled(StepIndex <= 0);
            if (ImGui.Button(L.T("< Back", "< Voltar"), new Vector2(backW, 28f)))
            {
                GoToStep(StepIndex - 1);
            }
            ImGui.EndDisabled();

            ImGui.SameLine(0, gap);

            ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain);
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark);
            var nextLabel = step.IsLanguageChoice
                ? L.T("Start >", "Começar >")
                : step.IsFinal
                    ? L.T("Finish ♥", "Concluir ♥")
                    : L.T("Next >", "Próximo >");
            if (ImGui.Button(nextLabel, new Vector2(nextW, 28f)))
            {
                if (step.IsFinal) Stop(markCompleted: true);
                else GoToStep(StepIndex + 1);
            }
            ImGui.PopStyleColor(2);
        }
        ImGui.End();

        ImGui.PopStyleVar(1);
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(pushedVars);
        ImGui.PopStyleColor(pushedColors);
    }

    private void GoToStep(int newIndex)
    {
        StepIndex = Math.Clamp(newIndex, 0, Steps.Length - 1);
        pendingTabSwitch = Steps[StepIndex].RequiredTabKey;
        bringToFrontFrames = 2;
    }

    private static void DrawScrimBlocker(string id, Vector2 pos, Vector2 size, uint col, bool forceFront)
    {
        if (size.X <= 1f || size.Y <= 1f) return;

        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        if (forceFront) ImGui.SetNextWindowFocus();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, ImGui.ColorConvertU32ToFloat4(col));

        var flags = ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoBringToFrontOnFocus;
        if (!forceFront) flags |= ImGuiWindowFlags.NoFocusOnAppearing;

        ImGui.Begin(id, flags);
        ImGui.End();

        ImGui.PopStyleColor(1);
        ImGui.PopStyleVar(3);
    }

    private static Vector2 ComputeGuideCardPos(
        Vector2 vpPos,
        Vector2 vpSize,
        bool hasHole,
        Vector2 holeMin,
        Vector2 holeMax,
        float cardW,
        float cardH)
    {
        if (!hasHole)
        {
            return new Vector2(
                vpPos.X + (vpSize.X - cardW) * 0.5f,
                vpPos.Y + (vpSize.Y - cardH) * 0.5f);
        }

        const float gap = 18f;
        const float margin = 16f;
        var vpRight = vpPos.X + vpSize.X;
        var vpBottom = vpPos.Y + vpSize.Y;

        Vector2 pos;
        // Prefer below spotlight, then right, then left, then above
        if (holeMax.Y + gap + cardH <= vpBottom - margin)
        {
            pos = new Vector2(holeMin.X, holeMax.Y + gap);
        }
        else if (holeMax.X + gap + cardW <= vpRight - margin)
        {
            pos = new Vector2(holeMax.X + gap, holeMin.Y);
        }
        else if (holeMin.X - gap - cardW >= vpPos.X + margin)
        {
            pos = new Vector2(holeMin.X - gap - cardW, holeMin.Y);
        }
        else
        {
            pos = new Vector2(holeMin.X, holeMin.Y - gap - cardH);
        }

        pos.X = Math.Clamp(pos.X, vpPos.X + margin, MathF.Max(vpPos.X + margin, vpRight - cardW - margin));
        pos.Y = Math.Clamp(pos.Y, vpPos.Y + margin, MathF.Max(vpPos.Y + margin, vpBottom - cardH - margin));
        return pos;
    }
}
