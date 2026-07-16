using Content.Shared._Mini.AntagTokens;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI.Roles;

/// <summary>
/// Resolves lobby antagonist card icons from the antag token catalog and fallbacks.
/// </summary>
public static class AntagLobbyIcons
{
    private static readonly Dictionary<string, string> ExtraPaths = new()
    {
        ["TraitorSleeper"] = "/Textures/_Mini/Interface/Antags/traitor.png",
        ["CorporateAgent"] = "/Textures/_Mini/Interface/Antags/traitor.png",
        ["Nukeops"] = "/Textures/_Mini/Interface/Antags/nukie.png",
        ["NukeopsMedic"] = "/Textures/_Mini/Interface/Antags/nukie.png",
        ["NukeopsCommander"] = "/Textures/_Mini/Interface/Antags/nukie.png",
        ["Blob"] = "/Textures/_Mini/Interface/Antags/blob.png",
        ["Wizard"] = "/Textures/_Mini/Interface/Antags/wizard.png",
    };

    public static bool TryResolveIconPath(AntagPrototype antag, out string path)
    {
        var listings = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AntagTokenListingSystem>();

        foreach (var listing in listings.ListingsOrdered)
        {
            if (listing.AntagId == antag.ID)
            {
                path = listing.IconPath;
                return true;
            }
        }

        if (ExtraPaths.TryGetValue(antag.ID, out path!))
            return true;

        path = string.Empty;
        return false;
    }

    public static bool TryResolveFallbackSprite(AntagPrototype antag, out SpriteSpecifier sprite)
    {
        if (antag.ID == "BloodCultist")
        {
            sprite = new SpriteSpecifier.Rsi(
                new ResPath("/Textures/WhiteDream/BloodCult/cult_hud.rsi"),
                "cult_member");
            return true;
        }

        if (antag.ID is "Xenoborg" or "MothershipCore")
        {
            sprite = antag.ID == "MothershipCore"
                ? new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Mobs/Silicon/mothership_core.rsi"),
                    "core-idle")
                : new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Mobs/Silicon/chassis.rsi"),
                    "xenoborg_heavy");
            return true;
        }

        sprite = default!;
        return false;
    }
}
