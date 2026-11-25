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
        _ => sp.ToString(),
    };

    public static string StageName(PetStage st) => st switch
    {
        PetStage.Egg => T("Egg", "Ovo"),
        PetStage.Baby => T("Baby", "Filhote"),
        PetStage.Young => T("Young", "Jovem"),
        PetStage.Legendary => T("Legendary", "Lendário"),
        _ => st.ToString(),
    };

    public static string AccessoryName(PetAccessory acc) => acc switch
    {
        PetAccessory.None => T("None", "Nenhum"),
        PetAccessory.Sunglasses => T("Cool Shades", "Óculos Escuros"),
        PetAccessory.PartyHat => T("Party Hat", "Chapéu de Festa"),
        PetAccessory.RoyalCrown => T("Royal Crown", "Coroa Real"),
        PetAccessory.GoldenHalo => T("Golden Halo", "Auréola Dourada"),
        PetAccessory.WizardHat => T("n0way Wizard Hat", "Chapéu Mago n0way"),
        _ => acc.ToString(),
    };
}
