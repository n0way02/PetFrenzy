using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using PetFrenzy.Models;

namespace PetFrenzy.Services;

public sealed class PetActivityLogItem
{
    public DateTime TimeLocal { get; init; } = DateTime.Now;
    public string Message { get; init; } = string.Empty;
}

public sealed unsafe class PetSimulationService : IDisposable
{
    private const float AfkSleepThresholdSec = 90f;
    private const long SaveIntervalMs = 30_000;

    private readonly List<PetActivityLogItem> activityLog = new();
    private readonly Random rng = new();

    private long lastTickMs;
    private long lastSaveMs;
    private long lastGilCheckMs;

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

    // Game state trackers
    private Vector3 lastPlayerPos;
    private bool hasLastPos;
    private float stationarySec;
    private bool wasInCombat;
    private bool wasDead;
    private bool wasInDuty;
    private long lastKnownGil = -1;
    private ulong lastContentId;
    private bool dirty;

    public IReadOnlyList<PetActivityLogItem> ActivityLog => activityLog;

    public PetSimulationService()
    {
        ApplyGentleOfflineDecay();
        lastTickMs = Environment.TickCount64;
        lastSaveMs = lastTickMs;
        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.ClientState.Logout += OnLogout;

        if (!Plugin.Config.Pet.IsHatched)
        {
            Say(L.T("Tap the egg to keep it warm!", "Clique no ovo para aquecê-lo!"), 6f);
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
        Plugin.Config.Pet.LastUpdatedUtc = DateTime.UtcNow;
        Plugin.Config.Save();
    }

    private void OnLogout(int type, int code)
    {
        lastKnownGil = -1;
        hasLastPos = false;
        wasInCombat = false;
        wasDead = false;
        wasInDuty = false;
        lastContentId = 0;
    }

    private void ApplyGentleOfflineDecay()
    {
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched) return;

        var elapsedHours = (float)(DateTime.UtcNow - pet.LastUpdatedUtc).TotalHours;
        if (elapsedHours <= 0.1f) return;

        // Casual mode: gentle capped decay so returning after a break is always friendly
        var hungerDrop = MathF.Min(35f, elapsedHours * 1.8f);
        var happyDrop = MathF.Min(30f, elapsedHours * 1.4f);
        var energyGain = MathF.Min(50f, elapsedHours * 8f);

        pet.Hunger = MathF.Max(20f, pet.Hunger - hungerDrop);
        pet.Happiness = MathF.Max(25f, pet.Happiness - happyDrop);
        pet.Energy = MathF.Min(100f, pet.Energy + energyGain);
        pet.LastUpdatedUtc = DateTime.UtcNow;
    }

    public void Feed()
    {
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched)
        {
            WarmEgg(15f);
            return;
        }

        ManualNap = false;
        pet.Hunger = MathF.Min(100f, pet.Hunger + 28f);
        pet.Happiness = MathF.Min(100f, pet.Happiness + 8f);
        pet.SnacksEaten++;
        AddXp(10);
        TriggerReaction(PetMood.Happy, 3.2f, GetSnackLine());
        LogEvent(L.T($"Fed {pet.Name} a tasty snack (+28 Hunger).", $"Alimentou {pet.Name} (+28 Fome)."));
        MarkDirty();
    }

    public void PetCompanion()
    {
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched)
        {
            WarmEgg(22f);
            return;
        }

        ManualNap = false;
        pet.Happiness = MathF.Min(100f, pet.Happiness + 16f);
        pet.PetsReceived++;
        AddXp(8);
        TriggerReaction(PetMood.Petted, 3.0f, GetPettedLine());
        LogEvent(L.T($"Petted {pet.Name} (+16 Happiness).", $"Fez carinho em {pet.Name} (+16 Felicidade)."));
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

    public void RewardMiniGame(int points)
    {
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched)
        {
            WarmEgg(25f);
            return;
        }

        ManualNap = false;
        pet.Happiness = MathF.Min(100f, pet.Happiness + MathF.Min(30f, 12f + points * 2f));
        pet.Energy = MathF.Max(10f, pet.Energy - 8f);
        var xpGain = 15 + points * 3;
        AddXp(xpGain);
        TriggerReaction(PetMood.Celebrating, 4.0f,
            L.T($"Yay! Score {points}! (+{xpGain} XP)", $"Eba! {points} pontos! (+{xpGain} XP)"));
        LogEvent(L.T($"Played Catch-a-Treat with {pet.Name} (Score {points}, +{xpGain} XP).",
            $"Brincou com {pet.Name} ({points} pts, +{xpGain} XP)."));
        MarkDirty();
    }

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

    public void AdoptNewEgg(PetSpecies species, string customName, bool keepLevelAndStats)
    {
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
            PetSpecies.FatCat => L.T($"Meow~ {pet.Name} is ready for naps!", $"Miau~ {pet.Name} quer carinho!"),
            _ => L.T($"*sparkle* {pet.Name} hatched!!", $"*brilho* {pet.Name} nasceu!!"),
        };

        TriggerReaction(PetMood.Celebrating, 5.5f, hello);
        LogEvent(L.T($"{pet.Name} ({L.SpeciesName(pet.Species)}) hatched from the egg!",
            $"{pet.Name} ({L.SpeciesName(pet.Species)}) nasceu do ovo!"));
        Plugin.Config.Save();
    }

    private void AddXp(int amount)
    {
        if (amount <= 0) return;
        var pet = Plugin.Config.Pet;
        if (!pet.IsHatched) return;

        pet.Xp += amount;
        while (pet.Xp >= pet.XpForNextLevel)
        {
            pet.Xp -= pet.XpForNextLevel;
            pet.Level++;
            TriggerReaction(PetMood.Celebrating, 5.0f,
                L.T($"LEVEL UP! Now Lv.{pet.Level}!!", $"SUBIU DE NÍVEL! Agora Lv.{pet.Level}!!"));
            LogEvent(L.T($"{pet.Name} reached Level {pet.Level}!", $"{pet.Name} chegou ao Nível {pet.Level}!"));
        }
        MarkDirty();
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
        if (activityLog.Count > 40) activityLog.RemoveAt(activityLog.Count - 1);
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

        // Casual slow stat tick
        if (ManualNap || stationarySec >= AfkSleepThresholdSec)
        {
            pet.Energy = MathF.Min(100f, pet.Energy + dt * 1.4f);
            pet.Hunger = MathF.Max(10f, pet.Hunger - dt * 0.012f);
        }
        else
        {
            pet.Hunger = MathF.Max(10f, pet.Hunger - dt * 0.025f);
            pet.Happiness = MathF.Max(15f, pet.Happiness - dt * 0.018f);
            pet.Energy = MathF.Max(15f, pet.Energy - dt * 0.015f);
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

        // Combat victory celebration
        if (wasInCombat && !inCombat && !isDead)
        {
            pet.CombatsCheered++;
            pet.Happiness = MathF.Min(100f, pet.Happiness + 2f);
            AddXp(5);
            MarkDirty();
        }
        wasInCombat = inCombat;

        // Duty completed
        if (wasInDuty && !inDuty && !isDead)
        {
            pet.DutiesCelebrated++;
            pet.Happiness = MathF.Min(100f, pet.Happiness + 10f);
            AddXp(25);
            TriggerReaction(PetMood.Celebrating, 4.5f, L.T("Duty clear!! GG!!", "Duty concluída!! GG!!"));
            LogEvent(L.T($"{pet.Name} celebrated a Duty clear (+25 XP)!", $"{pet.Name} comemorou uma Duty (+25 XP)!"));
            MarkDirty();
        }
        wasInDuty = inDuty;

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
                        if (gained >= 500)
                        {
                            pet.Happiness = MathF.Min(100f, pet.Happiness + 5f);
                            AddXp(gained >= 25_000 ? 18 : 6);
                            TriggerReaction(PetMood.Celebrating, 3.8f,
                                L.T($"+{gained:N0} gil! Shiny!!", $"+{gained:N0} gil! Brilhante!!"));
                            LogEvent(L.T($"{pet.Name} cheered for +{gained:N0} gil!", $"{pet.Name} comemorou +{gained:N0} gil!"));
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

        if (Plugin.ClientState.IsLoggedIn && Plugin.ObjectTable.LocalPlayer is { } player)
        {
            if (Plugin.Condition[ConditionFlag.Unconscious]) return PetMood.Crying;
            if (Plugin.Condition[ConditionFlag.InCombat] && player.MaxHp > 0 && (float)player.CurrentHp / player.MaxHp < 0.25f)
                return PetMood.Panic;
        }

        if (ReactionTimerSec > 0f) return ForcedReactionMood;

        if (Plugin.ClientState.IsLoggedIn && Plugin.Condition[ConditionFlag.InCombat])
            return PetMood.Cheering;

        if (ManualNap || stationarySec >= AfkSleepThresholdSec)
            return PetMood.Sleeping;

        if (pet.Hunger < 25f) return PetMood.Hungry;
        if (pet.Happiness < 25f) return PetMood.Grumpy;
        if (pet.Happiness >= 80f) return PetMood.Happy;

        return PetMood.Idle;
    }

    private string GetGreetingLine() => Plugin.Config.Pet.Species switch
    {
        PetSpecies.Moogle => L.T("Kupo! Ready for adventure?", "Kupo! Bora aventurar?"),
        PetSpecies.Namazu => L.T("Yes, yes! Let's make Gil!", "Yes, yes! Bora fazer Gil!"),
        PetSpecies.FatCat => L.T("Meow~ Got any snacks?", "Miau~ Tem petisco?"),
        _ => L.T("Hi! Let's explore Eorzea!", "Oi! Bora explorar Eorzea!"),
    };

    private string GetSnackLine()
    {
        string[] en = ["Yum!! Delicious!", "Nom nom nom~", "Best snack ever!", "More please~"];
        string[] pt = ["Nham!! Delícia!", "Nhac nhac nhac~", "Melhor petisco!", "Quero mais~"];
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
