using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PetFrenzy.Models;
using PetFrenzy.Services;

namespace PetFrenzy.UI;

public sealed class DashboardWindow : Window
{
    // Adopt tab form
    private int selectedAdoptSpecies;
    private string adoptNameInput = "Carby";
    private bool adoptKeepLevel = true;

    // Wardrobe & Shop live preview state
    private bool wardrobeCombatPose;
    private bool shopCombatPose;
    private PetAccessory? shopPreviewHat;
    private PetOutfit? shopPreviewOutfit;
    private WeaponSkin? shopPreviewWeaponSkin;

    public DashboardWindow() : base("PetFrenzy — Eorzea Tamagotchi###PetFrenzyDashboard", ImGuiWindowFlags.NoScrollbar)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(680, 480),
            MaximumSize = new Vector2(1500, 1050),
        };
        Size = new Vector2(920, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw()
    {
        Flags = Plugin.Tutorial.IsActive
            ? (ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBringToFrontOnFocus)
            : ImGuiWindowFlags.NoScrollbar;
    }

    public override void Draw()
    {
        var pushedColors = Theme.PushTheme();
        var pushedVars = Theme.PushStyleVars();

        try
        {
            Plugin.Config.Pet.EnsureCollections();

            // Auto-launch Milly's interactive spotlight tutorial on first-ever open!
            if (!Plugin.Config.TutorialCompleted && !Plugin.Tutorial.IsActive)
            {
                Plugin.Tutorial.Start();
            }

            DrawHeaderBar();
            ImGui.Spacing();

            if (ImGui.BeginTabBar("##PetFrenzyTabs"))
            {
                if (ImGui.BeginTabItem(L.T("My Pet", "Meu Pet"), Plugin.Tutorial.ConsumeTabFlag("tab_mypet")))
                {
                    Plugin.Tutorial.RegisterTarget("tab_mypet");
                    DrawMyPetTab();
                    ImGui.EndTabItem();
                }
                else
                {
                    Plugin.Tutorial.RegisterTarget("tab_mypet");
                }

                if (ImGui.BeginTabItem(L.T("Wardrobe", "Guarda-Roupa"), Plugin.Tutorial.ConsumeTabFlag("tab_wardrobe")))
                {
                    Plugin.Tutorial.RegisterTarget("tab_wardrobe");
                    DrawWardrobeTab();
                    ImGui.EndTabItem();
                }
                else
                {
                    Plugin.Tutorial.RegisterTarget("tab_wardrobe");
                }

                if (ImGui.BeginTabItem(L.T("Shop", "Loja"), Plugin.Tutorial.ConsumeTabFlag("tab_shop")))
                {
                    Plugin.Tutorial.RegisterTarget("tab_shop");
                    DrawShopTab();
                    ImGui.EndTabItem();
                }
                else
                {
                    Plugin.Tutorial.RegisterTarget("tab_shop");
                }

                if (ImGui.BeginTabItem(L.T("Rewards & Log", "Recompensas"), Plugin.Tutorial.ConsumeTabFlag("tab_rewards")))
                {
                    Plugin.Tutorial.RegisterTarget("tab_rewards");
                    DrawRewardsAndStatsTab();
                    ImGui.EndTabItem();
                }
                else
                {
                    Plugin.Tutorial.RegisterTarget("tab_rewards");
                }

                if (ImGui.BeginTabItem(L.T("Egg Nursery", "Chocadeira"), Plugin.Tutorial.ConsumeTabFlag("tab_nursery")))
                {
                    Plugin.Tutorial.RegisterTarget("tab_nursery");
                    DrawNurseryTab();
                    ImGui.EndTabItem();
                }
                else
                {
                    Plugin.Tutorial.RegisterTarget("tab_nursery");
                }

                if (ImGui.BeginTabItem(L.T("HUD & Settings", "HUD & Config"), Plugin.Tutorial.ConsumeTabFlag("tab_settings")))
                {
                    Plugin.Tutorial.RegisterTarget("tab_settings");
                    DrawSettingsTab();
                    ImGui.EndTabItem();
                }
                else
                {
                    Plugin.Tutorial.RegisterTarget("tab_settings");
                }

                if (ImGui.BeginTabItem(L.T("About", "Sobre"), Plugin.Tutorial.ConsumeTabFlag("tab_about")))
                {
                    Plugin.Tutorial.RegisterTarget("tab_about");
                    DrawAboutTab();
                    ImGui.EndTabItem();
                }
                else
                {
                    Plugin.Tutorial.RegisterTarget("tab_about");
                }

                if (Plugin.Sim.IsCreatorCharacter() && ImGui.BeginTabItem("★ Dev"))
                {
                    DrawCreatorDevTab();
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
        var sim = Plugin.Sim;

        ImGui.TextColored(Theme.YellowMain, "PETFRENZY");
        ImGui.SameLine();
        ImGui.TextColored(Theme.TextMuted, $"v1.0.0.2  •  {pet.Name} (Lv.{pet.Level}/{PetStateData.MaxLevel})  •");
        ImGui.SameLine();
        ImGui.TextColored(Theme.YellowMain, $"{pet.Coins:N0} {L.T("Coins", "Moedas")}");

        var nextSec = (int)sim.SecondsUntilNextOnlineCoins;
        ImGui.SameLine();
        ImGui.TextColored(Theme.TextMuted, pet.IsHibernating
            ? L.T("(Hibernating — Paused)", "(Hibernando — Pausado)")
            : $"({L.T($"+{PetSimulationService.OnlineRewardCoins} in", $"+{PetSimulationService.OnlineRewardCoins} em")} {nextSec / 60:00}:{nextSec % 60:00})");

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
        var leftW = Math.Clamp(avail.X * 0.28f, 195f, 240f);
        var canvasSz = MathF.Min(190f, leftW - 24f);

        // Left pane: Live Pet Preview + Quick Care
        ImGui.BeginChild("##PetPreviewPane", new Vector2(leftW, avail.Y), true);
        var weaponStyle = Plugin.Config.ShowJobWeapon ? sim.CurrentJob.Weapon : JobWeaponStyle.None;
        var clicked = PetCanvasRenderer.DrawPetCanvas("##DashPetCanvas", pet, sim.CurrentMood, sim.AnimTimeSec, sim.WobblePulseSec, canvasSz, interactive: true, weaponStyle: weaponStyle, isWeaponDrawn: sim.IsWeaponDrawn);
        if (clicked) sim.PetCompanion();

        if (!string.IsNullOrEmpty(sim.SpeechBubble) && sim.SpeechTimerSec > 0f)
        {
            UiControls.TextWrappedColored(Theme.YellowMain, $"\"{sim.SpeechBubble}\"");
        }
        else
        {
            ImGui.Dummy(new Vector2(0, ImGui.GetTextLineHeight()));
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
            if (ImGui.Button(L.T("♥ Give Headpats (Free)", "♥ Fazer Carinho (Grátis)"), new Vector2(-1, 28f))) sim.PetCompanion();
            if (ImGui.Button(sim.ManualNap ? L.T("★ Wake Up", "★ Acordar") : L.T("Zzz  Take a Nap", "Zzz  Tirar Soneca"), new Vector2(-1, 28f)))
                sim.ToggleNap();

            if (sim.ActiveDuty is { } duty)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.TextColored(Theme.CyanAccent, L.T("Active Duty Detected:", "Instância Detectada:"));
                ImGui.TextWrapped($"{duty.DutyName}");
                UiControls.TextWrappedColored(Theme.YellowMain, $"[{L.T(duty.Rule.LabelEn, duty.Rule.LabelPt)}] +{duty.Rule.ClearCoins} Coins");
            }
            else if (sim.CurrentFieldZone != FieldZoneKind.None)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.TextColored(Theme.CyanAccent, L.T("Active Zone Bonus:", "Zona Especial Ativa:"));
                UiControls.TextWrappedColored(Theme.YellowMain, PetSimulationService.FieldZoneLabel(sim.CurrentFieldZone));
            }
        }
        ImGui.EndChild();
        Plugin.Tutorial.RegisterTarget("mypet_main");

        ImGui.SameLine(0, 12f);

        // Right pane: Status Bars + Hibernation Banner + Quick Feed & Potions + Daily XP Pace
        ImGui.BeginChild("##PetDetailsPane", new Vector2(avail.X - leftW - 12f, avail.Y), true);

        if (pet.IsHibernating || pet.HasFreeWakeupMeal)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.24f, 0.06f, 0.34f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.Border, Theme.YellowMain);
            ImGui.BeginChild("##HibernationBanner", new Vector2(0, pet.IsHibernating ? 74f : 56f), true, ImGuiWindowFlags.NoScrollbar);
            if (pet.IsHibernating)
            {
                UiControls.TextWrappedColored(Theme.YellowMain, L.T(
                    "Zzz  HIBERNATING — No XP or Coins earned until woken up! Pet or feed your companion to wake them.",
                    "Zzz  HIBERNANDO — Sem ganho de XP ou Moedas até acordar! Faça carinho ou alimente seu pet."));
            }
            if (pet.HasFreeWakeupMeal)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain);
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark);
                if (ImGui.Button(L.T("★ Claim Free Wake-Up Snack (+45% Hunger, +30% Energy)", "★ Pegar Comida Grátis (+45% Fome, +30% Energia)"), new Vector2(-1, 26f)))
                {
                    sim.UseFreeWakeupMeal();
                }
                ImGui.PopStyleColor(2);
            }
            ImGui.EndChild();
            ImGui.PopStyleColor(2);
            ImGui.Spacing();
        }

        var xpText = pet.Level >= PetStateData.MaxLevel ? "MAX" : $"XP {pet.Xp:N0} / {pet.XpForNextLevel:N0}";
        UiControls.TextWrappedColored(Theme.YellowMain, $"{pet.Name} — Lv.{pet.Level} / {PetStateData.MaxLevel} ({L.StageName(pet.Stage)})  •  {xpText}");
        UiControls.TextWrappedColored(Theme.TextMuted, L.T(
            "Hunger drains 100% in 16h (hibernates 12h after 0%) • Activities drain Energy",
            "Fome esvazia 100% em 16h (hiberna após 12h em 0%) • Atividades gastam Energia"));
        ImGui.ProgressBar(pet.Level >= PetStateData.MaxLevel ? 1f : (float)pet.Xp / pet.XpForNextLevel, new Vector2(-1, 13f), string.Empty);

        ImGui.Spacing();
        var hungerCol = pet.Hunger <= 20f ? Theme.RedDanger : Theme.OrangeAccent;
        DrawStatBar(L.T("Hunger", "Fome"), pet.Hunger, hungerCol);
        DrawStatBar(L.T("Happiness", "Felicidade"), pet.Happiness, Theme.PinkAccent);
        DrawStatBar(L.T("Energy", "Energia"), pet.Energy, Theme.CyanAccent);

        // Quick Food Menu
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(Theme.YellowMain, L.T($"Quick Feed  (Balance: {pet.Coins:N0} Coins)", $"Alimentar Rápido  (Saldo: {pet.Coins:N0} Moedas)"));

        var foodAvailW = ImGui.GetContentRegionAvail().X;
        var stackFood = foodAvailW < 380f;
        var shopBtnW = stackFood ? -1f : (foodAvailW - 16f) / 3f;
        for (var i = 0; i < FoodCatalog.Menu.Length; i++)
        {
            var food = FoodCatalog.Menu[i];
            if (!stackFood && i > 0) ImGui.SameLine(0, 8f);

            var canAfford = pet.Coins >= food.CostCoins && pet.IsHatched;
            ImGui.BeginDisabled(!canAfford);
            var foodTitle = L.IsPt ? food.NamePt : food.NameEn;
            if (ImGui.Button($"{foodTitle}\n{food.CostCoins} {L.T("Coins", "Moedas")} (+{food.HungerRestore:F0}%)", new Vector2(shopBtnW, stackFood ? 36f : 44f)))
            {
                sim.BuyAndFeed(food);
            }
            ImGui.EndDisabled();

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(L.T(
                    $"{food.NameEn}\nCost: {food.CostCoins} Coins\n+{food.HungerRestore:F0}% Hunger, +{food.HappinessRestore:F0}% Happy, +{food.EnergyRestore:F0}% Energy, +{food.XpReward} XP",
                    $"{food.NamePt}\nCusto: {food.CostCoins} Moedas\n+{food.HungerRestore:F0}% Fome, +{food.HappinessRestore:F0}% Feliz, +{food.EnergyRestore:F0}% Energia, +{food.XpReward} XP"));
            }
        }

        // Quick Energy Potions
        ImGui.Spacing();
        ImGui.TextColored(Theme.CyanAccent, L.T("Quick Energy Potions", "Poções Rápidas de Energia"));
        for (var i = 0; i < EnergyPotionCatalog.Menu.Length; i++)
        {
            var pot = EnergyPotionCatalog.Menu[i];
            if (!stackFood && i > 0) ImGui.SameLine(0, 8f);

            var canAfford = pet.Coins >= pot.CostCoins && pet.IsHatched && !pet.IsHibernating;
            ImGui.BeginDisabled(!canAfford);
            var potTitle = L.IsPt ? pot.NamePt : pot.NameEn;
            if (ImGui.Button($"{potTitle}\n{pot.CostCoins} {L.T("Coins", "Moedas")} (+{pot.EnergyRestore:F0}% EN)", new Vector2(shopBtnW, stackFood ? 36f : 42f)))
            {
                sim.BuyAndDrinkPotion(pot);
            }
            ImGui.EndDisabled();

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(L.T(
                    $"{pot.NameEn}\nCost: {pot.CostCoins} Coins\n+{pot.EnergyRestore:F0}% Energy, +{pot.HappinessRestore:F0}% Happy, +{pot.XpReward} XP",
                    $"{pot.NamePt}\nCusto: {pot.CostCoins} Moedas\n+{pot.EnergyRestore:F0}% Energia, +{pot.HappinessRestore:F0}% Feliz, +{pot.XpReward} XP"));
            }
        }

        // Equipped summary (wrapped so it never clips!)
        ImGui.Spacing();
        ImGui.Separator();
        var skinInfo = WeaponSkinCatalog.Get(pet.EquippedWeaponSkin);
        UiControls.TextWrappedColored(Theme.TextMuted, L.T(
            $"Equipped: Hat [{L.AccessoryName(pet.EquippedAccessory)}] • Outfit [{L.OutfitName(pet.EquippedOutfit)}] • Aura [{skinInfo.NameEn}]",
            $"Equipado: Chapéu [{L.AccessoryName(pet.EquippedAccessory)}] • Roupa [{L.OutfitName(pet.EquippedOutfit)}] • Aura [{skinInfo.NamePt}]"));

        // Daily XP Pace (Anti-Rush System)
        ImGui.Spacing();
        ImGui.Separator();
        pet.RefreshDailyXpWindow();
        var mult = pet.CurrentXpMultiplier;
        var (paceLabel, paceCol) = mult >= 0.99f
            ? (L.T("Rested — 100% XP Rate", "Descansado — 100% XP"), Theme.GreenAccent)
            : mult >= 0.30f
                ? (L.T("Tired — 35% XP Rate (Soft Cap)", "Cansado — 35% XP (Limite Diário)"), Theme.OrangeAccent)
                : (L.T("Exhausted — 10% XP Rate (Resets 00:00 UTC)", "Exausto — 10% XP (Reseta 00:00 UTC)"), Theme.RedDanger);

        UiControls.TextWrappedColored(paceCol, $"{L.T("Daily XP Pace", "Ritmo Diário de XP")}  •  {paceLabel}");
        var dailyFrac = Math.Clamp((float)pet.DailyXpEarned / PetStateData.DailyFullXpCap, 0f, 1f);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, paceCol);
        ImGui.ProgressBar(dailyFrac, new Vector2(-1, 16f), $"{pet.DailyXpEarned:N0} / {PetStateData.DailyFullXpCap:N0} XP");
        ImGui.PopStyleColor();
        UiControls.TextWrappedColored(Theme.TextMuted, L.T(
            "Earn Coins & XP from ALL FFXIV content: Roulettes, Dungeons, Trials, Raids, PvP, FATEs, Bozja, Eureka, Occult Crescent, Crafting & Gathering!",
            "Ganhe Moedas e XP em TODO conteúdo do FFXIV: Roletas, Dungeons, Trials, Raids, PvP, FATEs, Bozja, Eureka, Occult Crescent, Craft e Coleta!"));

        ImGui.EndChild();
    }

    private void DrawWardrobeTab()
    {
        var pet = Plugin.Config.Pet;
        var sim = Plugin.Sim;
        var avail = ImGui.GetContentRegionAvail();
        var leftW = Math.Clamp(avail.X * 0.28f, 195f, 248f);
        var canvasSz = MathF.Min(192f, leftW - 24f);

        // Left: Live Dressing Room Preview
        ImGui.BeginChild("##WardrobePreview", new Vector2(leftW, avail.Y), true);
        ImGui.TextColored(Theme.YellowMain, L.T("Dressing Room", "Provador ao Vivo"));
        ImGui.TextColored(Theme.TextMuted, $"{pet.Name} • Lv.{pet.Level}");

        var previewWeapon = sim.CurrentJob.Weapon != JobWeaponStyle.None ? sim.CurrentJob.Weapon : JobWeaponStyle.Greatsword;
        PetCanvasRenderer.DrawPetCanvas("##WardrobeCanvas", pet, wardrobeCombatPose ? PetMood.Cheering : PetMood.Happy,
            sim.AnimTimeSec, 0f, canvasSz, interactive: false, weaponStyle: previewWeapon, isWeaponDrawn: wardrobeCombatPose);

        ImGui.Checkbox(L.T("Combat Pose", "Pose de Combate"), ref wardrobeCombatPose);

        ImGui.Spacing();
        ImGui.Separator();
        UiControls.TextWrappedColored(Theme.TextMuted, L.T(
            "Unlock new Hats, Outfits, Wings, Weapon Auras & Custom Colors in the Shop tab as you level up (up to Lv.100)!",
            "Desbloqueie novos Chapéus, Roupas, Asas, Auras de Armas e Cores Personalizadas na aba Loja conforme sobe até o Lv.100!"));
        ImGui.EndChild();

        ImGui.SameLine(0, 12f);

        // Right: Owned Cosmetics & Custom Body Colors
        ImGui.BeginChild("##WardrobeControls", new Vector2(avail.X - leftW - 12f, avail.Y), true);

        var rightW = ImGui.GetContentRegionAvail().X;
        var cols = rightW >= 460f ? 3 : 2;
        var btnW = (rightW - 8f * (cols - 1)) / cols;

        // 1. Owned Hats
        ImGui.TextColored(Theme.YellowMain, L.T($"Owned Hats ({pet.UnlockedHats.Count}/{CosmeticCatalog.Hats.Length})", $"Chapéus Desbloqueados ({pet.UnlockedHats.Count}/{CosmeticCatalog.Hats.Length})"));
        var ownedHats = CosmeticCatalog.Hats.Where(h => pet.UnlockedHats.Contains(h.Hat)).ToList();
        for (var i = 0; i < ownedHats.Count; i++)
        {
            var h = ownedHats[i];
            if (i % cols != 0) ImGui.SameLine(0, 8f);
            var active = pet.EquippedAccessory == h.Hat;
            if (active) { ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain); ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark); }
            if (ImGui.Button($"{(L.IsPt ? h.NamePt : h.NameEn)}##wh_{h.Hat}", new Vector2(btnW, 26f)))
            {
                pet.EquippedAccessory = h.Hat;
                Plugin.Config.Save();
            }
            if (active) ImGui.PopStyleColor(2);
        }

        // 2. Owned Outfits & Wings
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(Theme.YellowMain, L.T($"Owned Outfits & Wings ({pet.UnlockedOutfits.Count}/{CosmeticCatalog.Outfits.Length})", $"Roupas & Asas ({pet.UnlockedOutfits.Count}/{CosmeticCatalog.Outfits.Length})"));
        var ownedOutfits = CosmeticCatalog.Outfits.Where(o => pet.UnlockedOutfits.Contains(o.Outfit)).ToList();
        for (var i = 0; i < ownedOutfits.Count; i++)
        {
            var o = ownedOutfits[i];
            if (i % cols != 0) ImGui.SameLine(0, 8f);
            var active = pet.EquippedOutfit == o.Outfit;
            if (active) { ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain); ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark); }
            if (ImGui.Button($"{(L.IsPt ? o.NamePt : o.NameEn)}##wo_{o.Outfit}", new Vector2(btnW, 26f)))
            {
                pet.EquippedOutfit = o.Outfit;
                Plugin.Config.Save();
            }
            if (active) ImGui.PopStyleColor(2);
        }

        // 3. Owned Weapon Auras
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(Theme.YellowMain, L.T($"Owned Weapon Auras ({pet.UnlockedWeaponSkins.Count}/{WeaponSkinCatalog.All.Length})", $"Auras de Armas ({pet.UnlockedWeaponSkins.Count}/{WeaponSkinCatalog.All.Length})"));
        var ownedAuras = WeaponSkinCatalog.All.Where(a => pet.UnlockedWeaponSkins.Contains(a.Skin)).ToList();
        for (var i = 0; i < ownedAuras.Count; i++)
        {
            var a = ownedAuras[i];
            if (i % cols != 0) ImGui.SameLine(0, 8f);
            var active = pet.EquippedWeaponSkin == a.Skin;
            if (active) { ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain); ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark); }
            if (ImGui.Button($"{(L.IsPt ? a.NamePt : a.NameEn)}##wa_{a.Skin}", new Vector2(btnW, 26f)))
            {
                pet.EquippedWeaponSkin = a.Skin;
                Plugin.Config.Save();
            }
            if (active) ImGui.PopStyleColor(2);
        }

        // 4. Custom Body Part Colors
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(Theme.YellowMain, L.T("Custom Body Part Colors", "Cores Personalizadas do Pet"));
        if (!pet.UnlockedCustomColors)
        {
            UiControls.TextWrappedColored(Theme.TextMuted, L.T(
                $"Locked — buy the Custom Color Dye Kit in the Shop (Lv.{CosmeticCatalog.DyeKitRequiredLevel} • {CosmeticCatalog.DyeKitCostCoins} Coins).",
                $"Bloqueado — compre o Kit de Cores na aba Loja (Lv.{CosmeticCatalog.DyeKitRequiredLevel} • {CosmeticCatalog.DyeKitCostCoins} Moedas)."));
        }
        else
        {
            var useCustom = pet.UseCustomColors;
            if (ImGui.Checkbox(L.T("Enable Custom Colors", "Ativar Cores Personalizadas"), ref useCustom))
            {
                pet.UseCustomColors = useCustom;
                Plugin.Config.Save();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton(L.T("Reset to Species Default", "Resetar Padrão")))
            {
                var def = PetStateData.DefaultPaletteFor(pet.Species);
                pet.CustomBodyColor = def.Body;
                pet.CustomAccentColor = def.Accent;
                pet.CustomDetailColor = def.Detail;
                Plugin.Config.Save();
            }

            if (pet.UseCustomColors)
            {
                var body = pet.CustomBodyColor;
                if (ImGui.ColorEdit4(L.T("Primary Body", "Cor Principal (Corpo)"), ref body, ImGuiColorEditFlags.NoAlpha))
                { pet.CustomBodyColor = body; Plugin.Config.Save(); }

                var accent = pet.CustomAccentColor;
                if (ImGui.ColorEdit4(L.T("Secondary (Ears / Tail / Wings / Scarf)", "Cor Secundária (Orelhas / Cauda / Asas)"), ref accent, ImGuiColorEditFlags.NoAlpha))
                { pet.CustomAccentColor = accent; Plugin.Config.Save(); }

                var detail = pet.CustomDetailColor;
                if (ImGui.ColorEdit4(L.T("Detail (Blush / Gem / Pom-pom)", "Cor de Detalhe (Bochechas / Gema / Pom-pom)"), ref detail, ImGuiColorEditFlags.NoAlpha))
                { pet.CustomDetailColor = detail; Plugin.Config.Save(); }
            }
        }

        ImGui.EndChild();
    }

    private void DrawShopTab()
    {
        var pet = Plugin.Config.Pet;
        var sim = Plugin.Sim;
        var avail = ImGui.GetContentRegionAvail();
        var leftW = Math.Clamp(avail.X * 0.28f, 195f, 248f);
        var canvasSz = MathF.Min(192f, leftW - 24f);

        // Left: Live Shop Try-On Preview
        ImGui.BeginChild("##ShopPreview", new Vector2(leftW, avail.Y), true);
        ImGui.TextColored(Theme.YellowMain, L.T("Shop Try-On Preview", "Provador da Loja"));
        ImGui.TextColored(Theme.TextMuted, $"{pet.Coins:N0} {L.T("Coins", "Moedas")} • Lv.{pet.Level}");

        var tryPet = new PetStateData
        {
            Species = pet.Species,
            Name = pet.Name,
            IsHatched = true,
            Level = pet.Level,
            EquippedAccessory = shopPreviewHat ?? pet.EquippedAccessory,
            EquippedOutfit = shopPreviewOutfit ?? pet.EquippedOutfit,
            EquippedWeaponSkin = shopPreviewWeaponSkin ?? pet.EquippedWeaponSkin,
            UseCustomColors = pet.UseCustomColors,
            CustomBodyColor = pet.CustomBodyColor,
            CustomAccentColor = pet.CustomAccentColor,
            CustomDetailColor = pet.CustomDetailColor,
        };
        var previewWeapon = sim.CurrentJob.Weapon != JobWeaponStyle.None ? sim.CurrentJob.Weapon : JobWeaponStyle.Greatsword;

        PetCanvasRenderer.DrawPetCanvas("##ShopCanvas", tryPet, shopCombatPose ? PetMood.Cheering : PetMood.Happy,
            sim.AnimTimeSec, 0f, canvasSz, interactive: false, weaponStyle: previewWeapon, isWeaponDrawn: shopCombatPose);

        ImGui.Checkbox(L.T("Combat Pose", "Pose de Combate"), ref shopCombatPose);
        if (ImGui.Button(L.T("Reset Preview", "Resetar Preview"), new Vector2(-1, 24f)))
        {
            shopPreviewHat = null;
            shopPreviewOutfit = null;
            shopPreviewWeaponSkin = null;
        }
        ImGui.EndChild();

        ImGui.SameLine(0, 12f);

        // Right: Categorized Shop Sub-Tabs
        ImGui.BeginChild("##ShopCatalogPane", new Vector2(avail.X - leftW - 12f, avail.Y), true);
        if (ImGui.BeginTabBar("##ShopSubTabs"))
        {
            if (ImGui.BeginTabItem(L.T("Food", "Comida")))
            {
                DrawShopFoodSubTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(L.T("Energy Potions", "Poções de Energia")))
            {
                DrawShopPotionsSubTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(L.T("Hats", "Chapéus")))
            {
                DrawShopHatsSubTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(L.T("Outfits & Wings", "Roupas & Asas")))
            {
                DrawShopOutfitsSubTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(L.T("Weapon Auras", "Auras de Armas")))
            {
                DrawShopAurasSubTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(L.T("Color Dye Kit", "Kit de Cores")))
            {
                DrawShopDyeSubTab();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        ImGui.EndChild();
    }

    private void DrawShopFoodSubTab()
    {
        var pet = Plugin.Config.Pet;
        UiControls.TextWrappedColored(Theme.TextMuted, L.T(
            "Feed your pet regularly — Hunger drains 100% in 16 hours. After 12h at 0% Hunger, your pet hibernates (no XP/Coins) and grants 1 Free Wake-Up Snack!",
            "Alimente seu pet com frequência — a Fome esvazia 100% em 16 horas. Após 12h com 0% de Fome, ele hiberna (sem XP/Moedas) e libera 1 Comida Grátis!"));
        ImGui.Spacing();

        foreach (var food in FoodCatalog.Menu)
        {
            ImGui.BeginChild($"##ShopFood_{food.Id}", new Vector2(0, 58f), true, ImGuiWindowFlags.NoScrollbar);
            ImGui.TextColored(Theme.YellowMain, L.IsPt ? food.NamePt : food.NameEn);
            var btnW = 130f;
            var rx = ImGui.GetContentRegionAvail().X - btnW;
            if (rx > 0) ImGui.SameLine(ImGui.GetCursorPosX() + rx);
            else ImGui.SameLine();

            ImGui.BeginDisabled(pet.Coins < food.CostCoins || !pet.IsHatched);
            if (ImGui.Button($"{food.CostCoins} {L.T("Coins", "Moedas")}##bf_{food.Id}", new Vector2(btnW, 24f)))
                Plugin.Sim.BuyAndFeed(food);
            ImGui.EndDisabled();

            ImGui.TextColored(Theme.TextMuted,
                $"+{food.HungerRestore:F0}% {L.T("Hunger", "Fome")}  •  +{food.HappinessRestore:F0}% {L.T("Happy", "Feliz")}  •  +{food.EnergyRestore:F0}% {L.T("Energy", "Energia")}  •  +{food.XpReward} XP");
            ImGui.EndChild();
        }
    }

    private void DrawShopPotionsSubTab()
    {
        var pet = Plugin.Config.Pet;
        UiControls.TextWrappedColored(Theme.TextMuted, L.T(
            "In-game activities (Duties, Raids, FATEs, Crafting & Gathering) drain your pet's Energy. Drink Energy Potions for instant recovery or let your pet nap!",
            "Atividades no jogo (Instâncias, Raids, FATEs, Craft e Coleta) consomem a Energia do pet. Use Poções de Energia para recuperar na hora ou deixe ele tirar uma soneca!"));
        ImGui.Spacing();

        foreach (var pot in EnergyPotionCatalog.Menu)
        {
            ImGui.BeginChild($"##ShopPot_{pot.Id}", new Vector2(0, 58f), true, ImGuiWindowFlags.NoScrollbar);
            ImGui.TextColored(Theme.CyanAccent, L.IsPt ? pot.NamePt : pot.NameEn);
            var btnW = 130f;
            var rx = ImGui.GetContentRegionAvail().X - btnW;
            if (rx > 0) ImGui.SameLine(ImGui.GetCursorPosX() + rx);
            else ImGui.SameLine();

            ImGui.BeginDisabled(pet.Coins < pot.CostCoins || !pet.IsHatched || pet.IsHibernating);
            if (ImGui.Button($"{pot.CostCoins} {L.T("Coins", "Moedas")}##bp_{pot.Id}", new Vector2(btnW, 24f)))
                Plugin.Sim.BuyAndDrinkPotion(pot);
            ImGui.EndDisabled();

            ImGui.TextColored(Theme.TextMuted,
                $"+{pot.EnergyRestore:F0}% {L.T("Energy", "Energia")}  •  +{pot.HappinessRestore:F0}% {L.T("Happy", "Feliz")}  •  +{pot.XpReward} XP");
            ImGui.EndChild();
        }
    }

    private void DrawShopHatsSubTab()
    {
        var pet = Plugin.Config.Pet;
        foreach (var h in CosmeticCatalog.Hats)
        {
            if (h.Hat == PetAccessory.None) continue;
            var owned = pet.UnlockedHats.Contains(h.Hat);
            var meetsLv = pet.Level >= h.RequiredLevel;
            var canBuy = !owned && meetsLv && pet.Coins >= h.CostCoins;

            ImGui.BeginChild($"##ShopHat_{h.Hat}", new Vector2(0, 44f), true, ImGuiWindowFlags.NoScrollbar);
            ImGui.TextColored(meetsLv ? Theme.TextWhite : Theme.TextMuted, $"[Lv.{h.RequiredLevel}] {(L.IsPt ? h.NamePt : h.NameEn)}");

            var btnW = 122f;
            var prevW = 68f;
            var rx = ImGui.GetContentRegionAvail().X - (btnW + prevW + 10f);
            if (rx > 0) ImGui.SameLine(ImGui.GetCursorPosX() + rx);
            else ImGui.SameLine();

            if (ImGui.SmallButton($"{L.T("Try", "Ver")}##ph_{h.Hat}"))
                shopPreviewHat = h.Hat;
            ImGui.SameLine();

            if (owned)
            {
                ImGui.TextColored(Theme.GreenAccent, L.T("Owned ✓", "Comprado ✓"));
            }
            else
            {
                ImGui.BeginDisabled(!canBuy);
                var label = meetsLv ? $"{h.CostCoins} {L.T("Coins", "Moedas")}" : $"Lv.{h.RequiredLevel} • {h.CostCoins}c";
                if (ImGui.Button($"{label}##bh_{h.Hat}", new Vector2(btnW, 24f)))
                {
                    Plugin.Sim.BuyOrEquipHat(h.Hat);
                    shopPreviewHat = h.Hat;
                }
                ImGui.EndDisabled();
            }
            ImGui.EndChild();
        }
    }

    private void DrawShopOutfitsSubTab()
    {
        var pet = Plugin.Config.Pet;
        foreach (var o in CosmeticCatalog.Outfits)
        {
            if (o.Outfit == PetOutfit.None) continue;
            var owned = pet.UnlockedOutfits.Contains(o.Outfit);
            var meetsLv = pet.Level >= o.RequiredLevel;
            var canBuy = !owned && meetsLv && pet.Coins >= o.CostCoins;

            ImGui.BeginChild($"##ShopOut_{o.Outfit}", new Vector2(0, 44f), true, ImGuiWindowFlags.NoScrollbar);
            var titleCol = o.RequiredLevel >= 80 ? Theme.YellowMain : (meetsLv ? Theme.TextWhite : Theme.TextMuted);
            ImGui.TextColored(titleCol, $"[Lv.{o.RequiredLevel}] {(L.IsPt ? o.NamePt : o.NameEn)}");

            var btnW = 122f;
            var prevW = 68f;
            var rx = ImGui.GetContentRegionAvail().X - (btnW + prevW + 10f);
            if (rx > 0) ImGui.SameLine(ImGui.GetCursorPosX() + rx);
            else ImGui.SameLine();

            if (ImGui.SmallButton($"{L.T("Try", "Ver")}##po_{o.Outfit}"))
                shopPreviewOutfit = o.Outfit;
            ImGui.SameLine();

            if (owned)
            {
                ImGui.TextColored(Theme.GreenAccent, L.T("Owned ✓", "Comprado ✓"));
            }
            else
            {
                ImGui.BeginDisabled(!canBuy);
                var label = meetsLv ? $"{o.CostCoins} {L.T("Coins", "Moedas")}" : $"Lv.{o.RequiredLevel} • {o.CostCoins}c";
                if (ImGui.Button($"{label}##bo_{o.Outfit}", new Vector2(btnW, 24f)))
                {
                    Plugin.Sim.BuyOrEquipOutfit(o.Outfit);
                    shopPreviewOutfit = o.Outfit;
                }
                ImGui.EndDisabled();
            }
            ImGui.EndChild();
        }
    }

    private void DrawShopAurasSubTab()
    {
        var pet = Plugin.Config.Pet;
        foreach (var a in WeaponSkinCatalog.All)
        {
            if (a.Skin == WeaponSkin.Default) continue;
            var owned = pet.UnlockedWeaponSkins.Contains(a.Skin);
            var meetsLv = pet.Level >= a.RequiredLevel;
            var canBuy = !owned && meetsLv && pet.Coins >= a.CostCoins;

            ImGui.BeginChild($"##ShopAura_{a.ShortTag}", new Vector2(0, 60f), true, ImGuiWindowFlags.NoScrollbar);
            ImGui.TextColored(a.GlowPrimary, $"[Lv.{a.RequiredLevel} • {a.ShortTag}]");
            ImGui.SameLine();
            ImGui.TextColored(Theme.TextWhite, L.IsPt ? a.NamePt : a.NameEn);

            var btnW = 122f;
            var prevW = 68f;
            var rx = ImGui.GetContentRegionAvail().X - (btnW + prevW + 10f);
            if (rx > 0) ImGui.SameLine(ImGui.GetCursorPosX() + rx);
            else ImGui.SameLine();

            if (ImGui.SmallButton($"{L.T("Try", "Ver")}##pa_{a.ShortTag}"))
            {
                shopPreviewWeaponSkin = a.Skin;
                shopCombatPose = true;
            }
            ImGui.SameLine();

            if (owned)
            {
                ImGui.TextColored(Theme.GreenAccent, L.T("Owned ✓", "Comprado ✓"));
            }
            else
            {
                ImGui.BeginDisabled(!canBuy);
                var label = meetsLv ? $"{a.CostCoins} {L.T("Coins", "Moedas")}" : $"Lv.{a.RequiredLevel} • {a.CostCoins}c";
                if (ImGui.Button($"{label}##ba_{a.ShortTag}", new Vector2(btnW, 24f)))
                {
                    Plugin.Sim.BuyOrEquipWeaponSkin(a.Skin);
                    shopPreviewWeaponSkin = a.Skin;
                }
                ImGui.EndDisabled();
            }

            ImGui.TextColored(Theme.TextMuted, L.IsPt ? a.ThemePt : a.ThemeEn);
            ImGui.EndChild();
        }
    }

    private void DrawShopDyeSubTab()
    {
        var pet = Plugin.Config.Pet;
        ImGui.TextColored(Theme.YellowMain, L.T("Custom Color Dye Palette", "Kit de Cores Personalizadas"));
        ImGui.TextWrapped(L.T(
            "Unlock full RGB color customization in your Wardrobe! Recolor your pet's primary body, secondary parts (ears, tail, stripes, wings, scarf), and details (blush, forehead gem, Moogle pom-pom).",
            "Desbloqueie customização RGB completa no seu Guarda-Roupa! Mude a cor principal do corpo, partes secundárias (orelhas, cauda, listras, asas, cachecol) e detalhes (bochechas, gema, pom-pom)."));
        ImGui.Spacing();

        if (pet.UnlockedCustomColors)
        {
            ImGui.TextColored(Theme.GreenAccent, L.T("Owned ✓ — Go to the 'Wardrobe' tab to customize colors!", "Comprado ✓ — Vá na aba 'Guarda-Roupa' para mudar as cores!"));
        }
        else
        {
            var meetsLv = pet.Level >= CosmeticCatalog.DyeKitRequiredLevel;
            var canBuy = meetsLv && pet.Coins >= CosmeticCatalog.DyeKitCostCoins;
            ImGui.BeginDisabled(!canBuy);
            var label = meetsLv
                ? L.T($"Unlock Color Dye Kit ({CosmeticCatalog.DyeKitCostCoins} Coins)", $"Desbloquear Kit de Cores ({CosmeticCatalog.DyeKitCostCoins} Moedas)")
                : L.T($"Requires Lv.{CosmeticCatalog.DyeKitRequiredLevel} ({CosmeticCatalog.DyeKitCostCoins} Coins)", $"Requer Lv.{CosmeticCatalog.DyeKitRequiredLevel} ({CosmeticCatalog.DyeKitCostCoins} Moedas)");
            if (ImGui.Button(label, new Vector2(280f, 34f)))
            {
                Plugin.Sim.BuyCustomColorKit();
            }
            ImGui.EndDisabled();
        }
    }

    private void DrawRewardsAndStatsTab()
    {
        var pet = Plugin.Config.Pet;
        var sim = Plugin.Sim;
        var avail = ImGui.GetContentRegionAvail();
        var leftW = avail.X * 0.56f;

        // Left: All Content Rewards Schedule (Duties + Open World / Lifestyle)
        ImGui.BeginChild("##RewardTablePane", new Vector2(leftW, avail.Y), true);
        ImGui.TextColored(Theme.YellowMain, L.T("All FFXIV Content Rewards", "Recompensas de Todo Conteúdo FFXIV"));
        UiControls.TextWrappedColored(Theme.TextMuted, L.T(
            "Full XP up to 2,500/day -> 35% (4,500) -> 10%  •  Duty consolation >= 2 min.",
            "XP 100% até 2.500/dia -> 35% (4.500) -> 10%  •  Consolação >= 2 min."));
        ImGui.Spacing();

        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;

        ImGui.TextColored(Theme.CyanAccent, L.T("Open-World, Roulettes, Field Ops & Lifestyle", "Mundo Aberto, Roletas, Campo & Lifestyle"));
        if (ImGui.BeginTable("##OpenWorldRewardsTable", 3, flags))
        {
            ImGui.TableSetupColumn(L.T("Activity", "Atividade"), ImGuiTableColumnFlags.WidthStretch, 2.0f);
            ImGui.TableSetupColumn(L.T("Coins", "Moedas"), ImGuiTableColumnFlags.WidthStretch, 0.7f);
            ImGui.TableSetupColumn("XP", ImGuiTableColumnFlags.WidthStretch, 0.7f);
            ImGui.TableHeadersRow();

            (string En, string Pt, int Coins, int Xp, bool Highlight)[] openRows =
            [
                ("Daily Duty Roulette Bonus",               "Bônus de Roleta Diária",                 72, 120, true),
                ("CE / Eureka NM / Occult Crescent CE",     "CE (Bozja/OC) / NM (Eureka)",            78,  95, true),
                ("Hunt Marks (Seals / Nuts)",               "Hunts (Seals / Nuts)",                   42,  50, false),
                ("Field Op FATE (Bozja / Eureka / OC)",     "FATE de Campo (Bozja / Eureka / OC)",    36,  45, false),
                ("Cosmic Exploration Mission",              "Missão Exploração Cósmica",              36,  45, false),
                ("Gold Saucer GATE",                        "Gold Saucer GATE",                       36,  45, false),
                ("BLU Spell Learned / BST Beast Tamed",     "Magia BLU / Fera Domada (BST)",          36,  45, false),
                ("Quest / Leve / Tribal Quest",             "Quest / Leve / Tribo",                   30,  40, false),
                ("Normal FATE",                             "FATE Normal",                            26,  35, false),
                ("Island Sanctuary",                        "Ilha Santuário",                         18,  22, false),
                ("Crafting (DoH, 25s cd)",                  "Craft (DoH, 25s cd)",                    12,  15, false),
                ("Gathering / Fishing (DoL, 25s cd)",       "Coleta / Pesca (DoL, 25s cd)",           12,  15, false),
                ("Online Bonus (every 10 min)",             "Bônus Online (a cada 10 min)",           12,  15, false),
                ("Open-World Combat (20s cd)",              "Combate Mundo Aberto (20s cd)",           3,   5, false),
            ];

            foreach (var r in openRows)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextColored(r.Highlight ? Theme.YellowMain : Theme.TextWhite, L.T(r.En, r.Pt));
                ImGui.TableNextColumn(); ImGui.TextColored(Theme.GreenAccent, $"+{r.Coins}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"+{r.Xp}");
            }
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.TextColored(Theme.CyanAccent, L.T("Instanced Duties (All Types)", "Instâncias & Duties (Todos os Tipos)"));
        if (ImGui.BeginTable("##CoinRewardsTable", 4, flags))
        {
            ImGui.TableSetupColumn(L.T("Duty Type", "Tipo de Instância"), ImGuiTableColumnFlags.WidthStretch, 1.7f);
            ImGui.TableSetupColumn(L.T("Clear", "Clear"), ImGuiTableColumnFlags.WidthStretch, 0.7f);
            ImGui.TableSetupColumn(L.T("Consol.", "Consol."), ImGuiTableColumnFlags.WidthStretch, 0.7f);
            ImGui.TableSetupColumn("XP", ImGuiTableColumnFlags.WidthStretch, 0.6f);
            ImGui.TableHeadersRow();

            DutyCategory[] order =
            [
                DutyCategory.Guildhest,
                DutyCategory.GoldSaucerDuty,
                DutyCategory.NormalTrial,
                DutyCategory.MaskedCarnivale,
                DutyCategory.BeastmasterCrucible,
                DutyCategory.NormalRaid,
                DutyCategory.PvP,
                DutyCategory.OceanFishing,
                DutyCategory.Dungeon,
                DutyCategory.TreasureDungeon,
                DutyCategory.DeepDungeon,
                DutyCategory.AllianceRaid,
                DutyCategory.ExtremeTrial,
                DutyCategory.FieldRaid,
                DutyCategory.ChaoticOrCriterion,
                DutyCategory.SavageRaid,
                DutyCategory.UltimateRaid,
                DutyCategory.OtherDuty,
            ];

            foreach (var cat in order)
            {
                var rule = DutyContentClassifier.Rules[cat];
                var isHigh = cat is DutyCategory.ExtremeTrial or DutyCategory.ChaoticOrCriterion or DutyCategory.SavageRaid or DutyCategory.UltimateRaid;
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextColored(isHigh ? Theme.YellowMain : Theme.TextWhite, L.T(rule.LabelEn, rule.LabelPt));
                ImGui.TableNextColumn(); ImGui.TextColored(Theme.GreenAccent, $"+{rule.ClearCoins}");
                ImGui.TableNextColumn(); ImGui.TextColored(Theme.TextMuted, $"+{rule.ConsolationCoins}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"+{rule.ClearXp}");
            }
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(Theme.YellowMain, L.T("Lifetime Stats", "Estatísticas Gerais"));
        ImGui.TextWrapped($"{L.T("Coins Earned / Spent", "Moedas Ganhas / Gastas")}: {pet.TotalCoinsEarned:N0} / {pet.TotalCoinsSpent:N0}");
        ImGui.TextWrapped($"{L.T("Duties (High-End) / Roulettes / PvP", "Instâncias (High-End) / Roletas / PvP")}: {pet.DutiesCelebrated} ({pet.HighEndClears}) / {pet.RoulettesCompleted} / {pet.PvpMatches}");
        ImGui.TextWrapped($"{L.T("FATEs & CEs / Crafts / Gathers", "FATEs & CEs / Crafts / Coletas")}: {pet.FatesCompleted} / {pet.CraftsRewarded} / {pet.GathersRewarded}");
        ImGui.TextWrapped($"{L.T("Combats / Deaths / Meals / Headpats", "Combates / Mortes / Comidas / Carinhos")}: {pet.CombatsCheered} / {pet.DeathsMourned} / {pet.SnacksEaten} / {pet.PetsReceived}");
        ImGui.EndChild();

        ImGui.SameLine(0, 12f);

        // Right: Live Activity Log
        ImGui.BeginChild("##ActivityLogPane", new Vector2(avail.X - leftW - 12f, avail.Y), true);
        ImGui.TextColored(Theme.YellowMain, L.T("Recent Activity & Rewards Log", "Histórico de Atividades e Recompensas"));
        if (sim.ActiveDuty is { } duty)
        {
            ImGui.TextColored(Theme.CyanAccent, $"{L.T("Currently in", "Agora em")}: {duty.DutyName} [{L.T(duty.Rule.LabelEn, duty.Rule.LabelPt)}]");
        }
        else if (sim.CurrentFieldZone != FieldZoneKind.None)
        {
            ImGui.TextColored(Theme.CyanAccent, $"{L.T("Field Zone", "Zona Especial")}: {PetSimulationService.FieldZoneLabel(sim.CurrentFieldZone)}");
        }
        ImGui.Separator();

        ImGui.BeginChild("##PetLogScroll", new Vector2(0, ImGui.GetContentRegionAvail().Y), false);
        foreach (var entry in sim.ActivityLog)
        {
            ImGui.TextColored(Theme.TextMuted, $"[{entry.TimeLocal:HH:mm}]");
            ImGui.SameLine();
            ImGui.TextWrapped(entry.Message);
        }
        ImGui.EndChild();
        ImGui.EndChild();
    }

    private void DrawNurseryTab()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.BeginChild("##NurseryScroll", ImGui.GetContentRegionAvail(), false);
        ImGui.PopStyleColor();
        ImGui.Dummy(new Vector2(0, 4f));
        ImGui.TextColored(Theme.YellowMain, L.T("Choose & Hatch a New Companion Egg!", "Escolha e Choque um Novo Ovo!"));
        ImGui.TextWrapped(L.T(
            "Pick any companion below. Your new friend starts as an animated Egg that warms up as you play (or tap the egg to hatch it faster!).",
            "Escolha qualquer mascote abaixo. Seu novo amigo começa como um Ovo animado que aquece enquanto você joga (ou clique no ovo para chocar mais rápido!)."));
        ImGui.Spacing();

        var isCreator = Plugin.Sim.IsCreatorCharacter();
        var species = Enum.GetValues<PetSpecies>()
            .Where(sp => sp != PetSpecies.FluffyKitty || isCreator)
            .ToArray();
        var availW = ImGui.GetContentRegionAvail().X;
        var cols = availW >= 740f ? species.Length : Math.Min(3, species.Length);
        var cardW = (availW - 10f * (cols - 1)) / cols;

        for (var i = 0; i < species.Length; i++)
        {
            var sp = species[i];
            if (i % cols != 0) ImGui.SameLine(0, 10f);

            var isSel = selectedAdoptSpecies == (int)sp;
            if (isSel) ImGui.PushStyleColor(ImGuiCol.Border, Theme.YellowMain);
            ImGui.BeginChild($"##AdoptCard{i}", new Vector2(cardW, 196f), true, ImGuiWindowFlags.NoScrollbar);

            var previewPet = new PetStateData { Species = sp, IsHatched = true, Level = 20 };
            if (PetCanvasRenderer.DrawPetCanvas($"##Prev{i}", previewPet, isSel ? PetMood.Happy : PetMood.Idle, Plugin.Sim.AnimTimeSec, 0f, MathF.Min(114f, cardW - 20f)))
            {
                selectedAdoptSpecies = (int)sp;
                adoptNameInput = PetStateData.DefaultNameFor(sp);
            }

            ImGui.TextColored(isSel ? Theme.YellowMain : Theme.TextWhite, L.SpeciesName(sp));
            if (ImGui.Button(isSel ? L.T("Selected ✓", "Selecionado ✓") : L.T("Select", "Selecionar"), new Vector2(-1, 24f)))
            {
                selectedAdoptSpecies = (int)sp;
                adoptNameInput = PetStateData.DefaultNameFor(sp);
            }

            ImGui.EndChild();
            if (isSel) ImGui.PopStyleColor();
        }

        ImGui.Spacing();
        using (UiCard.Begin(L.T("Adoption Options", "Opções de Adoção")))
        {
            ImGui.SetNextItemWidth(MathF.Min(260f, UiCard.InnerWidth));
            ImGui.InputText(L.T("Companion Name", "Nome do Mascote"), ref adoptNameInput, 28);
            ImGui.Spacing();
            UiControls.SettingToggleRow("adopt_keep",
                L.T("Keep current Level & unlocked cosmetics", "Manter Nível atual e cosméticos desbloqueados"),
                ref adoptKeepLevel);

            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain);
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark);
            if (ImGui.Button(L.T("Adopt & Start Hatching Egg!", "Adotar e Começar a Chocar Ovo!"), new Vector2(MathF.Min(280f, UiCard.InnerWidth), 34f)))
            {
                Plugin.Sim.AdoptNewEgg((PetSpecies)selectedAdoptSpecies, adoptNameInput, adoptKeepLevel);
            }
            ImGui.PopStyleColor(2);
        }
        ImGui.EndChild();
    }

    private void DrawSettingsTab()
    {
        var cfg = Plugin.Config;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.BeginChild("##SettingsScroll", ImGui.GetContentRegionAvail(), false);
        ImGui.PopStyleColor();
        ImGui.Dummy(new Vector2(0, 4f));

        var hudCardMin = ImGui.GetCursorScreenPos();
        using (UiCard.Begin(L.T("Floating Pet HUD", "HUD Flutuante do Pet")))
        {
            var showHud = cfg.ShowHud;
            if (UiControls.SettingToggleRow("show_hud",
                L.T("Show floating Pet HUD", "Mostrar HUD flutuante do Pet"), ref showHud,
                L.T("Display your animated companion directly over the game screen.", "Exibe seu mascote animado diretamente sobre a tela do jogo.")))
            { cfg.ShowHud = showHud; cfg.Save(); }

            var transparent = cfg.TransparentHud;
            if (UiControls.SettingToggleRow("transparent_hud",
                L.T("Transparent / borderless HUD background", "Fundo transparente / sem borda"), ref transparent,
                L.T("Hides the HUD window frame so only your pet floats freely.", "Esconde a moldura da janela para apenas o pet flutuar livremente.")))
            { cfg.TransparentHud = transparent; cfg.Save(); }

            var lockPos = cfg.LockHudPosition;
            if (UiControls.SettingToggleRow("lock_hud",
                L.T("Lock HUD position", "Travar posição do HUD"), ref lockPos,
                L.T("Prevents accidental dragging while playing.", "Evita arrastar o pet sem querer enquanto joga.")))
            { cfg.LockHudPosition = lockPos; cfg.Save(); }

            var hideCutscene = cfg.HideHudInCutscene;
            if (UiControls.SettingToggleRow("hide_cutscene",
                L.T("Auto-hide HUD during cutscenes", "Esconder HUD durante cutscenes"), ref hideCutscene,
                L.T("Keeps story scenes clean and unobstructed.", "Mantém cenas da história limpas e sem obstruções.")))
            { cfg.HideHudInCutscene = hideCutscene; cfg.Save(); }

            var scale = cfg.HudScale;
            if (UiControls.SettingSliderRow("hud_scale",
                L.T("HUD Scale", "Tamanho do HUD"), ref scale, 0.75f, 1.60f, "%.2fx",
                L.T("Adjusts the size of your floating companion on screen.", "Ajusta o tamanho do seu mascote flutuante na tela.")))
            { cfg.HudScale = scale; cfg.Save(); }
        }
        var hudCardMax = new Vector2(hudCardMin.X + ImGui.GetContentRegionAvail().X, ImGui.GetCursorScreenPos().Y - 10f);
        Plugin.Tutorial.RegisterTargetRect("settings_hud_card", hudCardMin, hudCardMax);

        var interCardMin = ImGui.GetCursorScreenPos();
        using (UiCard.Begin(L.T("Companion Display & Interactions", "Exibição e Interações do Pet")))
        {
            var quickBtns = cfg.ShowHudQuickButtons;
            if (UiControls.SettingToggleRow("quick_btns",
                L.T("Show quick care buttons under pet", "Mostrar botões rápidos abaixo do pet"), ref quickBtns,
                L.T("Adds compact Feed & Headpat shortcuts beneath the HUD.", "Adiciona atalhos compactos de Comida e Carinho abaixo do HUD.")))
            { cfg.ShowHudQuickButtons = quickBtns; cfg.Save(); }

            var bubbles = cfg.ShowSpeechBubbles;
            if (UiControls.SettingToggleRow("speech_bubbles",
                L.T("Show speech bubbles", "Mostrar balões de fala"), ref bubbles,
                L.T("Displays reactions when you clear duties, level up, or pet them.", "Mostra reações ao concluir conteúdos, subir de nível ou fazer carinho.")))
            { cfg.ShowSpeechBubbles = bubbles; cfg.Save(); }

            var showWeapon = cfg.ShowJobWeapon;
            if (UiControls.SettingToggleRow("job_weapon",
                L.T("Equip player's current Job weapon & tools", "Equipar arma da Job e ferramentas de craft/coleta"), ref showWeapon,
                L.T("Your pet carries your Job's weapon on its back and draws it in combat, crafting, or gathering!", "Seu pet carrega a arma da sua Job nas costas e saca em combate, craft ou coleta!")))
            { cfg.ShowJobWeapon = showWeapon; cfg.Save(); }
        }
        var interCardMax = new Vector2(interCardMin.X + ImGui.GetContentRegionAvail().X, ImGui.GetCursorScreenPos().Y - 10f);
        Plugin.Tutorial.RegisterTargetRect("settings_inter_card", interCardMin, interCardMax);

        using (UiCard.Begin(L.T("Chat Commands", "Comandos de Chat")))
        {
            UiControls.InsetNote(
                "/petfrenzy  |  /pet  —  " + L.T("Open or close the PetFrenzy Dashboard", "Abre ou fecha o painel do PetFrenzy") + "\n" +
                "/pet hud  —  " + L.T("Toggle the floating Pet HUD on/off", "Liga ou desliga o HUD flutuante do Pet"),
                Theme.TextLight);
        }

        ImGui.EndChild();
    }

    private float aboutKittyPettedSec;

    private void DrawAboutTab()
    {
        var avail = ImGui.GetContentRegionAvail();
        var leftW = Math.Clamp(avail.X * 0.30f, 210f, 260f);
        var canvasSz = MathF.Min(190f, leftW - 32f);
        var sim = Plugin.Sim;
        if (aboutKittyPettedSec > 0f)
            aboutKittyPettedSec = MathF.Max(0f, aboutKittyPettedSec - ImGui.GetIO().DeltaTime);

        ImGui.Dummy(new Vector2(0, 4f));
        var contentH = MathF.Max(120f, avail.Y - 6f);

        // Left: Special Mascot Tribute Card featuring Milly!
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14f, 14f));
        ImGui.BeginChild("##AboutMascotCard", new Vector2(leftW, contentH), true);

        var cardInnerW = ImGui.GetContentRegionAvail().X;
        var titleStr = L.T("The Muse of PetFrenzy ♥", "A Musa do PetFrenzy ♥");
        var subStr = L.T("Creator's real-life fluffy kitty!", "A gatinha real do criador!");
        var tSize = ImGui.CalcTextSize(titleStr);
        var sSize = ImGui.CalcTextSize(subStr);

        if (tSize.X < cardInnerW) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (cardInnerW - tSize.X) * 0.5f);
        ImGui.TextColored(Theme.YellowMain, titleStr);
        if (sSize.X < cardInnerW) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (cardInnerW - sSize.X) * 0.5f);
        ImGui.TextColored(Theme.TextMuted, subStr);

        ImGui.Dummy(new Vector2(0, 8f));
        var canvasPadX = MathF.Max(0f, (cardInnerW - canvasSz) * 0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + canvasPadX);

        var mascot = new PetStateData
        {
            Species = PetSpecies.FluffyKitty,
            Name = "Milly",
            IsHatched = true,
            Level = 100,
        };
        var mascotMood = aboutKittyPettedSec > 0f ? PetMood.Petted : PetMood.Happy;
        if (PetCanvasRenderer.DrawPetCanvas("##AboutKittyCanvas", mascot, mascotMood, sim.AnimTimeSec, 0f, canvasSz, interactive: true))
        {
            aboutKittyPettedSec = 3.2f;
        }

        ImGui.Dummy(new Vector2(0, 6f));
        var caption = aboutKittyPettedSec > 0f
            ? L.T("\"Purrrrr~ ♥ Thank you for playing!\"", "\"Ronron~ ♥ Obrigada por jogar!\"")
            : L.T("Click her for headpats! ♥", "Clique nela para fazer carinho! ♥");
        var capSize = ImGui.CalcTextSize(caption);
        if (capSize.X < cardInnerW) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (cardInnerW - capSize.X) * 0.5f);
        UiControls.TextWrappedColored(aboutKittyPettedSec > 0f ? Theme.PinkAccent : Theme.TextMuted, caption);

        ImGui.Dummy(new Vector2(0, 12f));
        ImGui.Separator();
        ImGui.Dummy(new Vector2(0, 10f));

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.YellowMain);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark);
        if (ImGui.Button(L.T("★  Replay Tutorial", "★  Rever Tutorial"), new Vector2(-1, 32f)))
        {
            Plugin.Tutorial.Start();
        }
        ImGui.PopStyleColor(2);
        ImGui.EndChild();
        ImGui.PopStyleVar();

        ImGui.SameLine(0, 14f);

        // Right: Transparent scroll area so each FateFrenzy-style UiCard floats cleanly with contrast!
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.BeginChild("##AboutInfoScroll", new Vector2(avail.X - leftW - 14f, contentH), false);
        ImGui.PopStyleColor();

        using (UiCard.Begin(L.T("About PetFrenzy", "Sobre o PetFrenzy")))
        {
            ImGui.TextColored(Theme.TextWhite, "PetFrenzy — Eorzea Tamagotchi");
            ImGui.SameLine();
            ImGui.TextColored(Theme.YellowMain, "v1.0.0.2");
            ImGui.TextColored(Theme.TextMuted, L.T("Created with love by n0way02", "Criado com carinho por n0way02"));
            ImGui.Dummy(new Vector2(0, 6f));

            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + UiCard.InnerWidth);
            ImGui.TextUnformatted(L.T(
                "PetFrenzy brings a lively, animated Tamagotchi companion right onto your FFXIV screen! Hatch your pet, feed it, equip it with your current Job's weapon or crafting/gathering tool, and earn Pet Coins & XP from virtually every activity in Eorzea — from Roulettes, Dungeons, Trials, Raids and PvP to FATEs, Bozja, Eureka, Occult Crescent, Cosmic Exploration, Crafting and Gathering.",
                "O PetFrenzy traz um mascote Tamagotchi animado direto para a sua tela do FFXIV! Choque seu ovo, alimente-o, veja ele empunhando a arma da sua Job ou ferramenta de craft/coleta e ganhe Moedas e XP em praticamente todo conteúdo de Eorzea — de Roletas, Dungeons, Trials, Raids e PvP até FATEs, Bozja, Eureka, Occult Crescent, Exploração Cósmica, Craft e Coleta."));
            ImGui.PopTextWrapPos();
        }

        using (UiCard.Begin(L.T("Thank You! ♥", "Muito Obrigado! ♥")))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + UiCard.InnerWidth);
            ImGui.TextColored(Theme.TextLight, L.T(
                "Thank you so much for using PetFrenzy and taking your little companion along on your adventures across Eorzea! If you enjoy my plugins and want to support future updates and new ideas, feel free to check out my website or buy me a coffee on Ko-fi.",
                "Muito obrigado por usar o PetFrenzy e levar seu mascote nas suas aventuras por Eorzea! Se você curte os meus plugins e quiser apoiar novas atualizações e projetos futuros, visite o meu site ou pague um café no Ko-fi."));
            ImGui.PopTextWrapPos();
        }

        using (UiCard.Begin(L.T("Official Links (Right-click to copy)", "Links Oficiais (Botão direito para copiar)")))
        {
            var innerW = UiCard.InnerWidth;
            var stackBtns = innerW < 410f;
            var btnW = stackBtns ? innerW : (innerW - 16f) / 3f;

            DrawLinkButton(L.T("★  Website", "★  Site Oficial"), "https://n0wayplugins.discloud.app",
                Theme.PurpleMain, Theme.PurpleBright, new Vector4(0.32f, 0.05f, 0.65f, 1.0f), btnW);
            if (!stackBtns) ImGui.SameLine(0, 8f);

            DrawLinkButton(L.T("♥  Ko-fi", "♥  Apoiar Ko-fi"), "https://ko-fi.com/n0way02",
                new Vector4(0.95f, 0.32f, 0.38f, 0.92f), new Vector4(1.00f, 0.44f, 0.49f, 1.00f), new Vector4(0.80f, 0.22f, 0.28f, 1.00f), btnW);
            if (!stackBtns) ImGui.SameLine(0, 8f);

            DrawLinkButton("◆  GitHub", "https://github.com/n0way02",
                new Vector4(0.20f, 0.08f, 0.38f, 1.0f), Theme.PurpleMain, Theme.PurpleBright, btnW);
        }

        ImGui.EndChild();
    }

    private static void DrawLinkButton(string label, string url, Vector4 col, Vector4 hover, Vector4 active, float width)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, col);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);
        if (ImGui.Button(label, new Vector2(width, 32f)))
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
        ImGui.PopStyleColor(3);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"{url}\n" + L.T("(Right-click to copy)", "(Botão direito para copiar)"));
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                ImGui.SetClipboardText(url);
        }
    }

    private void DrawCreatorDevTab()
    {
        if (!Plugin.Sim.IsCreatorCharacter()) return;
        var pet = Plugin.Config.Pet;
        var sim = Plugin.Sim;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.BeginChild("##CreatorDevScroll", ImGui.GetContentRegionAvail(), false);
        ImGui.PopStyleColor();
        ImGui.Dummy(new Vector2(0, 4f));

        using (UiCard.Begin("★ Creator Sandbox / Debug Tools (Harumi Aoi Only)"))
        {
            UiControls.InsetNote(
                $"Level: {pet.Level}/100  •  XP: {pet.Xp}/{pet.XpForNextLevel}  •  Daily XP: {pet.DailyXpEarned} ({pet.CurrentXpMultiplier * 100f:F0}%)\n" +
                $"Coins: {pet.Coins:N0}  •  Hunger: {pet.Hunger:F1}%  •  Energy: {pet.Energy:F1}%  •  Happy: {pet.Happiness:F1}%\n" +
                $"Zero-Hunger Timer: {pet.ZeroHungerSeconds / 3600f:F2}h / 12.0h  •  Hibernating: {pet.IsHibernating}  •  Free Meal: {pet.HasFreeWakeupMeal}",
                Theme.YellowMain);
        }

        using (UiCard.Begin("Level, XP & Coins"))
        {
            if (ImGui.Button("+1 Lv", new Vector2(80f, 28f))) sim.DevAddLevels(1);
            ImGui.SameLine();
            if (ImGui.Button("+10 Lv", new Vector2(80f, 28f))) sim.DevAddLevels(10);
            ImGui.SameLine();
            if (ImGui.Button("Set Lv.100", new Vector2(96f, 28f))) sim.DevAddLevels(100);
            ImGui.SameLine();
            if (ImGui.Button("Reset Lv.1", new Vector2(96f, 28f))) sim.DevAddLevels(-100);

            if (ImGui.Button("+500 XP", new Vector2(90f, 28f))) sim.DevAddRawXp(500);
            ImGui.SameLine();
            if (ImGui.Button("+2,500 XP", new Vector2(96f, 28f))) sim.DevAddRawXp(2500);
            ImGui.SameLine();
            if (ImGui.Button("Reset Daily XP Cap", new Vector2(150f, 28f)))
            {
                pet.DailyXpEarned = 0;
                Plugin.Config.Save();
            }

            ImGui.Spacing();
            if (ImGui.Button("+500 Coins", new Vector2(105f, 28f))) sim.DevSetCoins(500, add: true);
            ImGui.SameLine();
            if (ImGui.Button("+5,000 Coins", new Vector2(115f, 28f))) sim.DevSetCoins(5000, add: true);
            ImGui.SameLine();
            if (ImGui.Button("+50,000 Coins", new Vector2(120f, 28f))) sim.DevSetCoins(50000, add: true);
            ImGui.SameLine();
            if (ImGui.Button("Set 0 Coins", new Vector2(100f, 28f))) sim.DevSetCoins(0, add: false);
        }

        using (UiCard.Begin("Needs, Time Skip & Hibernation"))
        {
            if (ImGui.Button("Hunger = 0%", new Vector2(110f, 28f))) { pet.Hunger = 0f; Plugin.Config.Save(); }
            ImGui.SameLine();
            if (ImGui.Button("Hunger = 100%", new Vector2(120f, 28f))) { pet.Hunger = 100f; pet.ZeroHungerSeconds = 0f; Plugin.Config.Save(); }
            ImGui.SameLine();
            if (ImGui.Button("Energy = 0%", new Vector2(110f, 28f))) { pet.Energy = 0f; Plugin.Config.Save(); }
            ImGui.SameLine();
            if (ImGui.Button("Energy = 100%", new Vector2(120f, 28f))) { pet.Energy = 100f; Plugin.Config.Save(); }

            if (ImGui.Button("Happy = 10%", new Vector2(110f, 28f))) { pet.Happiness = 10f; Plugin.Config.Save(); }
            ImGui.SameLine();
            if (ImGui.Button("Happy = 100%", new Vector2(120f, 28f))) { pet.Happiness = 100f; Plugin.Config.Save(); }

            ImGui.Spacing();
            if (ImGui.Button("Skip +8h Offline", new Vector2(130f, 28f))) sim.DevSimulateOfflineHours(8f);
            ImGui.SameLine();
            if (ImGui.Button("Skip +12h Offline", new Vector2(135f, 28f))) sim.DevSimulateOfflineHours(12f);
            ImGui.SameLine();
            if (ImGui.Button("Skip +16h Offline", new Vector2(135f, 28f))) sim.DevSimulateOfflineHours(16f);
            ImGui.SameLine();
            if (ImGui.Button("Skip +28h (Hibernate)", new Vector2(165f, 28f))) sim.DevSimulateOfflineHours(28f);

            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.OrangeAccent);
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark);
            if (ImGui.Button("Force Hibernation Now", new Vector2(175f, 28f))) sim.DevForceHibernation();
            ImGui.PopStyleColor(2);
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.GreenAccent);
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.BgDark);
            if (ImGui.Button("Force Wake Up & Full", new Vector2(175f, 28f))) sim.DevForceWakeUp();
            ImGui.PopStyleColor(2);
        }

        using (UiCard.Begin("Cosmetics, Egg & Tutorial"))
        {
            if (ImGui.Button("Unlock All Cosmetics", new Vector2(165f, 28f))) sim.DevUnlockAllCosmetics();
            ImGui.SameLine();
            if (ImGui.Button("Lock All Cosmetics", new Vector2(155f, 28f))) sim.DevResetAllCosmetics();

            if (ImGui.Button("Turn Into Egg (0%)", new Vector2(165f, 28f)))
            {
                pet.IsHatched = false;
                pet.EggProgress = 0f;
                Plugin.Config.Save();
            }
            ImGui.SameLine();
            if (ImGui.Button("Hatch Instantly", new Vector2(155f, 28f)))
            {
                pet.IsHatched = true;
                pet.EggProgress = 100f;
                Plugin.Config.Save();
            }
            ImGui.SameLine();
            if (ImGui.Button("Reset & Run Tutorial", new Vector2(165f, 28f)))
            {
                Plugin.Config.TutorialCompleted = false;
                Plugin.Config.Save();
                Plugin.Tutorial.Start();
            }
        }

        ImGui.EndChild();
    }

    private static void DrawStatBar(string label, float val, Vector4 col)
    {
        ImGui.TextColored(Theme.TextMuted, $"{label}: {val:F0}%");
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, col);
        ImGui.ProgressBar(Math.Clamp(val / 100f, 0f, 1f), new Vector2(-1, 13f), string.Empty);
        ImGui.PopStyleColor();
    }
}
