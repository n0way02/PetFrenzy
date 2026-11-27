using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PetFrenzy.Models;
using PetFrenzy.Services;

namespace PetFrenzy.UI;

/// <summary>
/// "What's New" Changelog popup displayed once automatically whenever PetFrenzy updates to a new version
/// (similar to RotationSolver Reborn), and re-openable anytime from the About tab.
/// </summary>
public sealed class ChangelogWindow : Window
{
    private static readonly PetStateData DeviruchiPreview = new()
    {
        Species = PetSpecies.Deviruchi,
        Name = "Devi",
        IsHatched = true,
        Level = 50,
    };

    private int resetScrollFrames;

    public ChangelogWindow()
        : base($"PetFrenzy — What's New (v{Plugin.CurrentVersion})###PetFrenzyChangelog",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(560f, 560f);
        SizeCondition = ImGuiCond.Always;
        IsOpen = false;
    }

    public override void OnOpen()
    {
        resetScrollFrames = 2;
    }

    public override void PreDraw()
    {
        var vp = ImGui.GetMainViewport();
        var center = vp.WorkPos + vp.WorkSize * 0.5f;
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
    }

    public override void OnClose()
    {
        MarkSeen();
    }

    public static void MarkSeen()
    {
        var cfg = Plugin.Config;
        if (!string.Equals(cfg.LastSeenChangelogVersion, Plugin.CurrentVersion, StringComparison.Ordinal))
        {
            cfg.LastSeenChangelogVersion = Plugin.CurrentVersion;
            cfg.Save();
        }
    }

    public override void Draw()
    {
        var cfg = Plugin.Config;
        var sim = Plugin.Sim;
        var pushedColors = Theme.PushTheme();
        var pushedVars = Theme.PushStyleVars();

        try
        {
            var availW = ImGui.GetContentRegionAvail().X;

            // ── Header with live animated Deviruchi preview + Version badge + EN/PT toggle ──
            PetCanvasRenderer.DrawPetCanvas("##ChgDevi", DeviruchiPreview, PetMood.Happy, sim.AnimTimeSec, 0f, 64f, interactive: false);
            ImGui.SameLine(0, 12f);

            ImGui.BeginGroup();
            ImGui.TextColored(Theme.YellowMain, L.T(
                $"★ PetFrenzy Updated to v{Plugin.CurrentVersion}!",
                $"★ PetFrenzy Atualizado para v{Plugin.CurrentVersion}!"));
            ImGui.TextColored(Theme.TextMuted, L.T(
                "Here is everything new in this update:",
                "Confira todas as novidades desta atualização:"));
            ImGui.Dummy(new Vector2(0, 2f));
            if (ImGui.SmallButton(cfg.Language == "pt" ? "Switch to English (EN)" : "Mudar para Português (PT-BR)"))
            {
                cfg.Language = cfg.Language == "pt" ? "en" : "pt";
                cfg.Save();
            }
            ImGui.EndGroup();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // ── Scrollable Changelog Cards (always starts scrolled to the very top on open!) ──
            var scrollH = MathF.Max(140f, ImGui.GetContentRegionAvail().Y - 76f);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            ImGui.BeginChild("##ChangelogScroll", new Vector2(availW, scrollH), false);
            ImGui.PopStyleColor();
            if (resetScrollFrames > 0)
            {
                ImGui.SetScrollY(0f);
                resetScrollFrames--;
            }

            using (UiCard.Begin(L.T("★ New Companion: Deviruchi!", "★ Novo Mascote: Deviruchi!")))
            {
                UiControls.TextWrappedColored(Theme.TextLight, L.T(
                    "• Adopt the adorable new Deviruchi imp in the Egg Nursery — complete with flapping bat wings, spade devil tail, glowing yellow eyes, cheeky grin, and its classic little trident!\n" +
                    "• Fully compatible with all Hats, Outfits, Weapon Auras, Custom RGB Colors, and features the exclusive 'Dark Trident Pierce' Mini-Boss Special!",
                    "• Adote o novo e fofinho Deviruchi na Chocadeira — com asinhas de morcego batendo, cauda de diabinho, olhos amarelos brilhantes, sorrisinho maroto e o tridente clássico!\n" +
                    "• Totalmente compatível com todos os Chapéus, Roupas, Auras de Armas, Kit de Cores RGB e tem a skill especial exclusiva 'Tridente Sombrio' nos Mini-Bosses!"));
            }

            using (UiCard.Begin(L.T("◆ Co-op Mini-Boss Arena (Up to 8 Players!)", "◆ Arena de Mini-Boss Co-op (Até 8 Jogadores!)")))
            {
                UiControls.TextWrappedColored(Theme.TextLight, L.T(
                    "• Battle 4 Level-Scaled Mini-Bosses in the 'Social & Boss' tab and on the live floating Battle Arena HUD: Lv.20 Jumbo Cactuar, Lv.45 King Moggle Mog, Lv.70 King Behemoth & Lv.95 Ultima Chibi!\n" +
                    "• Up to 8 pets surround the boss and auto-attack live with weapons drawn while you time tactical skills: Burst (2.2x), Species Special (3.2x), and + Heal & Shield!\n" +
                    "• Pet Party HP scales with every participating pet's Level. Watch for telegraphed Boss Casts (10,000 Needles!, Ultima!, etc.) and hit '+ Heal & Shield' to block 70% damage and avoid a Wipe!\n" +
                    "• Shared FFXIV-style Party Limit Break (0% -> 100%): charges over time, from skills, and gets bonus charge when mitigating casts. Using it unleashes a 5.5x CRIT and spends the bar for everyone!",
                    "• Enfrente 4 Mini-Bosses com Nível na aba 'Social & Boss' e na HUD de Arena ao vivo na tela: Lv.20 Cactuar Gigante, Lv.45 Rei Moggle Mog, Lv.70 Rei Behemoth e Lv.95 Ultima Chibi!\n" +
                    "• Até 8 pets cercam o boss e atacam sozinhos com as armas sacadas enquanto você usa as skills táticas: Golpe (2.2x), Especial da Espécie (3.2x) e + Cura & Escudo!\n" +
                    "• O HP dos Pets escala com o Nível de cada pet participante. Fique de olho nos Casts do Boss (10,000 Needles!, Ultima!, etc.) e use '+ Cura & Escudo' para reduzir 70% do dano e evitar Wipe!\n" +
                    "• Limit Break compartilhado estilo FFXIV (0% -> 100%): carrega com o tempo, skills e ao bloquear casts. Quem usar solta um CRIT de 5.5x e gasta a barra para toda a party!"));
            }

            using (UiCard.Begin(L.T("★ Invite Nearby Players in Your Zone!", "★ Convide Jogadores da Mesma Área!")))
            {
                UiControls.TextWrappedColored(Theme.TextLight, L.T(
                    "• Summoning a Mini-Boss with fewer than 8 party members? Check 'Invite nearby players in zone' (or click '+ Invite Zone Players' mid-fight)!\n" +
                    "• Invited players receive an interactive speech bubble directly on their floating Pet with Accept / Decline buttons and can jump into the battle at any time!",
                    "• Invocando um Mini-Boss com menos de 8 pessoas na party? Marque 'Convidar jogadores da área' (ou clique em '+ Convidar Jogadores da Área' no meio da luta)!\n" +
                    "• Quem está na mesma área recebe um balão de fala interativo direto no Pet flutuante com Aceitar / Recusar e entra na luta na hora!"));
            }

            using (UiCard.Begin(L.T("♥ Live Petting & Gifting Friends", "♥ Carinhos e Presentes ao Vivo")))
            {
                UiControls.TextWrappedColored(Theme.TextLight, L.T(
                    "• Click any friend's pet in the Party HUD or 'Social & Boss' tab to give live headpats, or gift them Kupo Nut Snacks & Energy Potions (even wakes up hibernating pets!).",
                    "• Clique no pet de qualquer amigo no HUD da Party ou na aba 'Social & Boss' para dar carinho ao vivo, ou presenteie Nozes Kupo e Poções de Energia (até acorda pets hibernando!)."));
            }

            using (UiCard.Begin(L.T("◆ UI Polish & Improvements", "◆ Melhorias de Interface & Polimento")))
            {
                UiControls.TextWrappedColored(Theme.TextLight, L.T(
                    "• Redesigned Egg Nursery grid (max 4 companions per row with larger centered previews!).\n" +
                    "• Sleek custom RPG HP, Limit Break & Boss Cast banners in both the Floating Arena HUD and Dashboard.\n" +
                    "• Automatic 'What's New' Changelog popup on updates, live Pet speech-bubble alerts when a new version is out, & improved Tutorial with auto-scrolling spotlights!",
                    "• Nova grade da Chocadeira (máximo 4 pets por linha com previews maiores e centralizados!).\n" +
                    "• Barras RPG redesenhadas de HP, Limit Break e Cast do Boss tanto na HUD flutuante quanto no Painel.\n" +
                    "• Popup automático de Novidades ao atualizar, aviso no balão de fala do Pet quando sair versão nova e Tutorial atualizado com auto-scroll inteligente!"));
            }

            ImGui.EndChild();

            // ── Opt-out checkbox + Bottom Dismiss Button ──
            ImGui.Dummy(new Vector2(0, 4f));
            var dontAutoShow = !cfg.AutoShowChangelogOnUpdate;
            if (ImGui.Checkbox(L.T(
                "Don't show this Changelog automatically on future updates##ChgOptOut",
                "Não mostrar este Changelog automaticamente nas próximas atualizações##ChgOptOut"), ref dontAutoShow))
            {
                cfg.AutoShowChangelogOnUpdate = !dontAutoShow;
                cfg.Save();
            }

            ImGui.Dummy(new Vector2(0, 4f));
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 0.94f, 0.35f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark);
            if (ImGui.Button(L.T("★  Awesome! Let's Play!", "★  Incrível! Bora Jogar!"), new Vector2(availW, 34f)))
            {
                MarkSeen();
                IsOpen = false;
            }
            ImGui.PopStyleColor(3);
        }
        finally
        {
            ImGui.PopStyleVar(pushedVars);
            ImGui.PopStyleColor(pushedColors);
        }
    }
}
