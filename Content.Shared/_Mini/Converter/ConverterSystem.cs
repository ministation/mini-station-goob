using System;
using Content.Shared.Examine;

namespace Content.Shared._Mini.Converter;

public sealed class ConverterSystem : EntitySystem
{
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

        args.PushMarkup(Loc.GetString("mini-converter-examine-progress",
            ("current", progress),
            ("needed", required)));
        args.PushMarkup(Loc.GetString("mini-converter-examine-disks",
            ("regular", regular),
            ("rare", rare)));
    }
}
