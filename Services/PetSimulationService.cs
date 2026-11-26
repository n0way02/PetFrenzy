using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.DutyState;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using PetFrenzy.Models;

namespace PetFrenzy.Services;

public sealed class PetActivityLogItem
{
    public DateTime TimeLocal { get; init; } = DateTime.Now;
    public string Message { get; init; } = string.Empty;
}

public sealed unsafe class PetSimulationService : IDisposable
{
    private const float AfkSleepThresholdSec = 900f; // 15 minutes AFK before auto-napping
    private const long SaveIntervalMs = 30_000;

    // 100% Hunger drains completely in 16 hours (100 / 57600 per second)
    private const float HungerDecayPerSec = 100f / 57600f;
    private const float HibernationThresholdSec = 43200f;             // 12 hours at 0% Hunger -> Hibernation
    private const float HappinessDecayPerSec = 48f / 86400f;          // ~48% per 24h normally
    private const float StarvingHappinessDecayPerSec = 108f / 86400f; // faster when Hunger == 0%
    private const float EnergyDecayPerSec = 24f / 86400f;             // gentle passive drain (activities drain most Energy!)

    // Online reward: +12 Coins every 10 minutes (600s) logged in (+20% economy boost)
    public const float OnlineRewardIntervalSec = 600f;
    public const int OnlineRewardCoins = 12;

    // Minimum time in a duty (2 min) to qualify for consolation coins on wipe/abandon
    private const long MinConsolationTimeMs = 120_000;

    private readonly List<PetActivityLogItem> activityLog = new();
    private readonly Random rng = new();

    private long lastTickMs;
    private long lastSaveMs;
    private long lastGilCheckMs;

    // Anti-spam / deduplication cooldown timestamps (ms)
    private long lastCraftRewardMs;
    private long lastGatherRewardMs;
    private long lastFateRewardMs;
    private long lastCeRewardMs;
    private long lastRouletteRewardMs;
    private long lastQuestRewardMs;
    private long lastHuntRewardMs;
    private long lastGateRewardMs;
    private long lastSanctuaryRewardMs;
    private long lastCombatRewardMs;
    private long lastLimitedJobRewardMs;
    private long lastHeadpatXpMs;

    // Live mood & speech bubble state
    public PetMood CurrentMood { get; private set; } = PetMood.Idle;
    public string SpeechBubble { get; private set; } = string.Empty;
    public float SpeechTimerSec { get; private set; }
    public float ReactionTimerSec { get; private set; }
    public PetMood ForcedReactionMood { get; private set; } = PetMood.Idle;
    public float HatchAnimTimerSec { get; private set; }
    public float WobblePulseSec { get; private set; }
    public bool ManualNap { get; private set; }
    public float AnimTimeSec { get; private set; }

    // Live Duty & Field Operation tracking state
    public DetectedDutyInfo? ActiveDuty { get; private set; }
    public FieldZoneKind CurrentFieldZone { get; private set; } = FieldZoneKind.None;
    private long activeDutyEnteredAtMs;
    private bool activeDutyCleared;
    private bool activeDutyRewarded;

    // Live FATE, Craft & Gather transition tracking
    private ushort activeFateId;
    private byte activeFateMaxProgress;
    private bool wasCrafting;
    private long craftStartedMs;
    private bool wasGathering;
    private long gatherStartedMs;

    // Live Player Job & Weapon state
    public JobVisualInfo CurrentJob { get; private set; } = JobWeaponCatalog.Resolve(0);
    public bool IsWeaponDrawn { get; private set; }

    // Game state trackers
    private Vector3 lastPlayerPos;
    private bool hasLastPos;
    private float stationarySec;
    private bool wasInCombat;
    private bool wasDead;
    private long lastKnownGil = -1;
    private ulong lastContentId;
    private bool dirty;

    public IReadOnlyList<PetActivityLogItem> ActivityLog => activityLog;

    public float SecondsUntilNextOnlineCoins =>
        MathF.Max(0f, OnlineRewardIntervalSec - Plugin.Config.Pet.OnlineRewardSeconds);

    public PetSimulationService()
    {
        Plugin.Config.Pet.EnsureCollections();
        Apply24hOfflineDecay();
        lastTickMs = Environment.TickCount64;
        lastSaveMs = lastTickMs;

        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.ClientState.Logout += OnLogout;
        Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
        Plugin.DutyState.DutyStarted += OnDutyStarted;
        Plugin.DutyState.DutyCompleted += OnDutyCompleted;
        Plugin.ChatGui.ChatMessage += OnChatMessage;

        // If plugin loaded while already inside a duty or field operation, detect it immediately
        if (Plugin.ClientState.IsLoggedIn)
        {
            CurrentFieldZone = DutyContentClassifier.ClassifyFieldZone(Plugin.ClientState.TerritoryType);
            if (Plugin.Condition[ConditionFlag.BoundByDuty])
                TryEnterDutyTracking(Plugin.ClientState.TerritoryType);
        }

        if (!Plugin.Config.Pet.IsHatched)
        {
            Say(L.T("Tap the egg to keep it warm!", "Clique no ovo para aquecê-lo!"), 6f);
        }
        else if (Plugin.Config.Pet.IsHibernating)
        {
            Say(L.T("Zzz... Hibernating! Pet or feed me!", "Zzz... Hibernando! Faça carinho ou me alimente!"), 7f);
        }
        else if (Plugin.Config.Pet.Hunger <= 15f)
        {
            Say(L.T("I'm starving!! Feed me please!", "Tô morrendo de fome!! Me alimente!"), 6f);
        }
        else
        {
            Say(GetGreetingLine(), 5f);
        }
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        Plugin.ClientState.Logout -= OnLogout;
        Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Plugin.DutyState.DutyStarted -= OnDutyStarted;
        Plugin.DutyState.DutyCompleted -= OnDutyCompleted;
        Plugin.ChatGui.ChatMessage -= OnChatMessage;
        Plugin.Config.Pet.LastUpdatedUtc = DateTime.UtcNow;
        Plugin.Config.Save();
    }

    private void OnLogout(int type, int code)
    {
        FinalizeAbandonedDutyIfAny();
        lastKnownGil = -1;
        hasLastPos = false;
        wasInCombat = false;
        wasDead = false;
        lastContentId = 0;
    }

    private void OnTerritoryChanged(uint territoryId)
    {
        // If we left an active duty without clearing it, check for consolation reward
        if (ActiveDuty is { } duty && territoryId != duty.TerritoryId)
        {
            FinalizeAbandonedDutyIfAny();
        }

        activeFateId = 0;
        activeFateMaxProgress = 0;
        CurrentFieldZone = DutyContentClassifier.ClassifyFieldZone(territoryId);

        if (CurrentFieldZone != FieldZoneKind.None)
        {
            var zoneLabel = FieldZoneLabel(CurrentFieldZone);
            LogEvent(L.T($"Entered {zoneLabel} (Field/Lifestyle bonuses active!).",
                $"Entrou em {zoneLabel} (bônus de campo/atividade ativos!)."));
        }

        // Check if the new territory is a duty instance
        if (Plugin.Condition[ConditionFlag.BoundByDuty] || DutyContentClassifier.ClassifyTerritory(territoryId) != null)
        {
            TryEnterDutyTracking(territoryId);
        }
    }

    private void OnDutyStarted(IDutyStateEventArgs args)
    {
        var tid = args.TerritoryType.RowId != 0 ? args.TerritoryType.RowId : Plugin.ClientState.TerritoryType;
        TryEnterDutyTracking(tid);
    }

    private void OnDutyCompleted(IDutyStateEventArgs args)
    {
        var tid = args.TerritoryType.RowId != 0 ? args.TerritoryType.RowId : Plugin.ClientState.TerritoryType;
        if (ActiveDuty == null)
        {
            TryEnterDutyTracking(tid);
        }

        if (ActiveDuty is not { } duty || activeDutyRewarded) return;

        activeDutyCleared = true;
        activeDutyRewarded = true;

        var rule = duty.Rule;
        var pet = Plugin.Config.Pet;
        if (pet.IsHibernating)
        {
            Say(L.T("Zzz... Hibernating (no rewards until woken up!)", "Zzz... Hibernando (sem recompensas até acordar!)"), 5f);
            return;
        }

        pet.DutiesCelebrated++;
        if (rule.Category == DutyCategory.PvP)
            pet.PvpMatches++;
        if (rule.Category is DutyCategory.ExtremeTrial or DutyCategory.ChaoticOrCriterion or DutyCategory.SavageRaid or DutyCategory.UltimateRaid)
        {
            pet.HighEndClears++;
        }

        var energyCost = rule.Category switch
        {
            DutyCategory.UltimateRaid => 25f,
            DutyCategory.SavageRaid or DutyCategory.ChaoticOrCriterion => 20f,
            DutyCategory.ExtremeTrial or DutyCategory.FieldRaid => 16f,
            DutyCategory.AllianceRaid or DutyCategory.DeepDungeon or DutyCategory.Dungeon or DutyCategory.TreasureDungeon or DutyCategory.OceanFishing => 12f,
            _ => 8f,
        };
        ConsumeActivityEnergy(energyCost);

        AwardCoins(rule.ClearCoins, $"{duty.DutyName} ({L.T(rule.LabelEn, rule.LabelPt)})");
        pet.Happiness = MathF.Min(100f, pet.Happiness + 14f);
        var actualXp = AddXp(rule.ClearXp);

        var msg = L.T(
            $"GG!! Cleared {rule.LabelEn}! +{rule.ClearCoins} Coins!",
            $"GG!! {rule.LabelPt} concluída! +{rule.ClearCoins} Moedas!");
        TriggerReaction(PetMood.Celebrating, 5.5f, msg);

        Plugin.ChatGui.Print($"[PetFrenzy] {duty.DutyName} ({L.T(rule.LabelEn, rule.LabelPt)}) — +{rule.ClearCoins} Coins (+{actualXp} XP)!");
        MarkDirty();
    }

    private void TryEnterDutyTracking(uint territoryId)
    {
        if (ActiveDuty is { } existing && existing.TerritoryId == territoryId)
            return;

        var classified = DutyContentClassifier.ClassifyTerritory(territoryId);
        if (classified == null) return;

        ActiveDuty = classified;
        activeDutyEnteredAtMs = Environment.TickCount64;
        activeDutyCleared = false;
        activeDutyRewarded = false;

        var rule = classified.Value.Rule;
        LogEvent(L.T(
            $"Entered {classified.Value.DutyName} [{rule.LabelEn}] (Clear: +{rule.ClearCoins} Coins).",
            $"Entrou em {classified.Value.DutyName} [{rule.LabelPt}] (Clear: +{rule.ClearCoins} Moedas)."));
        Say(L.T($"Good luck in {rule.LabelEn}!!", $"Boa sorte na {rule.LabelPt}!!"), 4f);
    }

    private void FinalizeAbandonedDutyIfAny()
    {
        if (ActiveDuty is not { } duty) return;

        if (!activeDutyCleared && !activeDutyRewarded)
        {
            var elapsedMs = Environment.TickCount64 - activeDutyEnteredAtMs;
            var rule = duty.Rule;

            if (elapsedMs >= MinConsolationTimeMs && rule.ConsolationCoins > 0)
            {
                AwardCoins(rule.ConsolationCoins, L.T($"Consolation: {duty.DutyName}", $"Consolação: {duty.DutyName}"));
                AddXp(Math.Max(5, rule.ClearXp / 4));
                var msg = L.T(
                    $"Nice try in {rule.LabelEn}! +{rule.ConsolationCoins} Coins",
                    $"Boa tentativa na {rule.LabelPt}! +{rule.ConsolationCoins} Moedas");
                TriggerReaction(PetMood.Happy, 4.5f, msg);
                Plugin.ChatGui.Print($"[PetFrenzy] {L.T("Consolation reward", "Recompensa de consolação")} ({duty.DutyName}): +{rule.ConsolationCoins} Coins!");
            }
            else
            {
                LogEvent(L.T($"Left {duty.DutyName} early (< 2 min, no consolation).",
                    $"Saiu de {duty.DutyName} cedo (< 2 min, sem consolação)."));
            }
        }

        ActiveDuty = null;
        activeDutyCleared = false;
        activeDutyRewarded = false;
    }

    private void Apply24hOfflineDecay()
    {
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched) return;

        var elapsedSec = (float)(DateTime.UtcNow - pet.LastUpdatedUtc).TotalSeconds;
        if (elapsedSec <= 5f) return;

        // 100% Hunger drains in 16 hours; after 12h continuously at 0% Hunger -> Hibernation!
        var secUntilZero = pet.Hunger > 0f ? pet.Hunger / HungerDecayPerSec : 0f;
        var prevHunger = pet.Hunger;
        pet.Hunger = MathF.Max(0f, pet.Hunger - elapsedSec * HungerDecayPerSec);

        if (elapsedSec > secUntilZero)
        {
            pet.ZeroHungerSeconds += (elapsedSec - secUntilZero);
            if (pet.ZeroHungerSeconds >= HibernationThresholdSec && !pet.IsHibernating)
            {
                EnterHibernation();
            }
        }
        else if (pet.Hunger > 0f)
        {
            pet.ZeroHungerSeconds = 0f;
        }

        var happyRate = pet.Hunger <= 0f ? StarvingHappinessDecayPerSec : HappinessDecayPerSec;
        var happyDrop = MathF.Min(75f, elapsedSec * happyRate);
        pet.Happiness = MathF.Max(10f, pet.Happiness - happyDrop);

        // Resting offline recovers Energy
        pet.Energy = MathF.Min(100f, pet.Energy + elapsedSec * (100f / 28800f));
        pet.LastUpdatedUtc = DateTime.UtcNow;

        if (prevHunger - pet.Hunger >= 10f)
        {
            LogEvent(L.T($"While away, {pet.Name}'s hunger dropped to {pet.Hunger:F0}%.",
                $"Enquanto esteve fora, a fome de {pet.Name} caiu para {pet.Hunger:F0}%."));
        }
    }

    private void EnterHibernation()
    {
        var pet = Plugin.Config.Pet;
        if (pet.IsHibernating) return;
        pet.IsHibernating = true;
        pet.HasFreeWakeupMeal = true;
        LogEvent(L.T(
            $"{pet.Name} entered Hibernation (12h at 0% Hunger)! No XP/Coins until woken up — 1 Free Wake-Up Snack granted!",
            $"{pet.Name} entrou em Hibernação (12h com 0% de Fome)! Sem XP/Moedas até acordar — ganhou 1 Comida Grátis!"));
        Say(L.T("Zzz... Hibernating! Wake me up!", "Zzz... Hibernando! Me acorde!"), 6f);
        MarkDirty();
    }

    private void WakeFromHibernation(bool viaFood)
    {
        var pet = Plugin.Config.Pet;
        if (!pet.IsHibernating) return;
        pet.IsHibernating = false;
        pet.ZeroHungerSeconds = 0f;
        if (!viaFood && pet.Hunger <= 0f)
            pet.Hunger = 10f; // Small grace buffer so they don't instantly re-enter hibernation
        LogEvent(L.T($"{pet.Name} woke up from Hibernation!", $"{pet.Name} acordou da Hibernação!"));
        TriggerReaction(PetMood.Happy, 4.5f, L.T("Yaaawn~ I'm awake!! ♥", "Aaaah~ Acordei!! ♥"));
        MarkDirty();
    }

    public bool UseFreeWakeupMeal()
    {
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched || !pet.HasFreeWakeupMeal) return false;

        pet.HasFreeWakeupMeal = false;
        WakeFromHibernation(viaFood: true);
        ManualNap = false;
        pet.Hunger = MathF.Min(100f, pet.Hunger + 45f);
        pet.Happiness = MathF.Min(100f, pet.Happiness + 20f);
        pet.Energy = MathF.Min(100f, pet.Energy + 30f);
        pet.ZeroHungerSeconds = 0f;
        pet.SnacksEaten++;
        TriggerReaction(PetMood.Happy, 4.5f, L.T("Thank you for the free snack!! ♥", "Obrigado pela comida grátis!! ♥"));
        LogEvent(L.T("Claimed Free Hibernation Wake-Up Snack (+45% Hunger, +30% Energy)!",
            "Usou a Comida Grátis de Hibernação (+45% Fome, +30% Energia)!"));
        Plugin.Config.Save();
        return true;
    }

    public bool BuyAndDrinkPotion(EnergyPotionItem potion)
    {
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched) return false;
        if (pet.IsHibernating)
        {
            Say(L.T("Wake me up with headpats or food first!", "Me acorde com carinho ou comida primeiro!"), 3.5f);
            return false;
        }
        if (pet.Coins < potion.CostCoins)
        {
            Say(L.T($"Need {potion.CostCoins} Coins! (Have {pet.Coins})", $"Preciso de {potion.CostCoins} Moedas! (Tem {pet.Coins})"), 3.2f);
            return false;
        }

        pet.Coins -= potion.CostCoins;
        pet.TotalCoinsSpent += potion.CostCoins;
        ManualNap = false;
        pet.Energy = MathF.Min(100f, pet.Energy + potion.EnergyRestore);
        pet.Happiness = MathF.Min(100f, pet.Happiness + potion.HappinessRestore);
        AddXp(potion.XpReward);
        TriggerReaction(PetMood.Celebrating, 3.5f, L.T("ZAP!! Full of energy!!", "ZAP!! Cheio de energia!!"));
        LogEvent(L.T(
            $"Drank {potion.NameEn} (-{potion.CostCoins} Coins) -> +{potion.EnergyRestore:F0}% Energy.",
            $"Tomou {potion.NamePt} (-{potion.CostCoins} Moedas) -> +{potion.EnergyRestore:F0}% Energia."));
        MarkDirty();
        return true;
    }

    private void ConsumeActivityEnergy(float cost)
    {
        if (cost <= 0f) return;
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched || pet.IsHibernating) return;
        pet.Energy = MathF.Max(0f, pet.Energy - cost);
        if (pet.Energy <= 10f && SpeechTimerSec <= 0f)
        {
            Say(L.T("So tired... Energy Potion or a nap?", "Tô cansado... Poção de Energia ou soneca?"), 3.5f);
        }
    }

    public bool BuyAndFeed(FoodItem food)
    {
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched)
        {
            WarmEgg(15f);
            return true;
        }

        if (pet.Coins < food.CostCoins)
        {
            Say(L.T($"Need {food.CostCoins} Coins! (Have {pet.Coins})", $"Preciso de {food.CostCoins} Moedas! (Tem {pet.Coins})"), 3.2f);
            return false;
        }

        WakeFromHibernation(viaFood: true);
        pet.Coins -= food.CostCoins;
        pet.TotalCoinsSpent += food.CostCoins;
        ManualNap = false;
        pet.Hunger = MathF.Min(100f, pet.Hunger + food.HungerRestore);
        pet.ZeroHungerSeconds = 0f;
        pet.Happiness = MathF.Min(100f, pet.Happiness + food.HappinessRestore);
        pet.Energy = MathF.Min(100f, pet.Energy + food.EnergyRestore);
        pet.SnacksEaten++;
        AddXp(food.XpReward);
        TriggerReaction(PetMood.Happy, 3.5f, GetSnackLine());
        LogEvent(L.T(
            $"Bought {food.NameEn} (-{food.CostCoins} Coins) → +{food.HungerRestore:F0}% Hunger.",
            $"Comprou {food.NamePt} (-{food.CostCoins} Moedas) → +{food.HungerRestore:F0}% Fome."));
        MarkDirty();
        return true;
    }

    public bool FeedQuickBasic() => BuyAndFeed(FoodCatalog.Menu[0]);

    public bool BuyOrEquipHat(PetAccessory hat)
    {
        var pet = Plugin.Config.Pet;
        pet.EnsureCollections();

        if (pet.UnlockedHats.Contains(hat))
        {
            pet.EquippedAccessory = hat;
            Plugin.Config.Save();
            return true;
        }

        var info = CosmeticCatalog.GetHat(hat);
        if (pet.Level < info.RequiredLevel)
        {
            Say(L.T($"Requires Lv.{info.RequiredLevel}!", $"Requer Nível {info.RequiredLevel}!"), 3f);
            return false;
        }
        if (pet.Coins < info.CostCoins)
        {
            Say(L.T($"Need {info.CostCoins} Coins!", $"Precisa de {info.CostCoins} Moedas!"), 3f);
            return false;
        }

        pet.Coins -= info.CostCoins;
        pet.TotalCoinsSpent += info.CostCoins;
        pet.UnlockedHats.Add(hat);
        pet.EquippedAccessory = hat;
        TriggerReaction(PetMood.Celebrating, 4.0f, L.T($"New hat: {info.NameEn}!!", $"Novo chapéu: {info.NamePt}!!"));
        LogEvent(L.T($"Bought {info.NameEn} (-{info.CostCoins} Coins).", $"Comprou {info.NamePt} (-{info.CostCoins} Moedas)."));
        Plugin.Config.Save();
        return true;
    }

    public bool BuyOrEquipOutfit(PetOutfit outfit)
    {
        var pet = Plugin.Config.Pet;
        pet.EnsureCollections();

        if (pet.UnlockedOutfits.Contains(outfit))
        {
            pet.EquippedOutfit = outfit;
            Plugin.Config.Save();
            return true;
        }

        var info = CosmeticCatalog.GetOutfit(outfit);
        if (pet.Level < info.RequiredLevel)
        {
            Say(L.T($"Requires Lv.{info.RequiredLevel}!", $"Requer Nível {info.RequiredLevel}!"), 3f);
            return false;
        }
        if (pet.Coins < info.CostCoins)
        {
            Say(L.T($"Need {info.CostCoins} Coins!", $"Precisa de {info.CostCoins} Moedas!"), 3f);
            return false;
        }

        pet.Coins -= info.CostCoins;
        pet.TotalCoinsSpent += info.CostCoins;
        pet.UnlockedOutfits.Add(outfit);
        pet.EquippedOutfit = outfit;
        TriggerReaction(PetMood.Celebrating, 4.5f, L.T($"New outfit: {info.NameEn}!!", $"Nova roupa: {info.NamePt}!!"));
        LogEvent(L.T($"Bought {info.NameEn} (-{info.CostCoins} Coins).", $"Comprou {info.NamePt} (-{info.CostCoins} Moedas)."));
        Plugin.Config.Save();
        return true;
    }

    public bool BuyCustomColorKit()
    {
        var pet = Plugin.Config.Pet;
        if (pet.UnlockedCustomColors) return true;

        if (pet.Level < CosmeticCatalog.DyeKitRequiredLevel)
        {
            Say(L.T($"Requires Lv.{CosmeticCatalog.DyeKitRequiredLevel}!", $"Requer Nível {CosmeticCatalog.DyeKitRequiredLevel}!"), 3f);
            return false;
        }
        if (pet.Coins < CosmeticCatalog.DyeKitCostCoins)
        {
            Say(L.T($"Need {CosmeticCatalog.DyeKitCostCoins} Coins!", $"Precisa de {CosmeticCatalog.DyeKitCostCoins} Moedas!"), 3f);
            return false;
        }

        pet.Coins -= CosmeticCatalog.DyeKitCostCoins;
        pet.TotalCoinsSpent += CosmeticCatalog.DyeKitCostCoins;
        pet.UnlockedCustomColors = true;
        var def = PetStateData.DefaultPaletteFor(pet.Species);
        pet.CustomBodyColor = def.Body;
        pet.CustomAccentColor = def.Accent;
        pet.CustomDetailColor = def.Detail;
        pet.UseCustomColors = true;
        TriggerReaction(PetMood.Celebrating, 4.0f, L.T("Unlocked Custom Dye Palette!!", "Desbloqueou Kit de Cores!!"));
        LogEvent(L.T($"Bought Custom Color Dye Kit (-{CosmeticCatalog.DyeKitCostCoins} Coins).",
            $"Comprou Kit de Cores Personalizadas (-{CosmeticCatalog.DyeKitCostCoins} Moedas)."));
        Plugin.Config.Save();
        return true;
    }

    public bool BuyOrEquipWeaponSkin(WeaponSkin skin)
    {
        var pet = Plugin.Config.Pet;
        pet.UnlockedWeaponSkins ??= [WeaponSkin.Default];
        pet.UnlockedWeaponSkins.Add(WeaponSkin.Default);

        if (pet.UnlockedWeaponSkins.Contains(skin))
        {
            pet.EquippedWeaponSkin = skin;
            Plugin.Config.Save();
            return true;
        }

        var info = WeaponSkinCatalog.Get(skin);
        if (pet.Level < info.RequiredLevel)
        {
            Say(L.T($"Requires Lv.{info.RequiredLevel}!", $"Requer Nível {info.RequiredLevel}!"), 3f);
            return false;
        }

        if (pet.Coins < info.CostCoins)
        {
            Say(L.T($"Need {info.CostCoins} Coins!", $"Precisa de {info.CostCoins} Moedas!"), 3f);
            return false;
        }

        pet.Coins -= info.CostCoins;
        pet.TotalCoinsSpent += info.CostCoins;
        pet.UnlockedWeaponSkins.Add(skin);
        pet.EquippedWeaponSkin = skin;

        TriggerReaction(PetMood.Celebrating, 4.5f,
            L.T($"Unlocked {info.NameEn}!!", $"Desbloqueou {info.NamePt}!!"));
        LogEvent(L.T(
            $"Unlocked & equipped {info.NameEn} (-{info.CostCoins} Coins)!",
            $"Desbloqueou e equipou {info.NamePt} (-{info.CostCoins} Moedas)!"));
        Plugin.Config.Save();
        return true;
    }

    public void PetCompanion()
    {
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched)
        {
            WarmEgg(22f);
            return;
        }

        if (pet.IsHibernating)
        {
            WakeFromHibernation(viaFood: false);
            pet.PetsReceived++;
            return;
        }

        ManualNap = false;
        pet.Happiness = MathF.Min(100f, pet.Happiness + 14f);
        pet.PetsReceived++;
        var now = Environment.TickCount64;
        if (now - lastHeadpatXpMs >= 10_000)
        {
            lastHeadpatXpMs = now;
            AddXp(1);
        }
        TriggerReaction(PetMood.Petted, 3.0f, GetPettedLine());
        LogEvent(L.T($"Petted {pet.Name} (+14% Happiness).", $"Fez carinho em {pet.Name} (+14% Felicidade)."));
        MarkDirty();
    }

    public void WarmEgg(float amount)
    {
        var pet = Plugin.Config.Pet;
        if (pet.IsHatched || HatchAnimTimerSec > 0f) return;

        pet.EggProgress = MathF.Min(100f, pet.EggProgress + amount);
        WobblePulseSec = 0.7f;
        Say(pet.EggProgress >= 70f
            ? L.T("*crack!* Almost ready!!", "*crec!* Quase nascendo!!")
            : L.T("*wobble!* Warm & cozy...", "*balança!* Quentinho..."), 2.5f);

        if (pet.EggProgress >= 100f)
        {
            BeginHatchingAnimation();
        }
        MarkDirty();
    }

    private static bool IsPlayerOriginatedChat(XivChatType type) => type switch
    {
        XivChatType.Say or XivChatType.Shout or XivChatType.Yell
        or XivChatType.TellOutgoing or XivChatType.TellIncoming
        or XivChatType.Party or XivChatType.Alliance or XivChatType.CrossParty
        or XivChatType.FreeCompany or XivChatType.NoviceNetwork or XivChatType.PvPTeam
        or XivChatType.Ls1 or XivChatType.Ls2 or XivChatType.Ls3 or XivChatType.Ls4
        or XivChatType.Ls5 or XivChatType.Ls6 or XivChatType.Ls7 or XivChatType.Ls8
        or XivChatType.CrossLinkShell1 or XivChatType.CrossLinkShell2 or XivChatType.CrossLinkShell3 or XivChatType.CrossLinkShell4
        or XivChatType.CrossLinkShell5 or XivChatType.CrossLinkShell6 or XivChatType.CrossLinkShell7 or XivChatType.CrossLinkShell8
        or XivChatType.CustomEmote or XivChatType.StandardEmote
        or XivChatType.Echo or XivChatType.Debug => true,
        _ => false,
    };

    private void OnChatMessage(IHandleableChatMessage chatMsg)
    {
        if (!Plugin.ClientState.IsLoggedIn || !Plugin.Config.Pet.IsHatched || Plugin.Config.Pet.IsHibernating) return;
        if (IsPlayerOriginatedChat(chatMsg.LogKind)) return;

        var text = chatMsg.Message.TextValue;
        if (string.IsNullOrWhiteSpace(text)) return;

        var now = Environment.TickCount64;

        // 1. Daily Duty Roulette Bonus (extra reward on top of the Duty clear! +20% Coins)
        if ((text.Contains("Duty Roulette", StringComparison.OrdinalIgnoreCase)
             || text.Contains("Zufallsinhalt", StringComparison.OrdinalIgnoreCase)
             || text.Contains("Mission aléatoire", StringComparison.OrdinalIgnoreCase)
             || text.Contains("コンテンツルーレット", StringComparison.Ordinal))
            && (text.Contains("bonus", StringComparison.OrdinalIgnoreCase)
                || text.Contains("reward", StringComparison.OrdinalIgnoreCase)
                || text.Contains("receive", StringComparison.OrdinalIgnoreCase)
                || text.Contains("obtain", StringComparison.OrdinalIgnoreCase)
                || text.Contains("complete", StringComparison.OrdinalIgnoreCase)))
        {
            if (now - lastRouletteRewardMs >= 45_000)
            {
                lastRouletteRewardMs = now;
                Plugin.Config.Pet.RoulettesCompleted++;
                GrantActivityReward(72, 120, 0f,
                    L.T("Duty Roulette Bonus!", "Bônus de Roleta Diária!"),
                    L.T("Roulette Bonus!! +72 Coins!", "Bônus de Roleta!! +72 Moedas!"),
                    celebrate: true, printChat: true);
            }
            return;
        }

        // 2. Critical Engagements (Bozja), Critical Encounters (Occult Crescent), Eureka NMs
        if ((text.Contains("Critical Engagement", StringComparison.OrdinalIgnoreCase)
             || text.Contains("Critical Encounter", StringComparison.OrdinalIgnoreCase)
             || text.Contains("notorious monster", StringComparison.OrdinalIgnoreCase)
             || ((CurrentFieldZone is FieldZoneKind.Bozja or FieldZoneKind.OccultCrescent or FieldZoneKind.Eureka)
                 && (text.Contains("mettle", StringComparison.OrdinalIgnoreCase)
                     || text.Contains("phantom", StringComparison.OrdinalIgnoreCase)
                     || text.Contains("sanguine cipher", StringComparison.OrdinalIgnoreCase)
                     || text.Contains("enlightenment", StringComparison.OrdinalIgnoreCase))))
            && (text.Contains("complete", StringComparison.OrdinalIgnoreCase)
                || text.Contains("obtain", StringComparison.OrdinalIgnoreCase)
                || text.Contains("receive", StringComparison.OrdinalIgnoreCase)
                || text.Contains("awarded", StringComparison.OrdinalIgnoreCase)))
        {
            if (now - lastCeRewardMs >= 30_000)
            {
                lastCeRewardMs = now;
                Plugin.Config.Pet.FatesCompleted++;
                var zone = FieldZoneLabel(CurrentFieldZone != FieldZoneKind.None ? CurrentFieldZone : FieldZoneKind.OccultCrescent);
                GrantActivityReward(78, 95, 10f,
                    L.T($"Critical Engagement / NM ({zone})", $"Critical Engagement / NM ({zone})"),
                    L.T("Epic CE/NM clear!! +78 Coins!", "CE/NM concluído!! +78 Moedas!"),
                    celebrate: true, printChat: true);
            }
            return;
        }

        // 3. FATEs / Bozja Skirmishes / Occult Crescent FATEs
        if ((text.Contains("FATE", StringComparison.Ordinal) || text.Contains("skirmish", StringComparison.OrdinalIgnoreCase))
            && (text.Contains("complete", StringComparison.OrdinalIgnoreCase)
                || text.Contains("succeeded", StringComparison.OrdinalIgnoreCase)
                || text.Contains("ended", StringComparison.OrdinalIgnoreCase)
                || text.Contains("abgeschlossen", StringComparison.OrdinalIgnoreCase)
                || text.Contains("terminé", StringComparison.OrdinalIgnoreCase)
                || text.Contains("コンプリート", StringComparison.Ordinal)))
        {
            RewardFateCompleted(now);
            return;
        }

        // 4. Crafting Synthesis (DoH)
        if (text.Contains("You synthesize", StringComparison.OrdinalIgnoreCase)
            || text.Contains("hergestellt", StringComparison.OrdinalIgnoreCase)
            || text.Contains("fabriqué", StringComparison.OrdinalIgnoreCase)
            || text.Contains("製作に成功", StringComparison.Ordinal))
        {
            RewardCraftCompleted(now);
            return;
        }

        // 5. Gathering & Fishing (DoL)
        if ((CurrentJob.Category == JobCategoryKind.Gatherer
             && (text.Contains("You obtain", StringComparison.OrdinalIgnoreCase)
                 || text.Contains("You land", StringComparison.OrdinalIgnoreCase)
                 || text.Contains("fished up", StringComparison.OrdinalIgnoreCase)
                 || text.Contains("erhalten", StringComparison.OrdinalIgnoreCase)
                 || text.Contains("obtenu", StringComparison.OrdinalIgnoreCase)
                 || text.Contains("を手に入れた", StringComparison.Ordinal)))
            || text.Contains("You land a", StringComparison.OrdinalIgnoreCase))
        {
            RewardGatherCompleted(now);
            return;
        }

        // 6. Hunt Marks (Allied Seals, Centurio Seals, Sacks of Nuts)
        if ((text.Contains("Allied Seals", StringComparison.OrdinalIgnoreCase)
             || text.Contains("Centurio Seals", StringComparison.OrdinalIgnoreCase)
             || text.Contains("Sack of Nuts", StringComparison.OrdinalIgnoreCase)
             || text.Contains("Sacks of Nuts", StringComparison.OrdinalIgnoreCase))
            && (text.Contains("obtain", StringComparison.OrdinalIgnoreCase)
                || text.Contains("receive", StringComparison.OrdinalIgnoreCase)))
        {
            if (now - lastHuntRewardMs >= 20_000)
            {
                lastHuntRewardMs = now;
                GrantActivityReward(42, 50, 5f,
                    L.T("Hunt Mark Reward", "Recompensa de Hunt"),
                    L.T("Nice Hunt!! +42 Coins!", "Boa Hunt!! +42 Moedas!"),
                    celebrate: false, printChat: false);
            }
            return;
        }

        // 7. Gold Saucer GATEs
        if (CurrentFieldZone == FieldZoneKind.GoldSaucer
            && (text.Contains("GATE", StringComparison.Ordinal) || text.Contains("MGP", StringComparison.Ordinal))
            && (text.Contains("obtain", StringComparison.OrdinalIgnoreCase)
                || text.Contains("receive", StringComparison.OrdinalIgnoreCase)
                || text.Contains("awarded", StringComparison.OrdinalIgnoreCase)
                || text.Contains("complete", StringComparison.OrdinalIgnoreCase)))
        {
            if (now - lastGateRewardMs >= 30_000)
            {
                lastGateRewardMs = now;
                GrantActivityReward(36, 45, 5f,
                    L.T("Gold Saucer GATE / MGP", "Gold Saucer GATE / MGP"),
                    L.T("Gold Saucer fun!! +36 Coins!", "Gold Saucer!! +36 Moedas!"),
                    celebrate: false, printChat: false);
            }
            return;
        }

        // 8. Island Sanctuary
        if (CurrentFieldZone == FieldZoneKind.IslandSanctuary
            && (text.Contains("cowries", StringComparison.OrdinalIgnoreCase)
                || text.Contains("sanctuary", StringComparison.OrdinalIgnoreCase))
            && (text.Contains("obtain", StringComparison.OrdinalIgnoreCase)
                || text.Contains("receive", StringComparison.OrdinalIgnoreCase)))
        {
            if (now - lastSanctuaryRewardMs >= 25_000)
            {
                lastSanctuaryRewardMs = now;
                GrantActivityReward(18, 22, 3f,
                    L.T("Island Sanctuary Activity", "Atividade na Ilha Santuário"),
                    L.T("Cozy Island work! +18 Coins", "Ilha Santuário! +18 Moedas"),
                    celebrate: false, printChat: false);
            }
            return;
        }

        // 10. Limited Jobs: Blue Mage Spell Learned & Beastmaster Beast Tamed / Bestiary
        if ((text.Contains("blue magic", StringComparison.OrdinalIgnoreCase) && text.Contains("learn", StringComparison.OrdinalIgnoreCase))
            || text.Contains("青魔法", StringComparison.Ordinal))
        {
            if (now - lastLimitedJobRewardMs >= 15_000)
            {
                lastLimitedJobRewardMs = now;
                GrantActivityReward(36, 45, 4f,
                    L.T("Blue Magic Spell Learned (BLU)", "Magia Azul Aprendida (BLU)"),
                    L.T("New Blue Magic!! +36 Coins!", "Nova Magia Azul!! +36 Moedas!"),
                    celebrate: true, printChat: false);
            }
            return;
        }

        if ((CurrentJob.JobId == 43
             && (text.Contains("bestiary", StringComparison.OrdinalIgnoreCase)
                 || text.Contains("tamed", StringComparison.OrdinalIgnoreCase)
                 || text.Contains("battlehorn", StringComparison.OrdinalIgnoreCase)
                 || text.Contains("familiar", StringComparison.OrdinalIgnoreCase)))
            || (text.Contains("Master's Bestiary", StringComparison.OrdinalIgnoreCase)))
        {
            if (now - lastLimitedJobRewardMs >= 15_000)
            {
                lastLimitedJobRewardMs = now;
                GrantActivityReward(36, 45, 4f,
                    L.T("Beast Tamed / Bestiary (BST)", "Fera Domada / Bestiário (BST)"),
                    L.T("New Beast tamed!! +36 Coins!", "Nova Fera domada!! +36 Moedas!"),
                    celebrate: true, printChat: false);
            }
            return;
        }

        // 9. Quests, Levequests, Tribal Quests & Cosmic Exploration Stellar Missions
        if ((text.Contains("quest", StringComparison.OrdinalIgnoreCase)
             || text.Contains("levequest", StringComparison.OrdinalIgnoreCase)
             || text.Contains("stellar mission", StringComparison.OrdinalIgnoreCase)
             || text.Contains("mission", StringComparison.OrdinalIgnoreCase)
             || text.Contains("delivery", StringComparison.OrdinalIgnoreCase))
            && (text.Contains("complete", StringComparison.OrdinalIgnoreCase)
                || text.Contains("accomplished", StringComparison.OrdinalIgnoreCase)
                || text.Contains("fulfilled", StringComparison.OrdinalIgnoreCase)
                || text.Contains("abgeschlossen", StringComparison.OrdinalIgnoreCase)
                || text.Contains("terminée", StringComparison.OrdinalIgnoreCase)
                || text.Contains("コンプリート", StringComparison.Ordinal)))
        {
            if (now - lastQuestRewardMs >= 15_000)
            {
                lastQuestRewardMs = now;
                var isCosmic = CurrentFieldZone == FieldZoneKind.CosmicExploration
                    || text.Contains("stellar mission", StringComparison.OrdinalIgnoreCase);
                GrantActivityReward(isCosmic ? 36 : 30, isCosmic ? 45 : 40, 5f,
                    isCosmic
                        ? L.T("Cosmic Exploration Mission", "Missão de Exploração Cósmica")
                        : L.T("Quest / Leve Completed", "Quest / Leve Concluída"),
                    isCosmic
                        ? L.T("Stellar Mission done! +36 Coins!", "Missão Cósmica! +36 Moedas!")
                        : L.T("Quest done! +30 Coins!", "Quest concluída! +30 Moedas!"),
                    celebrate: false, printChat: false);
            }
        }
    }

    private void RewardFateCompleted(long now)
    {
        if (Plugin.Config.Pet.IsHibernating || now - lastFateRewardMs < 15_000) return;
        lastFateRewardMs = now;
        Plugin.Config.Pet.FatesCompleted++;

        var isFieldOp = CurrentFieldZone is FieldZoneKind.Bozja or FieldZoneKind.Eureka or FieldZoneKind.OccultCrescent;
        var coins = isFieldOp ? 36 : 26;
        var xp = isFieldOp ? 45 : 35;
        var label = isFieldOp
            ? L.T($"Field Operation FATE ({FieldZoneLabel(CurrentFieldZone)})", $"FATE de Campo ({FieldZoneLabel(CurrentFieldZone)})")
            : L.T("FATE Completed", "FATE Concluída");
        GrantActivityReward(coins, xp, 5f, label,
            L.T($"FATE Clear!! +{coins} Coins!", $"FATE Concluída!! +{coins} Moedas!"),
            celebrate: false, printChat: false);
    }

    private void RewardCraftCompleted(long now)
    {
        if (Plugin.Config.Pet.IsHibernating || now - lastCraftRewardMs < 25_000) return;
        lastCraftRewardMs = now;
        Plugin.Config.Pet.CraftsRewarded++;
        var isCosmic = CurrentFieldZone == FieldZoneKind.CosmicExploration;
        var coins = isCosmic ? 17 : 12;
        var xp = isCosmic ? 20 : 15;
        GrantActivityReward(coins, xp, 3f,
            isCosmic ? L.T("Cosmic Crafting (DoH)", "Craft Cósmico (DoH)") : L.T("Crafting Synthesis (DoH)", "Síntese de Craft (DoH)"),
            L.T($"Great craft! +{coins} Coins!", $"Ótimo craft! +{coins} Moedas!"),
            celebrate: false, printChat: false);
    }

    private void RewardGatherCompleted(long now)
    {
        if (Plugin.Config.Pet.IsHibernating || now - lastGatherRewardMs < 25_000) return;
        lastGatherRewardMs = now;
        Plugin.Config.Pet.GathersRewarded++;
        var isSpecial = CurrentFieldZone is FieldZoneKind.CosmicExploration or FieldZoneKind.Diadem;
        var coins = isSpecial ? 17 : 12;
        var xp = isSpecial ? 20 : 15;
        GrantActivityReward(coins, xp, 3f,
            isSpecial ? L.T("Expedition Gathering (DoL)", "Coleta de Expedição (DoL)") : L.T("Gathering / Fishing (DoL)", "Coleta / Pesca (DoL)"),
            L.T($"Nice harvest! +{coins} Coins!", $"Boa coleta! +{coins} Moedas!"),
            celebrate: false, printChat: false);
    }

    private void GrantActivityReward(int coins, int rawXp, float energyCost, string logReason, string speechLine, bool celebrate, bool printChat)
    {
        var pet = Plugin.Config.Pet;
        if (pet.IsHibernating) return;

        ConsumeActivityEnergy(energyCost);
        AwardCoins(coins, logReason);
        var actualXp = AddXp(rawXp);
        pet.Happiness = MathF.Min(100f, pet.Happiness + (celebrate ? 10f : 3.5f));

        if (celebrate)
            TriggerReaction(PetMood.Celebrating, 4.5f, speechLine);
        else if (SpeechTimerSec <= 0f)
            Say(speechLine, 3.2f);

        if (printChat)
            Plugin.ChatGui.Print($"[PetFrenzy] {logReason} — +{coins} Coins (+{actualXp} XP)!");

        MarkDirty();
    }

    public static string FieldZoneLabel(FieldZoneKind kind) => kind switch
    {
        FieldZoneKind.Eureka => "Eureka",
        FieldZoneKind.Bozja => "Bozja / Zadnor",
        FieldZoneKind.OccultCrescent => "Occult Crescent",
        FieldZoneKind.CosmicExploration => "Cosmic Exploration",
        FieldZoneKind.Diadem => "The Diadem",
        FieldZoneKind.IslandSanctuary => "Island Sanctuary",
        FieldZoneKind.GoldSaucer => "Gold Saucer",
        _ => "Eorzea",
    };

    public void ToggleNap()
    {
        if (!Plugin.Config.Pet.IsHatched) return;
        ManualNap = !ManualNap;
        if (ManualNap)
        {
            Say(L.T("Zzz... nap time!", "Zzz... hora da soneca!"), 3.5f);
        }
        else
        {
            TriggerReaction(PetMood.Happy, 2.5f, L.T("I'm awake!!", "Acordei!!"));
        }
    }

    // SHA-256 hashes (keeps exclusive unlocks private)
    private const string CreatorCharacterHash = "3a748b1dfad8ca0e19afbc57636f872c83746b348fc9ceeae1ce1f7770de4e00";
    private static readonly HashSet<string> FluffyKittyAllowedHashes = new(StringComparer.Ordinal)
    {
        CreatorCharacterHash,
        "4c63322ec87f3f622e15ed19aacb6054d392c3c831809df7d892ecad2b6c17fa",
    };

    private static string? GetLocalPlayerHash()
    {
        var name = Plugin.ObjectTable.LocalPlayer?.Name.ToString();
        if (string.IsNullOrWhiteSpace(name)) return null;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.Trim().ToLowerInvariant()))).ToLowerInvariant();
    }

    public bool IsCreatorCharacter()
        => string.Equals(GetLocalPlayerHash(), CreatorCharacterHash, StringComparison.Ordinal);

    public bool CanAdoptFluffyKitty()
        => GetLocalPlayerHash() is { } h && FluffyKittyAllowedHashes.Contains(h);

    // ---- Creator-Only Dev / Sandbox Helpers (strictly locked to creator hash) ----
    public void DevSimulateOfflineHours(float hours)
    {
        if (!IsCreatorCharacter()) return;
        var pet = Plugin.Config.Pet;
        pet.LastUpdatedUtc = DateTime.UtcNow.AddHours(-hours);
        Apply24hOfflineDecay();
        LogEvent($"[DEV] Simulated +{hours:F0}h offline (Hunger={pet.Hunger:F0}%, Hibernating={pet.IsHibernating}).");
        Plugin.Config.Save();
    }

    public void DevForceHibernation()
    {
        if (!IsCreatorCharacter()) return;
        var pet = Plugin.Config.Pet;
        pet.Hunger = 0f;
        pet.ZeroHungerSeconds = HibernationThresholdSec;
        EnterHibernation();
        Plugin.Config.Save();
    }

    public void DevForceWakeUp()
    {
        if (!IsCreatorCharacter()) return;
        WakeFromHibernation(viaFood: true);
        Plugin.Config.Pet.Hunger = 100f;
        Plugin.Config.Pet.Energy = 100f;
        Plugin.Config.Pet.ZeroHungerSeconds = 0f;
        Plugin.Config.Save();
    }

    public void DevAddLevels(int delta)
    {
        if (!IsCreatorCharacter()) return;
        var pet = Plugin.Config.Pet;
        pet.Level = Math.Clamp(pet.Level + delta, 1, PetStateData.MaxLevel);
        pet.Xp = 0;
        LogEvent($"[DEV] Level set to Lv.{pet.Level}.");
        Plugin.Config.Save();
    }

    public void DevSetCoins(int coins, bool add)
    {
        if (!IsCreatorCharacter()) return;
        var pet = Plugin.Config.Pet;
        pet.Coins = add ? Math.Max(0, pet.Coins + coins) : Math.Max(0, coins);
        LogEvent($"[DEV] Coins set to {pet.Coins:N0}.");
        Plugin.Config.Save();
    }

    public void DevAddRawXp(int rawXp)
    {
        if (!IsCreatorCharacter()) return;
        AddXp(rawXp);
        Plugin.Config.Save();
    }

    public void DevUnlockAllCosmetics()
    {
        if (!IsCreatorCharacter()) return;
        var pet = Plugin.Config.Pet;
        pet.EnsureCollections();
        foreach (var h in CosmeticCatalog.Hats) pet.UnlockedHats.Add(h.Hat);
        foreach (var o in CosmeticCatalog.Outfits) pet.UnlockedOutfits.Add(o.Outfit);
        foreach (var w in WeaponSkinCatalog.All) pet.UnlockedWeaponSkins.Add(w.Skin);
        pet.UnlockedCustomColors = true;
        LogEvent("[DEV] Unlocked all Hats, Outfits, Wings, Auras & Dye Kit.");
        Plugin.Config.Save();
    }

    public void DevResetAllCosmetics()
    {
        if (!IsCreatorCharacter()) return;
        var pet = Plugin.Config.Pet;
        pet.UnlockedHats = [PetAccessory.None];
        pet.UnlockedOutfits = [PetOutfit.None];
        pet.UnlockedWeaponSkins = [WeaponSkin.Default];
        pet.EquippedAccessory = PetAccessory.None;
        pet.EquippedOutfit = PetOutfit.None;
        pet.EquippedWeaponSkin = WeaponSkin.Default;
        pet.UnlockedCustomColors = false;
        pet.UseCustomColors = false;
        LogEvent("[DEV] Reset all cosmetics to locked.");
        Plugin.Config.Save();
    }

    public void AdoptNewEgg(PetSpecies species, string customName, bool keepLevelAndStats)
    {
        if (species == PetSpecies.FluffyKitty && !CanAdoptFluffyKitty())
            return;

        var pet = Plugin.Config.Pet;
        pet.Species = species;
        pet.Name = string.IsNullOrWhiteSpace(customName) ? PetStateData.DefaultNameFor(species) : customName.Trim();
        pet.IsHatched = false;
        pet.EggProgress = 0f;
        pet.Hunger = 90f;
        pet.Happiness = 90f;
        pet.Energy = 95f;
        pet.AdoptedUtc = DateTime.UtcNow;

        if (!keepLevelAndStats)
        {
            pet.Level = 1;
            pet.Xp = 0;
            pet.EquippedAccessory = PetAccessory.None;
        }

        ManualNap = false;
        HatchAnimTimerSec = 0f;
        ReactionTimerSec = 0f;
        WobblePulseSec = 0.8f;

        Say(L.T($"A {L.SpeciesName(species)} egg appeared!", $"Um ovo de {L.SpeciesName(species)} apareceu!"), 5f);
        LogEvent(L.T($"Adopted a new {L.SpeciesName(species)} egg named {pet.Name}!",
            $"Adotou um novo ovo de {L.SpeciesName(species)} chamado {pet.Name}!"));
        Plugin.Config.Save();
    }

    private void AwardCoins(int amount, string reason)
    {
        if (amount <= 0) return;
        var pet = Plugin.Config.Pet;
        if (pet.IsHibernating) return;
        pet.Coins += amount;
        pet.TotalCoinsEarned += amount;
        LogEvent(L.T($"+{amount} Coins — {reason}", $"+{amount} Moedas — {reason}"));
        MarkDirty();
    }

    private void BeginHatchingAnimation()
    {
        HatchAnimTimerSec = 3.2f;
        CurrentMood = PetMood.Hatching;
        Say(L.T("*CRACK!!* It's hatching!!", "*CREC!!* Está chocando!!"), 3.5f);
    }

    private void CompleteHatching()
    {
        var pet = Plugin.Config.Pet;
        pet.IsHatched = true;
        pet.EggProgress = 100f;
        pet.Hunger = 100f;
        pet.Happiness = 100f;
        pet.Energy = 100f;

        var hello = pet.Species switch
        {
            PetSpecies.Moogle => L.T($"Kupo!! Hi, I'm {pet.Name}!", $"Kupo!! Oi, eu sou {pet.Name}!"),
            PetSpecies.Namazu => L.T($"Yes, yes! {pet.Name} has arrived!", $"Yes, yes! {pet.Name} chegou!"),
            PetSpecies.FatCat => L.T($"Meow~ {pet.Name} is ready for snacks!", $"Miau~ {pet.Name} quer petiscos!"),
            PetSpecies.FluffyKitty => L.T($"Purrrr~ ♥ {pet.Name} is here for you!", $"Ronron~ ♥ {pet.Name} chegou pra você!"),
            _ => L.T($"*sparkle* {pet.Name} hatched!!", $"*brilho* {pet.Name} nasceu!!"),
        };

        TriggerReaction(PetMood.Celebrating, 5.5f, hello);
        LogEvent(L.T($"{pet.Name} ({L.SpeciesName(pet.Species)}) hatched from the egg!",
            $"{pet.Name} ({L.SpeciesName(pet.Species)}) nasceu do ovo!"));
        Plugin.Config.Save();
    }

    private int AddXp(int rawAmount)
    {
        if (rawAmount <= 0) return 0;
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched || pet.IsHibernating || pet.Level >= PetStateData.MaxLevel) return 0;

        pet.RefreshDailyXpWindow();
        var mult = pet.CurrentXpMultiplier;
        var amount = Math.Max(1, (int)MathF.Round(rawAmount * mult));

        pet.DailyXpEarned += amount;
        pet.Xp += amount;
        while (pet.Level < PetStateData.MaxLevel && pet.Xp >= pet.XpForNextLevel)
        {
            pet.Xp -= pet.XpForNextLevel;
            pet.Level++;
            var lvBonus = pet.Level % 10 == 0 ? 180 : 60;
            AwardCoins(lvBonus, L.T($"Level {pet.Level} Bonus!", $"Bônus Nível {pet.Level}!"));
            TriggerReaction(PetMood.Celebrating, 5.0f,
                pet.Level == PetStateData.MaxLevel
                    ? L.T("MAX LEVEL 100!! LEGENDARY!!", "NÍVEL MÁXIMO 100!! LENDÁRIO!!")
                    : L.T($"LEVEL UP! Lv.{pet.Level} (+{lvBonus} Coins)!!", $"SUBIU DE NÍVEL! Lv.{pet.Level} (+{lvBonus} Moedas)!!"));
            LogEvent(L.T($"{pet.Name} reached Level {pet.Level}!", $"{pet.Name} chegou ao Nível {pet.Level}!"));
        }

        if (pet.Level >= PetStateData.MaxLevel)
        {
            pet.Level = PetStateData.MaxLevel;
            pet.Xp = 0;
        }
        MarkDirty();
        return amount;
    }

    private void TriggerReaction(PetMood mood, float durationSec, string line)
    {
        ForcedReactionMood = mood;
        ReactionTimerSec = durationSec;
        Say(line, durationSec);
    }

    private void Say(string line, float durationSec)
    {
        SpeechBubble = line;
        SpeechTimerSec = durationSec;
    }

    private void LogEvent(string msg)
    {
        activityLog.Insert(0, new PetActivityLogItem { TimeLocal = DateTime.Now, Message = msg });
        if (activityLog.Count > 50) activityLog.RemoveAt(activityLog.Count - 1);
    }

    private void MarkDirty()
    {
        dirty = true;
        Plugin.Config.Pet.LastUpdatedUtc = DateTime.UtcNow;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var now = Environment.TickCount64;
        var dt = MathF.Min(0.25f, (now - lastTickMs) / 1000f);
        lastTickMs = now;
        if (dt <= 0f) return;

        AnimTimeSec += dt;
        if (SpeechTimerSec > 0f) SpeechTimerSec = MathF.Max(0f, SpeechTimerSec - dt);
        if (ReactionTimerSec > 0f) ReactionTimerSec = MathF.Max(0f, ReactionTimerSec - dt);
        if (WobblePulseSec > 0f) WobblePulseSec = MathF.Max(0f, WobblePulseSec - dt);

        var pet = Plugin.Config.Pet;

        // Online time Coin reward (+12 Coins every 10 minutes online; paused during Hibernation!)
        if (Plugin.ClientState.IsLoggedIn && !pet.IsHibernating)
        {
            pet.OnlineRewardSeconds += dt;
            if (pet.OnlineRewardSeconds >= OnlineRewardIntervalSec)
            {
                pet.OnlineRewardSeconds -= OnlineRewardIntervalSec;
                AwardCoins(OnlineRewardCoins, L.T("10 min online bonus", "Bônus de 10 min online"));
                if (pet.IsHatched)
                {
                    AddXp(15);
                    if (SpeechTimerSec <= 0f)
                        Say(L.T($"+{OnlineRewardCoins} Coins (10m online!)", $"+{OnlineRewardCoins} Moedas (10m online!)"), 3.5f);
                }
            }
        }

        // Hatching animation countdown
        if (HatchAnimTimerSec > 0f)
        {
            HatchAnimTimerSec -= dt;
            CurrentMood = PetMood.Hatching;
            if (HatchAnimTimerSec <= 0f)
            {
                CompleteHatching();
            }
            return;
        }

        // Egg auto-warming while logged in
        if (!pet.IsHatched)
        {
            if (Plugin.ClientState.IsLoggedIn)
            {
                pet.EggProgress = MathF.Min(100f, pet.EggProgress + dt * 1.15f);
                if (pet.EggProgress >= 100f)
                {
                    BeginHatchingAnimation();
                }
            }
            CurrentMood = PetMood.Idle;
            return;
        }

        // Exact 16-hour full hunger drain (100% / 16h); after 12h at 0% Hunger -> Hibernation!
        pet.Hunger = MathF.Max(0f, pet.Hunger - dt * HungerDecayPerSec);
        if (pet.Hunger <= 0f)
        {
            pet.ZeroHungerSeconds += dt;
            if (pet.ZeroHungerSeconds >= HibernationThresholdSec && !pet.IsHibernating)
            {
                EnterHibernation();
            }
        }
        else
        {
            pet.ZeroHungerSeconds = 0f;
        }

        if (pet.IsHibernating || ManualNap || stationarySec >= AfkSleepThresholdSec)
        {
            pet.Energy = MathF.Min(100f, pet.Energy + dt * 1.2f);
        }
        else
        {
            var happyRate = pet.Hunger <= 0f ? StarvingHappinessDecayPerSec : HappinessDecayPerSec;
            pet.Happiness = MathF.Max(10f, pet.Happiness - dt * happyRate);
            pet.Energy = MathF.Max(15f, pet.Energy - dt * EnergyDecayPerSec);
        }

        // Inspect live FFXIV character state
        EvaluateGameEvents(now, dt);

        // Determine displayed mood by priority
        CurrentMood = ResolveMood();

        if (dirty && now - lastSaveMs > SaveIntervalMs)
        {
            Plugin.Config.Save();
            dirty = false;
            lastSaveMs = now;
        }
    }

    private void EvaluateGameEvents(long now, float dt)
    {
        if (!Plugin.ClientState.IsLoggedIn) return;
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) return;

        var cid = Plugin.PlayerState.ContentId;
        if (cid != 0 && cid != lastContentId)
        {
            lastContentId = cid;
            lastKnownGil = -1;
            hasLastPos = false;
        }

        var pet = Plugin.Config.Pet;
        var inCombat = Plugin.Condition[ConditionFlag.InCombat];
        var isDead = Plugin.Condition[ConditionFlag.Unconscious];
        var inDuty = Plugin.Condition[ConditionFlag.BoundByDuty];
        var isCrafting = Plugin.Condition[ConditionFlag.Crafting] || Plugin.Condition[ConditionFlag.PreparingToCraft];
        var isGathering = Plugin.Condition[ConditionFlag.Gathering] || Plugin.Condition[ConditionFlag.Fishing];

        // Track player's current Job & whether weapon/tool is drawn
        var jobId = player.ClassJob.RowId;
        if (jobId != CurrentJob.JobId)
        {
            CurrentJob = JobWeaponCatalog.Resolve(jobId);
        }

        IsWeaponDrawn = CurrentJob.Category switch
        {
            JobCategoryKind.Crafter => isCrafting,
            JobCategoryKind.Gatherer => isGathering,
            _ => inCombat && !isDead,
        };

        // Fallback duty detection if BoundByDuty toggled without TerritoryChanged
        if (inDuty && ActiveDuty == null)
        {
            TryEnterDutyTracking(Plugin.ClientState.TerritoryType);
        }
        else if (!inDuty && ActiveDuty != null && Plugin.ClientState.TerritoryType != ActiveDuty.Value.TerritoryId)
        {
            FinalizeAbandonedDutyIfAny();
        }

        // Movement / AFK check
        var pos = player.Position;
        if (!hasLastPos || Vector3.DistanceSquared(pos, lastPlayerPos) > 0.04f || inCombat)
        {
            lastPlayerPos = pos;
            hasLastPos = true;
            stationarySec = 0f;
            if (inCombat) ManualNap = false;
        }
        else
        {
            stationarySec += dt;
        }

        // Death event
        if (isDead && !wasDead)
        {
            pet.DeathsMourned++;
            TriggerReaction(PetMood.Crying, 6.0f, L.T("NOOO! Get up!! ;_;", "NÃÃÃO! Levanta!! ;_;"));
            LogEvent(L.T($"{pet.Name} mourned your KO!", $"{pet.Name} chorou pela sua morte!"));
            MarkDirty();
        }
        wasDead = isDead;

        // Combat victory (20s anti-spam cooldown)
        if (wasInCombat && !inCombat && !isDead)
        {
            pet.CombatsCheered++;
            pet.Happiness = MathF.Min(100f, pet.Happiness + 1.5f);
            if (!pet.IsHibernating && now - lastCombatRewardMs >= 20_000)
            {
                lastCombatRewardMs = now;
                ConsumeActivityEnergy(2f);
                AwardCoins(3, L.T("Combat Victory", "Vitória em Combate"));
                AddXp(5);
            }
            MarkDirty();
        }
        wasInCombat = inCombat;

        // Crafting (DoH) session fallback detector (works even if chat filters hide synthesis logs)
        if (isCrafting && !wasCrafting)
            craftStartedMs = now;
        else if (!isCrafting && wasCrafting && (now - craftStartedMs) >= 2_500)
            RewardCraftCompleted(now);
        wasCrafting = isCrafting;

        // Gathering / Fishing (DoL) session fallback detector
        if (isGathering && !wasGathering)
            gatherStartedMs = now;
        else if (!isGathering && wasGathering && (now - gatherStartedMs) >= 2_000)
            RewardGatherCompleted(now);
        wasGathering = isGathering;

        // Live FATE / Bozja Skirmish / Occult Crescent FATE tracker via FateManager (works even without chat!)
        var fm = FateManager.Instance();
        if (fm != null)
        {
            var curFate = fm->CurrentFate;
            if (curFate != null && curFate->FateId != 0)
            {
                if (curFate->FateId != activeFateId)
                {
                    activeFateId = curFate->FateId;
                    activeFateMaxProgress = curFate->Progress;
                }
                else if (curFate->Progress > activeFateMaxProgress)
                {
                    activeFateMaxProgress = curFate->Progress;
                }
            }
            else if (activeFateId != 0)
            {
                if (activeFateMaxProgress >= 80)
                    RewardFateCompleted(now);
                activeFateId = 0;
                activeFateMaxProgress = 0;
            }
        }

        // Low HP panic speech
        if (inCombat && !isDead && player.MaxHp > 0 && (float)player.CurrentHp / player.MaxHp < 0.25f)
        {
            if (SpeechTimerSec <= 0f)
                Say(L.T("HEAL!! WATCH YOUR HP!!", "CURA!! OLHA O HP!!"), 2.5f);
        }

        // Gil gain check every 2s
        if (now - lastGilCheckMs >= 2000)
        {
            lastGilCheckMs = now;
            if (!Plugin.Condition[ConditionFlag.BetweenAreas] && !Plugin.Condition[ConditionFlag.BetweenAreas51])
            {
                var inv = InventoryManager.Instance();
                if (inv != null)
                {
                    long curGil = inv->GetInventoryItemCount(1, false, false, false, 0);
                    if (lastKnownGil >= 0 && curGil > lastKnownGil)
                    {
                        var gained = curGil - lastKnownGil;
                        pet.TotalGilWitnessed += gained;
                        if (gained >= 1_000)
                        {
                            pet.Happiness = MathF.Min(100f, pet.Happiness + 4f);
                            AddXp(gained >= 25_000 ? 15 : 5);
                            if (SpeechTimerSec <= 0f)
                                TriggerReaction(PetMood.Celebrating, 3.5f,
                                    L.T($"+{gained:N0} gil! Shiny!!", $"+{gained:N0} gil! Brilhante!!"));
                        }
                        MarkDirty();
                    }
                    lastKnownGil = curGil;
                }
            }
        }
    }

    private PetMood ResolveMood()
    {
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched) return PetMood.Idle;
        if (HatchAnimTimerSec > 0f) return PetMood.Hatching;
        if (pet.IsHibernating) return PetMood.Sleeping;

        if (Plugin.ClientState.IsLoggedIn && Plugin.ObjectTable.LocalPlayer is { } player)
        {
            if (Plugin.Condition[ConditionFlag.Unconscious]) return PetMood.Crying;
            if (Plugin.Condition[ConditionFlag.InCombat] && player.MaxHp > 0 && (float)player.CurrentHp / player.MaxHp < 0.25f)
                return PetMood.Panic;
        }

        if (ReactionTimerSec > 0f) return ForcedReactionMood;

        if (Plugin.ClientState.IsLoggedIn && Plugin.Condition[ConditionFlag.InCombat])
            return PetMood.Cheering;

        if (Plugin.ClientState.IsLoggedIn &&
            (Plugin.Condition[ConditionFlag.Crafting]
             || Plugin.Condition[ConditionFlag.PreparingToCraft]
             || Plugin.Condition[ConditionFlag.Gathering]
             || Plugin.Condition[ConditionFlag.Fishing]))
            return PetMood.Working;

        if (ManualNap || stationarySec >= AfkSleepThresholdSec)
            return PetMood.Sleeping;

        if (pet.Hunger <= 20f) return PetMood.Hungry;
        if (pet.Happiness < 25f) return PetMood.Grumpy;
        if (pet.Happiness >= 80f) return PetMood.Happy;

        return PetMood.Idle;
    }

    private string GetGreetingLine() => Plugin.Config.Pet.Species switch
    {
        PetSpecies.Moogle => L.T("Kupo! Ready for adventure?", "Kupo! Bora aventurar?"),
        PetSpecies.Namazu => L.T("Yes, yes! Let's earn Coins!", "Yes, yes! Bora ganhar Moedas!"),
        PetSpecies.FatCat => L.T("Meow~ Got any snacks?", "Miau~ Tem petisco?"),
        PetSpecies.FluffyKitty => L.T("Mrrrow~ ♥ Ready to play?", "Miaaau~ ♥ Bora jogar?"),
        _ => L.T("Hi! Let's explore Eorzea!", "Oi! Bora explorar Eorzea!"),
    };

    private string GetSnackLine()
    {
        string[] en = ["Yum!! Delicious!", "Nom nom nom~", "Best meal ever!", "So full~ ♥"];
        string[] pt = ["Nham!! Delícia!", "Nhac nhac nhac~", "Melhor comida!", "Barriguinha cheia~ ♥"];
        var idx = rng.Next(en.Length);
        return L.T(en[idx], pt[idx]);
    }

    private string GetPettedLine()
    {
        string[] en = ["Hehe~ ♥", "Purrrr~ ♥", "Yay, headpats!", "Best WoL ever! ♥"];
        string[] pt = ["Hehe~ ♥", "Ronron~ ♥", "Oba, carinho!", "Melhor WoL! ♥"];
        var idx = rng.Next(en.Length);
        return L.T(en[idx], pt[idx]);
    }
}
