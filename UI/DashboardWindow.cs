using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PetFrenzy.Models;
using PetFrenzy.Services;

namespace PetFrenzy.UI;

public sealed class DashboardWindow : Window
{
    private readonly Random rng = new();

    // Adopt tab form
    private int selectedAdoptSpecies;
    private string adoptNameInput = "Carby";
    private bool adoptKeepLevel = true;

    // Mini-game "Catch the Treat" state
    private bool miniGameActive;
    private int miniGameScore;
    private float miniGameTimeLeft;
    private Vector2 treatNormPos = new(0.5f, 0.5f);

    public DashboardWindow() : base("PetFrenzy — Eorzea Tamagotchi###PetFrenzyDashboard", ImGuiWindowFlags.NoScrollbar)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(720, 500),
            MaximumSize = new Vector2(1400, 1000),
        };
        Size = new Vector2(820, 580);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var pushedColors = Theme.PushTheme();
        var pushedVars = Theme.PushStyleVars();

        try
        {
            DrawHeaderBar();
            ImGui.Spacing();

            if (ImGui.BeginTabBar("##PetFrenzyTabs"))
            {
                if (ImGui.BeginTabItem(L.T("My Pet", "Meu Pet")))
                {
                    DrawMyPetTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(L.T("Egg Nursery", "Chocadeira")))
                {
                    DrawNurseryTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(L.T("Stats & Log", "Estatísticas")))
                {
                    DrawStatsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(L.T("HUD & Settings", "HUD & Config")))
                {
                    DrawSettingsTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }
        finally
        {
            ImGui.PopStyleVar(pushedVars);
            ImGui.PopStyleColor(pushedColors);
        }
    }

    private void DrawHeaderBar()
    {
        var pet = Plugin.Config.Pet;
        ImGui.TextColored(Theme.YellowMain, "PETFRENZY");
        ImGui.SameLine();
        ImGui.TextColored(Theme.TextMuted, $"v1.0.0.0  •  {pet.Name} ({L.SpeciesName(pet.Species)} — {L.StageName(pet.Stage)})");

        var supportW = 98f;
        var langW = 68f;
        var rightOffset = ImGui.GetContentRegionAvail().X - (supportW + langW + 12f);
        if (rightOffset > 0) ImGui.SameLine(ImGui.GetCursorPosX() + rightOffset);
        else ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.95f, 0.32f, 0.38f, 0.90f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.00f, 0.44f, 0.49f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.80f, 0.22f, 0.28f, 1.00f));
        if (ImGui.Button(L.T("♥ Support Me", "♥ Apoiar"), new Vector2(supportW, 0)))
        {
            try { Process.Start(new ProcessStartInfo("https://ko-fi.com/n0way02") { UseShellExecute = true }); } catch { }
        }
        ImGui.PopStyleColor(3);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Support me on Ko-fi\nhttps://ko-fi.com/n0way02");
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                ImGui.SetClipboardText("https://ko-fi.com/n0way02");
        }

        ImGui.SameLine();
        if (ImGui.Button(L.IsPt ? "PT / EN" : "EN / PT", new Vector2(langW, 0)))
        {
            Plugin.Config.Language = L.IsPt ? "en" : "pt";
            Plugin.Config.Save();
        }

        ImGui.Separator();
    }

    private void DrawMyPetTab()
    {
        var pet = Plugin.Config.Pet;
        var sim = Plugin.Sim;
        var avail = ImGui.GetContentRegionAvail();
        var leftW = 240f;

        // Left pane: Live Pet Preview + Quick Care
        ImGui.BeginChild("##PetPreviewPane", new Vector2(leftW, avail.Y), true);
        var clicked = PetCanvasRenderer.DrawPetCanvas("##DashPetCanvas", pet, sim.CurrentMood, sim.AnimTimeSec, sim.WobblePulseSec, 196f, interactive: true);
        if (clicked) sim.PetCompanion();

        if (!string.IsNullOrEmpty(sim.SpeechBubble) && sim.SpeechTimerSec > 0f)
        {
            ImGui.TextColored(Theme.YellowMain, $"\"{sim.SpeechBubble}\"");
        }

        ImGui.Spacing();
        var nameBuf = pet.Name;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##PetNameEdit", ref nameBuf, 28))
        {
            pet.Name = nameBuf;
            Plugin.Config.Save();
        }

        ImGui.Spacing();
        if (!pet.IsHatched)
        {
            ImGui.TextColored(Theme.YellowMain, L.T($"Hatching: {pet.EggProgress:F0}%", $"Chocando: {pet.EggProgress:F0}%"));
            ImGui.ProgressBar(pet.EggProgress / 100f, new Vector2(-1, 18f));
            if (ImGui.Button(L.T("Warm & Pet Egg (+22%)", "Aquecer Ovo (+22%)"), new Vector2(-1, 30f)))
                sim.WarmEgg(22f);
        }
        else
        {
            if (ImGui.Button(L.T("🍖 Feed Snack", "🍖 Alimentar"), new Vector2(-1, 28f))) sim.Feed();
            if (ImGui.Button(L.T("♥ Give Headpats", "♥ Fazer Carinho"), new Vector2(-1, 28f))) sim.PetCompanion();
            if (ImGui.Button(sim.ManualNap ? L.T("☀ Wake Up", "☀ Acordar") : L.T("💤 Take a Nap", "💤 Tirar Soneca"), new Vector2(-1, 28f)))
                sim.ToggleNap();
        }
        ImGui.EndChild();

        ImGui.SameLine(0, 12f);

        // Right pane: Status Bars + Wardrobe + Mini-Game
        ImGui.BeginChild("##PetDetailsPane", new Vector2(avail.X - leftW - 12f, avail.Y), true);

        ImGui.TextColored(Theme.YellowMain, $"{pet.Name} — Lv.{pet.Level} ({L.StageName(pet.Stage)})");
        ImGui.ProgressBar((float)pet.Xp / pet.XpForNextLevel, new Vector2(-1, 16f), $"XP {pet.Xp} / {pet.XpForNextLevel}");

        ImGui.Spacing();
        DrawStatBar(L.T("Hunger", "Fome"), pet.Hunger, Theme.OrangeAccent);
        DrawStatBar(L.T("Happiness", "Felicidade"), pet.Happiness, Theme.PinkAccent);
        DrawStatBar(L.T("Energy", "Energia"), pet.Energy, Theme.CyanAccent);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(Theme.YellowMain, L.T("Wardrobe (Unlock Accessories by Leveling)", "Guarda-Roupa (Desbloqueie subindo de nível)"));

        var accs = Enum.GetValues<PetAccessory>();
        for (var i = 0; i < accs.Length; i++)
        {
            var acc = accs[i];
            var reqLv = PetStateData.UnlockLevelFor(acc);
            var unlocked = pet.Level >= reqLv;
            var equipped = pet.EquippedAccessory == acc;

            if (i % 3 != 0) ImGui.SameLine();

            ImGui.BeginDisabled(!unlocked);
            if (equipped)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain);
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark);
            }

            var btnLabel = unlocked
                ? L.AccessoryName(acc)
                : $"{L.AccessoryName(acc)} (Lv.{reqLv})";

            if (ImGui.Button(btnLabel, new Vector2(158f, 28f)))
            {
                pet.EquippedAccessory = acc;
                Plugin.Config.Save();
            }

            if (equipped) ImGui.PopStyleColor(2);
            ImGui.EndDisabled();
        }

        ImGui.Spacing();
        ImGui.Separator();
        DrawMiniGameSection();

        ImGui.EndChild();
    }

    private void DrawMiniGameSection()
    {
        ImGui.TextColored(Theme.YellowMain, L.T("Mini-Game: Catch the Golden Treat!", "Mini-Jogo: Pegue o Petisco Dourado!"));

        if (!miniGameActive)
        {
            ImGui.TextColored(Theme.TextMuted, L.T(
                "Click the golden treat as many times as you can in 10 seconds to earn bonus Happiness & XP!",
                "Clique no petisco dourado o máximo de vezes em 10 segundos para ganhar Felicidade e XP bônus!"));
            if (ImGui.Button(L.T("Start Mini-Game (10s)", "Iniciar Mini-Jogo (10s)"), new Vector2(210f, 28f)))
            {
                miniGameActive = true;
                miniGameScore = 0;
                miniGameTimeLeft = 10f;
                RespawnTreat();
            }
            return;
        }

        miniGameTimeLeft -= ImGui.GetIO().DeltaTime;
        if (miniGameTimeLeft <= 0f)
        {
            miniGameActive = false;
            Plugin.Sim.RewardMiniGame(miniGameScore);
            return;
        }

        ImGui.TextColored(Theme.GreenAccent, $"{L.T("Score", "Pontos")}: {miniGameScore}   |   {L.T("Time", "Tempo")}: {miniGameTimeLeft:0.0}s");

        var arenaSize = new Vector2(ImGui.GetContentRegionAvail().X, 110f);
        ImGui.BeginChild("##MiniGameArena", arenaSize, true, ImGuiWindowFlags.NoScrollbar);
        var inner = ImGui.GetContentRegionAvail();
        var btnSize = new Vector2(46f, 28f);
        var pos = new Vector2(
            MathF.Max(4f, treatNormPos.X * (inner.X - btnSize.X - 8f)),
            MathF.Max(4f, treatNormPos.Y * (inner.Y - btnSize.Y - 8f)));

        ImGui.SetCursorPos(pos);
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark);
        if (ImGui.Button("★", btnSize))
        {
            miniGameScore++;
            RespawnTreat();
        }
        ImGui.PopStyleColor(2);
        ImGui.EndChild();
    }

    private void RespawnTreat()
    {
        treatNormPos = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
    }

    private void DrawNurseryTab()
    {
        ImGui.TextColored(Theme.YellowMain, L.T("Choose & Hatch a New Companion Egg!", "Escolha e Choque um Novo Ovo!"));
        ImGui.TextWrapped(L.T(
            "Pick any of the 4 iconic Eorzea companions below. Your new friend starts as an animated Egg that warms up as you play (or tap the egg to hatch it faster!).",
            "Escolha qualquer um dos 4 mascotes de Eorzea abaixo. Seu novo amigo começa como um Ovo animado que aquece enquanto você joga (ou clique no ovo para chocar mais rápido!)."));
        ImGui.Spacing();

        var species = Enum.GetValues<PetSpecies>();
        var cardW = (ImGui.GetContentRegionAvail().X - 30f) / 4f;

        for (var i = 0; i < species.Length; i++)
        {
            var sp = species[i];
            if (i > 0) ImGui.SameLine(0, 10f);

            var isSel = selectedAdoptSpecies == i;
            if (isSel) ImGui.PushStyleColor(ImGuiCol.Border, Theme.YellowMain);
            ImGui.BeginChild($"##AdoptCard{i}", new Vector2(cardW, 190f), true, ImGuiWindowFlags.NoScrollbar);

            var previewPet = new PetStateData { Species = sp, IsHatched = true, Level = 5 };
            if (PetCanvasRenderer.DrawPetCanvas($"##Prev{i}", previewPet, isSel ? PetMood.Happy : PetMood.Idle, Plugin.Sim.AnimTimeSec, 0f, MathF.Min(120f, cardW - 20f)))
            {
                selectedAdoptSpecies = i;
                adoptNameInput = PetStateData.DefaultNameFor(sp);
            }

            ImGui.TextColored(isSel ? Theme.YellowMain : Theme.TextWhite, L.SpeciesName(sp));
            if (ImGui.Button(isSel ? L.T("Selected ✓", "Selecionado ✓") : L.T("Select", "Selecionar"), new Vector2(-1, 24f)))
            {
                selectedAdoptSpecies = i;
                adoptNameInput = PetStateData.DefaultNameFor(sp);
            }

            ImGui.EndChild();
            if (isSel) ImGui.PopStyleColor();
        }

        ImGui.Spacing();
        ImGui.Separator();

        ImGui.SetNextItemWidth(240f);
        ImGui.InputText(L.T("Companion Name", "Nome do Mascote"), ref adoptNameInput, 28);
        ImGui.Checkbox(L.T("Keep current Level & unlocked accessories", "Manter Nível atual e acessórios desbloqueados"), ref adoptKeepLevel);

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark);
        if (ImGui.Button(L.T("Adopt & Start Hatching Egg!", "Adotar e Começar a Chocar Ovo!"), new Vector2(260f, 34f)))
        {
            Plugin.Sim.AdoptNewEgg((PetSpecies)selectedAdoptSpecies, adoptNameInput, adoptKeepLevel);
        }
        ImGui.PopStyleColor(2);
    }

    private void DrawStatsTab()
    {
        var pet = Plugin.Config.Pet;
        ImGui.TextColored(Theme.YellowMain, L.T("Lifetime Companion Stats", "Estatísticas do Companheiro"));
        ImGui.Separator();

        ImGui.Columns(2, "##PetStatsCols", false);
        ImGui.TextUnformatted($"{L.T("Gil Witnessed", "Gil Presenciado")}: {pet.TotalGilWitnessed:N0} gil");
        ImGui.TextUnformatted($"{L.T("Combats Cheered", "Combates Torcidos")}: {pet.CombatsCheered}");
        ImGui.TextUnformatted($"{L.T("Duties Celebrated", "Duties Comemoradas")}: {pet.DutiesCelebrated}");
        ImGui.NextColumn();
        ImGui.TextUnformatted($"{L.T("Deaths Mourned", "Mortes Choradas")}: {pet.DeathsMourned}");
        ImGui.TextUnformatted($"{L.T("Snacks Eaten", "Petiscos Comidos")}: {pet.SnacksEaten}");
        ImGui.TextUnformatted($"{L.T("Headpats Received", "Carinhos Recebidos")}: {pet.PetsReceived}");
        ImGui.Columns(1);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(Theme.YellowMain, L.T("Recent Companion Activity", "Atividade Recente"));

        ImGui.BeginChild("##PetLogScroll", new Vector2(0, ImGui.GetContentRegionAvail().Y), true);
        foreach (var entry in Plugin.Sim.ActivityLog)
        {
            ImGui.TextColored(Theme.TextMuted, $"[{entry.TimeLocal:HH:mm}]");
            ImGui.SameLine();
            ImGui.TextUnformatted(entry.Message);
        }
        ImGui.EndChild();
    }

    private void DrawSettingsTab()
    {
        var cfg = Plugin.Config;
        ImGui.TextColored(Theme.YellowMain, L.T("Floating HUD Settings", "Configurações do HUD Flutuante"));
        ImGui.Separator();

        var showHud = cfg.ShowHud;
        if (ImGui.Checkbox(L.T("Show floating Pet HUD", "Mostrar HUD flutuante do Pet"), ref showHud))
        { cfg.ShowHud = showHud; cfg.Save(); }

        var transparent = cfg.TransparentHud;
        if (ImGui.Checkbox(L.T("Transparent / borderless HUD background", "Fundo transparente / sem borda no HUD"), ref transparent))
        { cfg.TransparentHud = transparent; cfg.Save(); }

        var lockPos = cfg.LockHudPosition;
        if (ImGui.Checkbox(L.T("Lock HUD position", "Travar posição do HUD"), ref lockPos))
        { cfg.LockHudPosition = lockPos; cfg.Save(); }

        var quickBtns = cfg.ShowHudQuickButtons;
        if (ImGui.Checkbox(L.T("Show quick care buttons under pet", "Mostrar botões rápidos abaixo do pet"), ref quickBtns))
        { cfg.ShowHudQuickButtons = quickBtns; cfg.Save(); }

        var bubbles = cfg.ShowSpeechBubbles;
        if (ImGui.Checkbox(L.T("Show speech bubbles", "Mostrar balões de fala"), ref bubbles))
        { cfg.ShowSpeechBubbles = bubbles; cfg.Save(); }

        var hideCutscene = cfg.HideHudInCutscene;
        if (ImGui.Checkbox(L.T("Auto-hide HUD during cutscenes", "Esconder HUD automaticamente em cutscenes"), ref hideCutscene))
        { cfg.HideHudInCutscene = hideCutscene; cfg.Save(); }

        var scale = cfg.HudScale;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderFloat(L.T("HUD Scale", "Tamanho do HUD"), ref scale, 0.75f, 1.6f, "%.2fx"))
        { cfg.HudScale = scale; cfg.Save(); }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(Theme.YellowMain, L.T("Commands", "Comandos"));
        ImGui.BulletText("/petfrenzy  |  /pet — " + L.T("Open/close Dashboard", "Abrir/fechar Painel"));
        ImGui.BulletText("/pet hud — " + L.T("Toggle floating HUD visibility", "Mostrar/esconder HUD flutuante"));
    }

    private static void DrawStatBar(string label, float val, Vector4 col)
    {
        ImGui.TextColored(Theme.TextMuted, $"{label}: {val:F0}%");
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, col);
        ImGui.ProgressBar(Math.Clamp(val / 100f, 0f, 1f), new Vector2(-1, 13f), string.Empty);
        ImGui.PopStyleColor();
    }
}
