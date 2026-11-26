using System.Collections.Generic;
using System.Numerics;

namespace PetFrenzy.Models;

public enum WeaponSkin
{
    Default = 0,
    UCoB = 1,
    UWU = 2,
    TEA = 3,
    DSR = 4,
    TOP = 5,
    FRU = 6,
}

public sealed record WeaponSkinInfo(
    WeaponSkin Skin,
    string ShortTag,
    string NameEn,
    string NamePt,
    string ThemeEn,
    string ThemePt,
    int RequiredLevel,
    int CostCoins,
    Vector4 MetalColor,
    Vector4 AccentColor,
    Vector4 GlowPrimary,
    Vector4 GlowSecondary);

public static class WeaponSkinCatalog
{
    public static readonly WeaponSkinInfo[] All =
    [
        new(
            WeaponSkin.Default, "STD",
            "Standard Armory", "Armamento Padrão",
            "Classic job weapon & tool colors.", "Cores clássicas da sua Job.",
            1, 0,
            new(0.86f, 0.88f, 0.94f, 1f),
            new(1.00f, 0.90f, 0.00f, 1f),
            new(0f, 0f, 0f, 0f),
            new(0f, 0f, 0f, 0f)),

        new(
            WeaponSkin.UCoB, "FIRE",
            "Solar Flare Aura", "Aura Chama Solar",
            "Dark gunmetal & gold with blazing golden-amber flames and embers.",
            "Metal escuro e ouro com chamas douradas e brasas flutuantes.",
            15, 1000,
            new(0.28f, 0.26f, 0.30f, 1f),
            new(1.00f, 0.78f, 0.16f, 1f),
            new(1.00f, 0.78f, 0.12f, 1f),
            new(1.00f, 0.48f, 0.08f, 1f)),

        new(
            WeaponSkin.UWU, "AETH",
            "Aetherlight Aura", "Aura Luz Etérea",
            "Polished silver with radiant white & soft sky-blue light sparkles.",
            "Prata polida com brilho branco radiante e faíscas azul-celeste.",
            30, 1800,
            new(0.92f, 0.96f, 1.00f, 1f),
            new(0.62f, 0.86f, 1.00f, 1f),
            new(0.94f, 0.98f, 1.00f, 1f),
            new(0.48f, 0.82f, 1.00f, 1f)),

        new(
            WeaponSkin.TEA, "VOLT",
            "Clockwork Volt Aura", "Aura Engrenagem Volt",
            "Sleek steel with electric blue glow & golden-orange spinning gears.",
            "Aço polido com brilho azul elétrico e engrenagens douradas girando.",
            45, 2900,
            new(0.68f, 0.75f, 0.84f, 1f),
            new(1.00f, 0.68f, 0.14f, 1f),
            new(0.15f, 0.74f, 1.00f, 1f),
            new(1.00f, 0.64f, 0.10f, 1f)),

        new(
            WeaponSkin.DSR, "RUNE",
            "Celestial Rune Aura", "Aura Runa Celestial",
            "Regal silver & white with holy sky-blue and gold orbiting diamond runes.",
            "Prata real com runas losango azul-celeste e douradas orbitando.",
            60, 4400,
            new(0.90f, 0.93f, 0.98f, 1f),
            new(1.00f, 0.86f, 0.34f, 1f),
            new(0.45f, 0.80f, 1.00f, 1f),
            new(1.00f, 0.88f, 0.38f, 1f)),

        new(
            WeaponSkin.TOP, "NEON",
            "Cyber Neon Aura", "Aura Cyber Neon",
            "Matte black frame with neon magenta, violet & laser-red glitch lines.",
            "Preto fosco com linhas neon magenta, roxo e vermelho laser.",
            75, 6500,
            new(0.15f, 0.13f, 0.20f, 1f),
            new(1.00f, 0.20f, 0.42f, 1f),
            new(0.96f, 0.20f, 0.68f, 1f),
            new(1.00f, 0.18f, 0.28f, 1f)),

        new(
            WeaponSkin.FRU, "PRSM",
            "Astral Prism Aura", "Aura Prisma Astral",
            "Crystalline dual-tone: icy white-cyan light rings & deep void violet aura.",
            "Cristal duplo: anéis de luz branco-gelo e aura roxa profunda.",
            90, 9500,
            new(0.84f, 0.90f, 0.98f, 1f),
            new(0.58f, 0.32f, 0.98f, 1f),
            new(0.88f, 0.96f, 1.00f, 1f),
            new(0.48f, 0.24f, 0.96f, 1f)),
    ];

    private static readonly Dictionary<WeaponSkin, WeaponSkinInfo> Map = BuildMap();

    private static Dictionary<WeaponSkin, WeaponSkinInfo> BuildMap()
    {
        var d = new Dictionary<WeaponSkin, WeaponSkinInfo>(All.Length);
        foreach (var info in All) d[info.Skin] = info;
        return d;
    }

    public static WeaponSkinInfo Get(WeaponSkin skin)
        => Map.TryGetValue(skin, out var info) ? info : All[0];
}
