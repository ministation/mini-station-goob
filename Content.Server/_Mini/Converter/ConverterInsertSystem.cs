using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Shared._Mini.Converter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Research.TechnologyDisk.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Converter;

/// <summary>
/// Handles inserting technology disks into <see cref="ConverterComponent"/> consoles.
/// </summary>
public sealed class ConverterInsertSystem : EntitySystem
{
    private static readonly EntProtoId TelecrystalPrototype = "Telecrystal1";
    private static readonly ProtoId<Content.Shared.Random.WeightedRandomPrototype> RareDiskWeights = "RareTechDiskTierWeights";

    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConverterComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<ConverterComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<TechnologyDiskComponent>(args.Used, out var disk))
            return;

        if (ent.Comp.PointsPerTelecrystal <= 0)
        {
            _popup.PopupEntity(Loc.GetString("mini-converter-examine-disabled"), ent, args.User);
            return;
        }

        if (!this.IsPowered(ent.Owner, EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("mini-converter-unpowered"), ent, args.User);
            return;
        }

        var value = disk.TierWeightPrototype == RareDiskWeights
            ? ent.Comp.RareTechnologyDiskPoints
            : ent.Comp.TechnologyDiskPoints;

        if (value <= 0)
            return;

        args.Handled = true;
        QueueDel(args.Used);
        InsertDiskValue(ent, value);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg"), ent);
        _popup.PopupEntity(Loc.GetString("mini-converter-disk-accepted", ("points", value)), ent, args.User);
    }

    /// <summary>
    /// Adds disk points to the converter and spawns telecrystals when fully charged.
    /// </summary>
    public bool InsertDiskValue(Entity<ConverterComponent> ent, int value)
    {
        if (value <= 0 || ent.Comp.PointsPerTelecrystal <= 0)
            return false;

        ent.Comp.StoredPoints += value;

        var payout = ent.Comp.StoredPoints / ent.Comp.PointsPerTelecrystal;
        ent.Comp.StoredPoints %= ent.Comp.PointsPerTelecrystal;

        if (payout <= 0)
            return true;

        var coords = Transform(ent).Coordinates;
        var telecrystalStack = Spawn(TelecrystalPrototype, coords);
        _stack.SetCount(telecrystalStack, payout);
        _stack.TryMergeToContacts(telecrystalStack);
        return true;
    }
}
