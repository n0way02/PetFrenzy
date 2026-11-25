using System;

namespace PetFrenzy.Models;

public enum PetSpecies
{
    Carbuncle = 0,
    FatCat = 1,
    Moogle = 2,
    Namazu = 3,
}

public enum PetStage
{
    Egg = 0,
    Baby = 1,
    Young = 2,
    Legendary = 3,
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
}

public enum PetAccessory
{
    None = 0,
    Sunglasses = 1,   // Unlocks at Lv. 2
    PartyHat = 2,     // Unlocks at Lv. 4
    RoyalCrown = 3,   // Unlocks at Lv. 6
    GoldenHalo = 4,   // Unlocks at Lv. 8
    WizardHat = 5,    // Unlocks at Lv. 10
}

public sealed class PetStateData
{
    public PetSpecies Species { get; set; } = PetSpecies.Carbuncle;
    public string Name { get; set; } = "Carby";
    public bool IsHatched { get; set; } = false;

    /// <summary>0..100 hatching progress while in Egg stage.</summary>
    public float EggProgress { get; set; } = 0f;

    public int Level { get; set; } = 1;
    public int Xp { get; set; } = 0;

    /// <summary>0..100 (Casual mode: never dies, gently asks for care when low).</summary>
    public float Hunger { get; set; } = 85f;
    public float Happiness { get; set; } = 85f;
    public float Energy { get; set; } = 90f;

    public PetAccessory EquippedAccessory { get; set; } = PetAccessory.None;
    public DateTime AdoptedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    // Fun lifetime stats
    public long TotalGilWitnessed { get; set; }
    public int CombatsCheered { get; set; }
    public int DeathsMourned { get; set; }
    public int DutiesCelebrated { get; set; }
    public int SnacksEaten { get; set; }
    public int PetsReceived { get; set; }

    public PetStage Stage
    {
        get
        {
            if (!IsHatched) return PetStage.Egg;
            if (Level >= 10) return PetStage.Legendary;
            if (Level >= 5) return PetStage.Young;
            return PetStage.Baby;
        }
    }

    public int XpForNextLevel => 100 + (Level - 1) * 50;

    public static int UnlockLevelFor(PetAccessory acc) => acc switch
    {
        PetAccessory.None => 1,
        PetAccessory.Sunglasses => 2,
        PetAccessory.PartyHat => 4,
        PetAccessory.RoyalCrown => 6,
        PetAccessory.GoldenHalo => 8,
        PetAccessory.WizardHat => 10,
        _ => 1,
    };

    public static string DefaultNameFor(PetSpecies sp) => sp switch
    {
        PetSpecies.Carbuncle => "Carby",
        PetSpecies.FatCat => "Mochi",
        PetSpecies.Moogle => "Kupo",
        PetSpecies.Namazu => "Wasshoi",
        _ => "Buddy",
    };
}
