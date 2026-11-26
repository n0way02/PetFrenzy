using PetFrenzy.Models;

namespace PetFrenzy.Services;

public static class L
{
    public static bool IsPt => Plugin.Config.Language == "pt";

    /// <summary>English first (default), Portuguese second.</summary>
    public static string T(string en, string pt) => IsPt ? pt : en;

    public static string SpeciesName(PetSpecies sp) => sp switch
    {
        PetSpecies.Carbuncle => "Carbuncle",
        PetSpecies.FatCat => "Fat Cat",
        PetSpecies.Moogle => "Moogle",
        PetSpecies.Namazu => "Namazu",
        PetSpecies.FluffyKitty => T("Fluffy Kitty ★", "Gatinha Peluda ★"),
        PetSpecies.ShepherdDog => T("Fluffy Shepherd", "Pastor Alemão"),
        _ => sp.ToString(),
    };

    public static string StageName(PetStage st) => st switch
    {
        PetStage.Egg => T("Egg", "Ovo"),
        PetStage.Baby => T("Baby", "Filhote"),
        PetStage.Young => T("Young", "Jovem"),
        PetStage.Veteran => T("Veteran", "Veterano"),
        PetStage.Legendary => T("Legendary", "Lendário"),
        _ => st.ToString(),
    };

    public static string AccessoryName(PetAccessory acc)
    {
        var info = CosmeticCatalog.GetHat(acc);
        return IsPt ? info.NamePt : info.NameEn;
    }

    public static string OutfitName(PetOutfit outfit)
    {
        var info = CosmeticCatalog.GetOutfit(outfit);
        return IsPt ? info.NamePt : info.NameEn;
    }
}
