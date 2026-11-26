using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace PetFrenzy.Services;

public enum DutyCategory
{
    Unknown = 0,
    Guildhest = 1,
    NormalTrial = 2,
    NormalRaid = 3,
    PvP = 4,
    Dungeon = 5,
    DeepDungeon = 6,
    TreasureDungeon = 7,
    OceanFishing = 8,
    MaskedCarnivale = 9,
    BeastmasterCrucible = 10,
    GoldSaucerDuty = 11,
    AllianceRaid = 12,
    FieldRaid = 13,
    ExtremeTrial = 14,
    ChaoticOrCriterion = 15,
    SavageRaid = 16,
    UltimateRaid = 17,
    OtherDuty = 18,
}

public enum FieldZoneKind
{
    None = 0,
    Eureka = 1,
    Bozja = 2,
    OccultCrescent = 3,
    CosmicExploration = 4,
    Diadem = 5,
    IslandSanctuary = 6,
    GoldSaucer = 7,
}

public readonly record struct DutyRewardRule(
    DutyCategory Category,
    string LabelEn,
    string LabelPt,
    int ClearCoins,
    int ConsolationCoins,
    int ClearXp);

public readonly record struct DetectedDutyInfo(
    uint TerritoryId,
    string DutyName,
    DutyRewardRule Rule);

/// <summary>
/// Resolves any FFXIV TerritoryType into its exact DutyCategory (Normal/Extreme/Unreal Trials,
/// Dungeons, Variant/Criterion, Deep Dungeons, PvP, Treasure Dungeons, Alliance/Chaotic/Savage/Ultimate Raids,
/// Field Raids, Ocean Fishing, Masked Carnivale, Gold Saucer) and detects Field Operation zones
/// (Eureka, Bozja/Zadnor, Occult Crescent, Cosmic Exploration, Diadem, Island Sanctuary).
/// </summary>
public sealed class DutyContentClassifier
{
    public static readonly Dictionary<DutyCategory, DutyRewardRule> Rules = new()
    {
        [DutyCategory.Guildhest]          = new(DutyCategory.Guildhest,          "Guildhest",                  "Guildhest",                  18,   4,  20),
        [DutyCategory.GoldSaucerDuty]     = new(DutyCategory.GoldSaucerDuty,     "Gold Saucer Duty",           "Gold Saucer",                36,   6,  35),
        [DutyCategory.NormalTrial]        = new(DutyCategory.NormalTrial,        "Normal Trial",               "Trial Normal",               42,  10,  45),
        [DutyCategory.MaskedCarnivale]    = new(DutyCategory.MaskedCarnivale,    "Masked Carnivale (BLU)",     "Masked Carnivale (BLU)",     48,  10,  50),
        [DutyCategory.BeastmasterCrucible]= new(DutyCategory.BeastmasterCrucible,"Crucible of Unbroken (BST)",  "Crucible (Beastmaster)",     66,  14,  75),
        [DutyCategory.NormalRaid]         = new(DutyCategory.NormalRaid,         "Normal Raid",                "Raid Normal",                54,  12,  55),
        [DutyCategory.PvP]                = new(DutyCategory.PvP,                "PvP (CC/Frontline/RW)",      "PvP (CC/Frontline/RW)",      60,  18,  65),
        [DutyCategory.OceanFishing]       = new(DutyCategory.OceanFishing,       "Ocean Fishing",              "Pesca Oceânica",             84,  18,  90),
        [DutyCategory.Dungeon]            = new(DutyCategory.Dungeon,            "Dungeon / Variant",          "Dungeon / Variant",          90,  18,  95),
        [DutyCategory.TreasureDungeon]    = new(DutyCategory.TreasureDungeon,    "Treasure Dungeon",           "Dungeon de Mapa",            96,  24, 100),
        [DutyCategory.DeepDungeon]        = new(DutyCategory.DeepDungeon,        "Deep Dungeon",               "Deep Dungeon",              102,  24, 110),
        [DutyCategory.AllianceRaid]       = new(DutyCategory.AllianceRaid,       "Alliance Raid",              "Alliance Raid",             126,  26, 135),
        [DutyCategory.ExtremeTrial]       = new(DutyCategory.ExtremeTrial,       "Extreme / Unreal Trial",     "Trial Extreme / Unreal",    156,  34, 165),
        [DutyCategory.FieldRaid]          = new(DutyCategory.FieldRaid,          "Field Raid (Bozja/OC/Eur)",  "Raid de Campo (Bozja/OC)",  168,  36, 180),
        [DutyCategory.ChaoticOrCriterion] = new(DutyCategory.ChaoticOrCriterion, "Chaotic / Criterion",        "Chaotic / Criterion",       240,  54, 250),
        [DutyCategory.SavageRaid]         = new(DutyCategory.SavageRaid,         "Savage Raid",                "Raid Savage",               288,  60, 300),
        [DutyCategory.UltimateRaid]       = new(DutyCategory.UltimateRaid,       "Ultimate Raid",              "Raid Ultimate",             660, 108, 650),
        [DutyCategory.OtherDuty]          = new(DutyCategory.OtherDuty,          "Other Instanced Duty",       "Outra Instância",            54,  12,  55),
    };

    public static FieldZoneKind ClassifyFieldZone(uint territoryId)
    {
        if (territoryId == 0) return FieldZoneKind.None;
        if (territoryId == 144) return FieldZoneKind.GoldSaucer;

        try
        {
            var terrSheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
            if (terrSheet == null || !terrSheet.TryGetRow(territoryId, out var terr))
                return FieldZoneKind.None;

            var placeName = terr.PlaceName.ValueNullable?.Name.ToString() ?? string.Empty;
            var intendedUse = (uint)terr.TerritoryIntendedUse.RowId;

            // Known TerritoryIntendedUse IDs + resilient PlaceName matching for Dawntrail & future patches
            if (placeName.Contains("Occult Crescent", StringComparison.OrdinalIgnoreCase)
                || placeName.Contains("South Horn", StringComparison.OrdinalIgnoreCase)
                || placeName.Contains("North Horn", StringComparison.OrdinalIgnoreCase)
                || placeName.Contains("Shades' Triangle", StringComparison.OrdinalIgnoreCase)
                || intendedUse == 61)
            {
                // Note: Cosmic Exploration & Occult Crescent may share nearby intendedUse IDs depending on patch;
                // check Cosmic Exploration place names first!
                if (placeName.Contains("Sinus Ardorum", StringComparison.OrdinalIgnoreCase)
                    || placeName.Contains("Cosmic", StringComparison.OrdinalIgnoreCase)
                    || placeName.Contains("Phaenna", StringComparison.OrdinalIgnoreCase))
                    return FieldZoneKind.CosmicExploration;

                return FieldZoneKind.OccultCrescent;
            }

            if (placeName.Contains("Sinus Ardorum", StringComparison.OrdinalIgnoreCase)
                || placeName.Contains("Cosmic", StringComparison.OrdinalIgnoreCase)
                || placeName.Contains("Phaenna", StringComparison.OrdinalIgnoreCase)
                || intendedUse == 60)
                return FieldZoneKind.CosmicExploration;

            if (placeName.Contains("Bozjan", StringComparison.OrdinalIgnoreCase)
                || placeName.Contains("Zadnor", StringComparison.OrdinalIgnoreCase)
                || intendedUse == 48)
                return FieldZoneKind.Bozja;

            if (placeName.Contains("Eureka", StringComparison.OrdinalIgnoreCase)
                || intendedUse == 41)
                return FieldZoneKind.Eureka;

            if (placeName.Contains("Diadem", StringComparison.OrdinalIgnoreCase)
                || intendedUse == 26)
                return FieldZoneKind.Diadem;

            if (placeName.Contains("Unnamed Island", StringComparison.OrdinalIgnoreCase)
                || placeName.Contains("Island Sanctuary", StringComparison.OrdinalIgnoreCase)
                || intendedUse == 49)
                return FieldZoneKind.IslandSanctuary;

            if (intendedUse == 23 || placeName.Contains("Gold Saucer", StringComparison.OrdinalIgnoreCase))
                return FieldZoneKind.GoldSaucer;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, $"[PetFrenzy] Failed to classify field zone {territoryId}");
        }

        return FieldZoneKind.None;
    }

    public static DetectedDutyInfo? ClassifyTerritory(uint territoryId)
    {
        if (territoryId == 0) return null;

        try
        {
            var terrSheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
            if (terrSheet == null || !terrSheet.TryGetRow(territoryId, out var terr))
                return null;

            var cfcRef = terr.ContentFinderCondition;
            var intendedUse = (uint)terr.TerritoryIntendedUse.RowId;
            var placeName = terr.PlaceName.ValueNullable?.Name.ToString() ?? string.Empty;

            // Open-world field operations (Eureka/Bozja/Occult Crescent/Cosmic/Diadem/Island)
            // are tracked via FATE/CE/Gather/Craft events rather than as a single-clear Duty.
            var fieldKind = ClassifyFieldZone(territoryId);
            if (fieldKind is FieldZoneKind.Eureka or FieldZoneKind.Bozja or FieldZoneKind.OccultCrescent
                or FieldZoneKind.CosmicExploration or FieldZoneKind.Diadem or FieldZoneKind.IslandSanctuary
                or FieldZoneKind.GoldSaucer)
            {
                // Except dedicated instanced Field Raids (Delubrum Reginae, Castrum Lacus Litore, Dalriada, Forked Tower, Baldesion Arsenal)
                if (!IsInstancedFieldRaidName(placeName))
                    return null;
            }

            if (cfcRef.RowId == 0)
            {
                // Treasure Hunt Dungeons sometimes lack a standard CFC row
                if (intendedUse == 27 || IsTreasureDungeonName(placeName))
                {
                    var tRule = Rules[DutyCategory.TreasureDungeon];
                    return new DetectedDutyInfo(territoryId, string.IsNullOrWhiteSpace(placeName) ? "Treasure Dungeon" : placeName, tRule);
                }
                return null;
            }

            var cfc = cfcRef.Value;
            var name = cfc.Name.ToString();
            if (string.IsNullOrWhiteSpace(name))
                name = !string.IsNullOrWhiteSpace(placeName) ? placeName : $"Duty #{territoryId}";

            var contentTypeId = cfc.ContentType.RowId;
            var memberTypeId = cfc.ContentMemberType.RowId;
            var isHighEnd = cfc.HighEndDuty;

            var cat = DetermineCategory(name, placeName, contentTypeId, memberTypeId, intendedUse, isHighEnd);
            var rule = Rules.TryGetValue(cat, out var r) ? r : Rules[DutyCategory.OtherDuty];

            return new DetectedDutyInfo(territoryId, name, rule);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, $"[PetFrenzy] Failed to classify territory {territoryId}");
            return null;
        }
    }

    private static bool IsInstancedFieldRaidName(string name)
        => name.Contains("Delubrum Reginae", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Castrum Lacus Litore", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Dalriada", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Baldesion Arsenal", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Forked Tower", StringComparison.OrdinalIgnoreCase);

    private static bool IsTreasureDungeonName(string name)
        => name.Contains("Aquapolis", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Uznair", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Lyhe Ghiah", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Excitatron", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Gymnasion", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Cenote Ja Ja Gural", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Vault Oneiron", StringComparison.OrdinalIgnoreCase);

    private static DutyCategory DetermineCategory(
        string name,
        string placeName,
        uint contentTypeId,
        uint memberTypeId,
        uint intendedUse,
        bool isHighEnd)
    {
        // 1. Ultimate Raids (ContentType 28 or explicit Ultimate tag across EN/DE/FR/JP)
        if (contentTypeId == 28
            || name.Contains("(Ultimate)", StringComparison.OrdinalIgnoreCase)
            || name.Contains("(fatal)", StringComparison.OrdinalIgnoreCase)
            || name.Contains("絶", StringComparison.Ordinal))
        {
            return DutyCategory.UltimateRaid;
        }

        // 2. Savage / Criterion Savage
        if (name.Contains("(Savage)", StringComparison.OrdinalIgnoreCase)
            || name.Contains("(episch)", StringComparison.OrdinalIgnoreCase)
            || name.Contains("(sadique)", StringComparison.OrdinalIgnoreCase)
            || name.Contains("零式", StringComparison.Ordinal))
        {
            return DutyCategory.SavageRaid;
        }

        // 3. Chaotic Alliance Raid & Criterion Dungeons
        if (name.Contains("(Chaotic)", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Chaotic", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Another ", StringComparison.OrdinalIgnoreCase)) // Criterion dungeons ("Another Sil'dihn...", "Another Mount Rokkon...", "Another Aloalo...")
        {
            return DutyCategory.ChaoticOrCriterion;
        }

        // 4. Instanced Field Raids (Delubrum, Castrum, Dalriada, Forked Tower, Baldesion Arsenal)
        if (IsInstancedFieldRaidName(name) || IsInstancedFieldRaidName(placeName))
            return DutyCategory.FieldRaid;

        // 5. Extreme / Unreal / Minstrel's Ballad trials
        if (name.Contains("(Extreme)", StringComparison.OrdinalIgnoreCase)
            || name.Contains("(Unreal)", StringComparison.OrdinalIgnoreCase)
            || name.Contains("(extrem)", StringComparison.OrdinalIgnoreCase)
            || name.Contains("(extrême)", StringComparison.OrdinalIgnoreCase)
            || name.Contains("極", StringComparison.Ordinal)
            || name.Contains("幻", StringComparison.Ordinal)
            || name.Contains("Minstrel's Ballad", StringComparison.OrdinalIgnoreCase))
        {
            return DutyCategory.ExtremeTrial;
        }

        // 6. ContentType / IntendedUse-based classification
        // Lumina ContentType RowIds:
        // 2 = Dungeons, 3 = Guildhests, 4 = Trials, 5 = Raids (8-man & 24-man Alliance),
        // 6 = PvP, 9 = Treasure Hunt, 19 = Gold Saucer, 21 = Deep Dungeons,
        // 26 = Field Operations / Eureka / Bozja, 27 = Masked Carnivale,
        // 28 = Ultimate, 29 = Ocean Fishing / Field Raid, 30 = V&C Dungeons
        if (contentTypeId == 6 || intendedUse is 18 or 28 or 37 or 39 or 42)
            return DutyCategory.PvP;

        if (contentTypeId == 21 || intendedUse == 31)
            return DutyCategory.DeepDungeon;

        if (contentTypeId == 9 || intendedUse == 27 || IsTreasureDungeonName(name) || IsTreasureDungeonName(placeName))
            return DutyCategory.TreasureDungeon;

        if (contentTypeId == 27 || name.Contains("Masked Carnivale", StringComparison.OrdinalIgnoreCase))
            return DutyCategory.MaskedCarnivale;

        if (name.Contains("Crucible of the Unbroken", StringComparison.OrdinalIgnoreCase)
            || placeName.Contains("Crucible of the Unbroken", StringComparison.OrdinalIgnoreCase))
            return DutyCategory.BeastmasterCrucible;

        if (contentTypeId == 19)
            return DutyCategory.GoldSaucerDuty;

        if (intendedUse == 47 || name.Contains("Endeavor", StringComparison.OrdinalIgnoreCase) || name.Contains("Ocean Fishing", StringComparison.OrdinalIgnoreCase))
            return DutyCategory.OceanFishing;

        if (contentTypeId == 3)
            return DutyCategory.Guildhest;

        if (contentTypeId == 2 || contentTypeId == 30)
            return isHighEnd ? DutyCategory.ChaoticOrCriterion : DutyCategory.Dungeon;

        if (contentTypeId == 4)
            return isHighEnd ? DutyCategory.ExtremeTrial : DutyCategory.NormalTrial;

        if (contentTypeId == 5)
        {
            if (memberTypeId == 4)
                return isHighEnd ? DutyCategory.ChaoticOrCriterion : DutyCategory.AllianceRaid;

            return isHighEnd ? DutyCategory.SavageRaid : DutyCategory.NormalRaid;
        }

        if (contentTypeId == 26)
            return DutyCategory.FieldRaid;

        return DutyCategory.OtherDuty;
    }
}
