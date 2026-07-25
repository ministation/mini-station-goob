using System.Linq;
using Content.Shared.Genetics;
using Content.Shared.Genetics.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Server.Genetics.System;

/// <summary>
/// Mini port of Wega EnsureMarking — uses HumanoidAppearanceComponent.MarkingSet (no VisualBody).
/// </summary>
public sealed partial class EnsureMarkingSystem : EntitySystem
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;

    public static readonly ProtoId<MarkingPrototype> DefaultHorns = "LizardHornsDemonic";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnsureHornsGenComponent, ComponentInit>(OnHornsInit);
        SubscribeLocalEvent<EnsureHornsGenComponent, ComponentShutdown>(OnHornsShutdown);
    }

    private void OnHornsInit(Entity<EnsureHornsGenComponent> ent, ref ComponentInit args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        humanoid.MarkingSet.AddBack(MarkingCategories.HeadTop, new Marking(DefaultHorns, new List<Color> { Color.Black }));
        Dirty(ent.Owner, humanoid);
    }

    private void OnHornsShutdown(Entity<EnsureHornsGenComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        humanoid.MarkingSet.RemoveCategory(MarkingCategories.HeadTop);
        Dirty(ent.Owner, humanoid);
    }

    public void UpdateMarkingCategory(
        EntityUid ent,
        HumanoidVisualLayers layer,
        string[] colorR, string[] colorG, string[] colorB,
        string[] style, string species,
        List<MarkingPrototypeInfo> markingPrototypes,
        string[]? secondaryColorR = null,
        string[]? secondaryColorG = null,
        string[]? secondaryColorB = null)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        var category = LayerToCategory(layer);
        humanoid.MarkingSet.RemoveCategory(category);

        if (style.All(c => c == "0"))
        {
            Dirty(ent, humanoid);
            return;
        }

        if (layer == HumanoidVisualLayers.HeadTop && HasComp<EnsureHornsGenComponent>(ent))
        {
            humanoid.MarkingSet.AddBack(category, new Marking(DefaultHorns, new List<Color> { Color.Black }));
            Dirty(ent, humanoid);
            return;
        }

        var bestMatch = FindBestMatchingMarking(style, species, markingPrototypes);
        if (bestMatch == null)
            return;

        string redHex = colorR[0] + colorR[1];
        string greenHex = colorG[0] + colorG[1];
        string blueHex = colorB[0] + colorB[1];
        var mainColor = new Color(
            Convert.ToInt32(redHex, 16) / 255f,
            Convert.ToInt32(greenHex, 16) / 255f,
            Convert.ToInt32(blueHex, 16) / 255f);
        var colors = new List<Color> { mainColor };

        if (layer == HumanoidVisualLayers.Hair &&
            secondaryColorR != null && secondaryColorG != null && secondaryColorB != null)
        {
            var secondaryColor = new Color(
                Convert.ToInt32(secondaryColorR[0] + secondaryColorR[1], 16) / 255f,
                Convert.ToInt32(secondaryColorG[0] + secondaryColorG[1], 16) / 255f,
                Convert.ToInt32(secondaryColorB[0] + secondaryColorB[1], 16) / 255f);
            colors.Add(secondaryColor);
        }

        humanoid.MarkingSet.AddBack(category, new Marking(bestMatch.MarkingPrototypeId, colors));
        Dirty(ent, humanoid);
    }

    private static MarkingCategories LayerToCategory(HumanoidVisualLayers layer) => layer switch
    {
        HumanoidVisualLayers.Hair => MarkingCategories.Hair,
        HumanoidVisualLayers.FacialHair => MarkingCategories.FacialHair,
        HumanoidVisualLayers.HeadTop => MarkingCategories.HeadTop,
        HumanoidVisualLayers.Head => MarkingCategories.Head,
        HumanoidVisualLayers.Chest => MarkingCategories.Chest,
        HumanoidVisualLayers.Tail => MarkingCategories.Tail,
        _ => MarkingCategories.Overlay
    };

    private MarkingPrototypeInfo? FindBestMatchingMarking(string[] style, string species, List<MarkingPrototypeInfo> markingPrototypes)
    {
        MarkingPrototypeInfo? bestMatch = null;
        int bestScore = int.MaxValue;

        foreach (var marking in markingPrototypes)
        {
            if (!string.IsNullOrEmpty(marking.Groups) && !marking.Groups.Contains(species))
                continue;

            int score = CalculateStyleMatchScore(marking.HexValue, style);
            if (score < bestScore)
            {
                bestScore = score;
                bestMatch = marking;
            }
        }

        return bestMatch;
    }

    private int CalculateStyleMatchScore(string[] markingStyle, string[] targetStyle)
    {
        int score = 0;
        for (int i = 0; i < markingStyle.Length; i++)
        {
            if (i >= targetStyle.Length)
                break;

            int markingValue = Convert.ToInt32(markingStyle[i], 16);
            int targetValue = Convert.ToInt32(targetStyle[i], 16);
            score += Math.Abs(markingValue - targetValue);
        }
        return score;
    }
}
