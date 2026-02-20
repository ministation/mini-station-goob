using System;
using Content.Shared.Examine;

namespace Content.Shared._Mini.Converter;

public sealed class ConverterSystem : EntitySystem
{
    private const string LowProgressColor = "#D65C5C";
    private const string MediumProgressColor = "#E0B844";
    private const string HighProgressColor = "#6BBE4D";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConverterComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<ConverterComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.PointsPerTelecrystal <= 0)
        {
            args.PushMarkup(Loc.GetString("mini-converter-examine-disabled"));
            return;
        }

        var required = ent.Comp.PointsPerTelecrystal;
        var progress = Math.Clamp(ent.Comp.StoredPoints, 0, required);
        var remaining = Math.Max(0, required - progress);

        var regular = ent.Comp.TechnologyDiskPoints > 0
            ? (int) Math.Ceiling(remaining / (double) ent.Comp.TechnologyDiskPoints)
            : 0;
        var rare = ent.Comp.RareTechnologyDiskPoints > 0
            ? (int) Math.Ceiling(remaining / (double) ent.Comp.RareTechnologyDiskPoints)
            : 0;

        var color = GetProgressColor(progress, required);
        var progressValue = $"[color={color}]{progress}/{required}[/color]";
        args.PushMarkup($"{Loc.GetString("mini-converter-examine-progress-prefix")} {progressValue}.");
        args.PushMarkup(Loc.GetString("mini-converter-examine-disks",
            ("regular", regular),
            ("rare", rare)));
    }

    private static string GetProgressColor(int progress, int required)
    {
        if (required <= 0)
            return LowProgressColor;

        var ratio = progress / (float) required;

        if (ratio >= 0.80f)
            return HighProgressColor;

        if (ratio >= 0.40f)
            return MediumProgressColor;

        return LowProgressColor;
    }
}
