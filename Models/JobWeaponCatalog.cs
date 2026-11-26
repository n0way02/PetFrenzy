namespace PetFrenzy.Models;

public enum JobWeaponStyle
{
    None = 0,
    // Tanks
    SwordShield,
    Greataxe,
    Greatsword,
    Gunblade,
    // Healers
    WhiteStaff,
    Book,
    StarGlobe,
    Nouliths,
    // Melee DPS
    Fists,
    Lance,
    TwinDaggers,
    Katana,
    Scythe,
    TwinBlades,
    AxeShield, // Beastmaster (BST, Limited Job)
    // Physical Ranged
    Bow,
    Gun,
    Chakrams,
    // Magical Ranged
    BlackRod,
    RapierFocus,
    BlueCane,
    Paintbrush,
    // Crafters (DoH)
    Saw,
    Hammer,
    Knife,
    Needle,
    Potion,
    FryingPan,
    // Gatherers (DoL)
    Pickaxe,
    Hatchet,
    FishingRod,
}

public enum JobCategoryKind
{
    Combat = 0,
    Crafter = 1,
    Gatherer = 2,
}

public readonly record struct JobVisualInfo(
    uint JobId,
    string Abbrev,
    JobWeaponStyle Weapon,
    JobCategoryKind Category);

public static class JobWeaponCatalog
{
    public static JobVisualInfo Resolve(uint classJobId) => classJobId switch
    {
        // Tanks
        1 => new(1, "GLA", JobWeaponStyle.SwordShield, JobCategoryKind.Combat),
        19 => new(19, "PLD", JobWeaponStyle.SwordShield, JobCategoryKind.Combat),
        3 => new(3, "MRD", JobWeaponStyle.Greataxe, JobCategoryKind.Combat),
        21 => new(21, "WAR", JobWeaponStyle.Greataxe, JobCategoryKind.Combat),
        32 => new(32, "DRK", JobWeaponStyle.Greatsword, JobCategoryKind.Combat),
        37 => new(37, "GNB", JobWeaponStyle.Gunblade, JobCategoryKind.Combat),

        // Healers
        6 => new(6, "CNJ", JobWeaponStyle.WhiteStaff, JobCategoryKind.Combat),
        24 => new(24, "WHM", JobWeaponStyle.WhiteStaff, JobCategoryKind.Combat),
        28 => new(28, "SCH", JobWeaponStyle.Book, JobCategoryKind.Combat),
        33 => new(33, "AST", JobWeaponStyle.StarGlobe, JobCategoryKind.Combat),
        40 => new(40, "SGE", JobWeaponStyle.Nouliths, JobCategoryKind.Combat),

        // Melee DPS
        2 => new(2, "PGL", JobWeaponStyle.Fists, JobCategoryKind.Combat),
        20 => new(20, "MNK", JobWeaponStyle.Fists, JobCategoryKind.Combat),
        4 => new(4, "LNC", JobWeaponStyle.Lance, JobCategoryKind.Combat),
        22 => new(22, "DRG", JobWeaponStyle.Lance, JobCategoryKind.Combat),
        29 => new(29, "ROG", JobWeaponStyle.TwinDaggers, JobCategoryKind.Combat),
        30 => new(30, "NIN", JobWeaponStyle.TwinDaggers, JobCategoryKind.Combat),
        34 => new(34, "SAM", JobWeaponStyle.Katana, JobCategoryKind.Combat),
        39 => new(39, "RPR", JobWeaponStyle.Scythe, JobCategoryKind.Combat),
        41 => new(41, "VPR", JobWeaponStyle.TwinBlades, JobCategoryKind.Combat),
        43 => new(43, "BST", JobWeaponStyle.AxeShield, JobCategoryKind.Combat),

        // Physical Ranged DPS
        5 => new(5, "ARC", JobWeaponStyle.Bow, JobCategoryKind.Combat),
        23 => new(23, "BRD", JobWeaponStyle.Bow, JobCategoryKind.Combat),
        31 => new(31, "MCH", JobWeaponStyle.Gun, JobCategoryKind.Combat),
        38 => new(38, "DNC", JobWeaponStyle.Chakrams, JobCategoryKind.Combat),

        // Magical Ranged DPS
        7 => new(7, "THM", JobWeaponStyle.BlackRod, JobCategoryKind.Combat),
        25 => new(25, "BLM", JobWeaponStyle.BlackRod, JobCategoryKind.Combat),
        26 => new(26, "ACN", JobWeaponStyle.Book, JobCategoryKind.Combat),
        27 => new(27, "SMN", JobWeaponStyle.Book, JobCategoryKind.Combat),
        35 => new(35, "RDM", JobWeaponStyle.RapierFocus, JobCategoryKind.Combat),
        36 => new(36, "BLU", JobWeaponStyle.BlueCane, JobCategoryKind.Combat),
        42 => new(42, "PCT", JobWeaponStyle.Paintbrush, JobCategoryKind.Combat),

        // Crafters (DoH)
        8 => new(8, "CRP", JobWeaponStyle.Saw, JobCategoryKind.Crafter),
        9 => new(9, "BSM", JobWeaponStyle.Hammer, JobCategoryKind.Crafter),
        10 => new(10, "ARM", JobWeaponStyle.Hammer, JobCategoryKind.Crafter),
        11 => new(11, "GSM", JobWeaponStyle.Hammer, JobCategoryKind.Crafter),
        12 => new(12, "LTW", JobWeaponStyle.Knife, JobCategoryKind.Crafter),
        13 => new(13, "WVR", JobWeaponStyle.Needle, JobCategoryKind.Crafter),
        14 => new(14, "ALC", JobWeaponStyle.Potion, JobCategoryKind.Crafter),
        15 => new(15, "CUL", JobWeaponStyle.FryingPan, JobCategoryKind.Crafter),

        // Gatherers (DoL)
        16 => new(16, "MIN", JobWeaponStyle.Pickaxe, JobCategoryKind.Gatherer),
        17 => new(17, "BTN", JobWeaponStyle.Hatchet, JobCategoryKind.Gatherer),
        18 => new(18, "FSH", JobWeaponStyle.FishingRod, JobCategoryKind.Gatherer),

        _ => new(0, string.Empty, JobWeaponStyle.None, JobCategoryKind.Combat),
    };
}
