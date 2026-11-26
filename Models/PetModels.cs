using System;
using System.Collections.Generic;
using System.Numerics;

namespace PetFrenzy.Models;

public enum PetSpecies
{
    Carbuncle = 0,
    FatCat = 1,
    Moogle = 2,
    Namazu = 3,
    FluffyKitty = 4, // Creator-exclusive fluffy seal-point kitty
    ShepherdDog = 5, // Fluffy German Shepherd dog
}

public enum PetStage
{
    Egg = 0,
    Baby = 1,      // Lv. 1 - 19
    Young = 2,     // Lv. 20 - 49
    Veteran = 3,   // Lv. 50 - 79
    Legendary = 4, // Lv. 80 - 100
}

public enum PetMood
{
    Idle = 0,
    Happy = 1,
    Sleeping = 2,
    Cheering = 3,
    Panic = 4,
    Crying = 5,
    Celebrating = 6,
    Hungry = 7,
    Grumpy = 8,
    Petted = 9,
    Hatching = 10,
    Working = 11,
}

public enum PetAccessory
{
    None = 0,
    Sunglasses = 1,
    PartyHat = 2,
    RoyalCrown = 3,
    GoldenHalo = 4,
    WizardHat = 5,
    CuteBow = 6,
    Bandana = 7,
    ChefHat = 8,
    WitchHat = 9,
    DemonHorns = 10,
    CelestialDiadem = 11,
}

public readonly record struct FoodItem(
    string Id,
    string NameEn,
    string NamePt,
    int CostCoins,
    float HungerRestore,
    float HappinessRestore,
    float EnergyRestore,
    int XpReward);

public static class FoodCatalog
{
    public static readonly FoodItem[] Menu =
    [
        new("kibble", "Kupo Nut Snack",   "Noz Kupo (Básico)",     40,  35f,  6f,  0f,  8),
        new("tart",   "Rolanberry Tart",  "Torta de Rolanberry",   85,  70f, 18f,  8f, 16),
        new("feast",  "Eorzean Banquet",  "Banquete de Eorzea",   140, 100f, 35f, 25f, 28),
    ];
}

public readonly record struct EnergyPotionItem(
    string Id,
    string NameEn,
    string NamePt,
    int CostCoins,
    float EnergyRestore,
    float HappinessRestore,
    int XpReward);

public static class EnergyPotionCatalog
{
    public static readonly EnergyPotionItem[] Menu =
    [
        new("espresso", "Aether Espresso",     "Expresso Etéreo",       35,  35f,  5f,  6),
        new("elixir",   "Hi-Elixir Tonic",     "Tônico Hi-Elixir",      70,  70f, 12f, 14),
        new("super",    "Loporrit Super Brew", "Super Poção Loporrit", 115, 100f, 25f, 24),
    ];
}

public sealed class PetStateData
{
    public const int MaxLevel = 100;
    public const int DailyFullXpCap = 5000;
    public const int DailyTiredXpCap = 9000;

    public PetSpecies Species { get; set; } = PetSpecies.Carbuncle;
    public string Name { get; set; } = "Carby";
    public bool IsHatched { get; set; } = false;

    /// <summary>0..100 hatching progress while in Egg stage.</summary>
    public float EggProgress { get; set; } = 0f;

    public int Level { get; set; } = 1;
    public int Xp { get; set; } = 0;

    /// <summary>Pet Coins earned by playing FFXIV (online time, dungeons, trials, raids, etc.).</summary>
    public int Coins { get; set; } = 120;
    public long TotalCoinsEarned { get; set; } = 120;
    public long TotalCoinsSpent { get; set; } = 0;

    /// <summary>Accumulated online seconds toward the next 10-minute (+10 Coins) tick.</summary>
    public float OnlineRewardSeconds { get; set; } = 0f;

    /// <summary>0..100 (Drains 100% over 16 hours; after 12h at 0% Hunger, enters Hibernation instead of dying).</summary>
    public float Hunger { get; set; } = 85f;
    public float Happiness { get; set; } = 85f;
    public float Energy { get; set; } = 90f;

    /// <summary>Seconds spent continuously at 0% Hunger (hibernates at 12h = 43,200s).</summary>
    public float ZeroHungerSeconds { get; set; } = 0f;
    /// <summary>When hibernating, pet earns no XP or Coins until woken up via headpats or food.</summary>
    public bool IsHibernating { get; set; } = false;
    /// <summary>Granted for free when entering hibernation so any player can always feed & wake their pet.</summary>
    public bool HasFreeWakeupMeal { get; set; } = false;

    // Equipped cosmetics
    public PetAccessory EquippedAccessory { get; set; } = PetAccessory.None;
    public PetOutfit EquippedOutfit { get; set; } = PetOutfit.None;
    public WeaponSkin EquippedWeaponSkin { get; set; } = WeaponSkin.Default;

    // Owned / unlocked cosmetic sets (purchased in the Shop!)
    public HashSet<PetAccessory> UnlockedHats { get; set; } = [PetAccessory.None];
    public HashSet<PetOutfit> UnlockedOutfits { get; set; } = [PetOutfit.None];
    public HashSet<WeaponSkin> UnlockedWeaponSkins { get; set; } = [WeaponSkin.Default];

    // Custom Dye Colors (unlocked via Shop Dye Kit at Lv. 5)
    public bool UnlockedCustomColors { get; set; } = false;
    public bool UseCustomColors { get; set; } = false;
    public Vector4 CustomBodyColor { get; set; } = new(0.95f, 0.93f, 0.89f, 1f);
    public Vector4 CustomAccentColor { get; set; } = new(0.62f, 0.59f, 0.66f, 1f);
    public Vector4 CustomDetailColor { get; set; } = new(1.00f, 0.45f, 0.62f, 1f);

    public DateTime AdoptedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    // Daily XP pacing (prevents 1-day Lv.100 rush while keeping daily play fun)
    public string DailyXpUtcDate { get; set; } = string.Empty;
    public int DailyXpEarned { get; set; }

    // Fun lifetime stats
    public long TotalGilWitnessed { get; set; }
    public int CombatsCheered { get; set; }
    public int DeathsMourned { get; set; }
    public int DutiesCelebrated { get; set; }
    public int HighEndClears { get; set; }
    public int FatesCompleted { get; set; }
    public int CraftsRewarded { get; set; }
    public int GathersRewarded { get; set; }
    public int RoulettesCompleted { get; set; }
    public int PvpMatches { get; set; }
    public int SnacksEaten { get; set; }
    public int PetsReceived { get; set; }

    public PetStage Stage
    {
        get
        {
            if (!IsHatched) return PetStage.Egg;
            if (Level >= 80) return PetStage.Legendary;
            if (Level >= 50) return PetStage.Veteran;
            if (Level >= 20) return PetStage.Young;
            return PetStage.Baby;
        }
    }

    /// <summary>
    /// Steep Lv.1..100 XP curve (~550k total XP to Lv.100; ~2-3 months with daily soft-cap pacing).
    /// </summary>
    public int XpForNextLevel => Level >= MaxLevel ? 1 : 250 + (Level - 1) * 75 + ((Level - 1) * (Level - 1)) / 2;

    public void RefreshDailyXpWindow()
    {
        // Resets daily at 08:00 UTC (05:00 BRT / Brasília time)
        var today = DateTime.UtcNow.AddHours(-8).ToString("yyyy-MM-dd");
        if (!string.Equals(DailyXpUtcDate, today, StringComparison.Ordinal))
        {
            DailyXpUtcDate = today;
            DailyXpEarned = 0;
        }
    }

    public float CurrentXpMultiplier
    {
        get
        {
            RefreshDailyXpWindow();
            if (DailyXpEarned < DailyFullXpCap) return 1.0f;
            if (DailyXpEarned < DailyTiredXpCap) return 0.35f;
            return 0.10f;
        }
    }

    public void EnsureCollections()
    {
        UnlockedHats ??= [PetAccessory.None];
        UnlockedHats.Add(PetAccessory.None);
        if (EquippedAccessory != PetAccessory.None) UnlockedHats.Add(EquippedAccessory);

        UnlockedOutfits ??= [PetOutfit.None];
        UnlockedOutfits.Add(PetOutfit.None);
        if (EquippedOutfit != PetOutfit.None) UnlockedOutfits.Add(EquippedOutfit);

        UnlockedWeaponSkins ??= [WeaponSkin.Default];
        UnlockedWeaponSkins.Add(WeaponSkin.Default);
        if (EquippedWeaponSkin != WeaponSkin.Default) UnlockedWeaponSkins.Add(EquippedWeaponSkin);

        // Guard against zero-alpha custom colors (e.g. older save before Vector4 field serialization)
        if (CustomBodyColor.W <= 0.01f || CustomAccentColor.W <= 0.01f || CustomDetailColor.W <= 0.01f)
        {
            var def = DefaultPaletteFor(Species);
            CustomBodyColor = def.Body;
            CustomAccentColor = def.Accent;
            CustomDetailColor = def.Detail;
        }
    }

    public static (Vector4 Body, Vector4 Accent, Vector4 Detail) DefaultPaletteFor(PetSpecies sp) => sp switch
    {
        PetSpecies.Carbuncle   => (new(0.24f, 0.88f, 0.82f, 1f), new(0.70f, 0.98f, 0.94f, 1f), new(1.00f, 0.25f, 0.35f, 1f)),
        PetSpecies.FatCat      => (new(0.95f, 0.93f, 0.89f, 1f), new(0.62f, 0.59f, 0.66f, 1f), new(1.00f, 0.45f, 0.62f, 1f)),
        PetSpecies.Moogle      => (new(0.98f, 0.97f, 1.00f, 1f), new(0.45f, 0.12f, 0.78f, 1f), new(1.00f, 0.90f, 0.00f, 1f)),
        PetSpecies.FluffyKitty => (new(0.85f, 0.78f, 0.68f, 1f), new(0.15f, 0.12f, 0.14f, 1f), new(0.54f, 0.84f, 0.99f, 1f)),
        PetSpecies.ShepherdDog => (new(0.76f, 0.54f, 0.31f, 1f), new(0.13f, 0.11f, 0.12f, 1f), new(0.88f, 0.62f, 0.22f, 1f)),
        _                      => (new(0.98f, 0.89f, 0.62f, 1f), new(0.88f, 0.20f, 0.26f, 1f), new(1.00f, 0.45f, 0.62f, 1f)),
    };

    public static string DefaultNameFor(PetSpecies sp) => sp switch
    {
        PetSpecies.Carbuncle => "Carby",
        PetSpecies.FatCat => "Mochi",
        PetSpecies.Moogle => "Kupo",
        PetSpecies.Namazu => "Wasshoi",
        PetSpecies.FluffyKitty => "Fluffy",
        PetSpecies.ShepherdDog => "Rex",
        _ => "Buddy",
    };
}
