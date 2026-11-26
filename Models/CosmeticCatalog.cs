using System.Collections.Generic;

namespace PetFrenzy.Models;

public enum PetOutfit
{
    None = 0,
    CozyScarf = 1,      // Lv. 10
    HeroCape = 2,       // Lv. 25
    DapperTux = 3,      // Lv. 35
    KnightArmor = 4,    // Lv. 50
    ArchmageRobe = 5,   // Lv. 65
    SeraphWings = 6,    // Lv. 80
    AstralWings = 7,    // Lv. 100 (Ultimate Flex!)
    RedNecktie = 8,     // Lv. 15 (Classic Red Tie!)
}

public sealed record HatInfo(
    PetAccessory Hat,
    string NameEn,
    string NamePt,
    int RequiredLevel,
    int CostCoins);

public sealed record OutfitInfo(
    PetOutfit Outfit,
    string NameEn,
    string NamePt,
    int RequiredLevel,
    int CostCoins);

public static class CosmeticCatalog
{
    public const int DyeKitRequiredLevel = 5;
    public const int DyeKitCostCoins = 350;

    public static readonly HatInfo[] Hats =
    [
        new(PetAccessory.None,            "None",                  "Nenhum",                1,    0),
        new(PetAccessory.CuteBow,         "Cute Ribbon Bow",       "Laço Fofo",             5,    300),
        new(PetAccessory.Sunglasses,      "Cool Shades",           "Óculos Escuros",        10,   550),
        new(PetAccessory.Bandana,         "Adventurer Bandana",    "Bandana Aventureiro",   15,   800),
        new(PetAccessory.PartyHat,        "Party Hat",             "Chapéu de Festa",       20,   1100),
        new(PetAccessory.ChefHat,         "Master Chef Toque",     "Chapéu de Chef",        30,   1600),
        new(PetAccessory.WitchHat,        "Arcane Witch Hat",      "Chapéu de Bruxa",       40,   2200),
        new(PetAccessory.DemonHorns,      "Crimson Horns",         "Chifres Carmesim",      50,   3000),
        new(PetAccessory.RoyalCrown,      "Royal Crown",           "Coroa Real",            60,   4000),
        new(PetAccessory.GoldenHalo,      "Seraphic Halo",         "Auréola Seráfica",      75,   5200),
        new(PetAccessory.WizardHat,       "n0way Archmage Hat",    "Chapéu Arquimago n0way",85,   6800),
        new(PetAccessory.CelestialDiadem, "Celestial Sovereign",   "Diadema Celestial",     95,   8500),
    ];

    public static readonly OutfitInfo[] Outfits =
    [
        new(PetOutfit.None,         "None",                    "Nenhuma",               1,    0),
        new(PetOutfit.CozyScarf,    "Cozy Winter Scarf",       "Cachecol Quentinho",    10,   600),
        new(PetOutfit.RedNecktie,   "Red Necktie",             "Gravata Vermelha",      15,   900),
        new(PetOutfit.HeroCape,     "Hero's Cape",             "Capa de Herói",         25,   1400),
        new(PetOutfit.DapperTux,    "Dapper Bowtie & Vest",    "Smoking Elegante",      35,   2100),
        new(PetOutfit.KnightArmor,  "Paladin Armor & Mantle",  "Armadura de Paladino",  50,   3400),
        new(PetOutfit.ArchmageRobe, "Archmage Collar & Robe",  "Manto de Arquimago",    65,   4800),
        new(PetOutfit.SeraphWings,  "Seraph Angel Wings",      "Asas de Anjo Serafim",  80,   7200),
        new(PetOutfit.AstralWings,  "Astral Dragon Wings",     "Asas de Dragão Astral", 100, 12000),
    ];

    private static readonly Dictionary<PetAccessory, HatInfo> HatMap = BuildHatMap();
    private static readonly Dictionary<PetOutfit, OutfitInfo> OutfitMap = BuildOutfitMap();

    private static Dictionary<PetAccessory, HatInfo> BuildHatMap()
    {
        var d = new Dictionary<PetAccessory, HatInfo>(Hats.Length);
        foreach (var h in Hats) d[h.Hat] = h;
        return d;
    }

    private static Dictionary<PetOutfit, OutfitInfo> BuildOutfitMap()
    {
        var d = new Dictionary<PetOutfit, OutfitInfo>(Outfits.Length);
        foreach (var o in Outfits) d[o.Outfit] = o;
        return d;
    }

    public static HatInfo GetHat(PetAccessory hat) => HatMap.TryGetValue(hat, out var h) ? h : Hats[0];
    public static OutfitInfo GetOutfit(PetOutfit outfit) => OutfitMap.TryGetValue(outfit, out var o) ? o : Outfits[0];
}
