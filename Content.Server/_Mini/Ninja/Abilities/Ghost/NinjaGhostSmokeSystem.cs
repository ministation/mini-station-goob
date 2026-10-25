// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.Fluids.EntitySystems;
using Content.Server.Spreader;
using Content.Shared.Actions;
using Content.Shared._Mini.Ninja.Abilities.Ghost;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Actions.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Server._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Ghost tier 1: the smoke screen. The ninja can deploy a cloud of smoke, and can arm an automatic
/// mode that deploys a weaker cloud on a few other path abilities.
/// </summary>
public sealed class NinjaGhostSmokeSystem : EntitySystem
{
    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly SpreaderSystem _spreader = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaGhostSmokeComponent, NinjaGhostSmokeEvent>(OnSmoke);
        SubscribeLocalEvent<NinjaGhostSmokeComponent, NinjaGhostSmokeAutoToggleEvent>(OnToggleAuto);
    }

    private void OnSmoke(Entity<NinjaGhostSmokeComponent> ent, ref NinjaGhostSmokeEvent args)
    {
        if (args.Handled || !IsWearer(ent.Owner, args.Performer))
            return;

        if (TrySpawnSmoke(ent, args.Performer, autoMode: false))
            args.Handled = true;
    }

    private void OnToggleAuto(Entity<NinjaGhostSmokeComponent> ent, ref NinjaGhostSmokeAutoToggleEvent args)
    {
        if (args.Handled || !IsWearer(ent.Owner, args.Performer))
            return;

        ent.Comp.AutoMode = !ent.Comp.AutoMode;
        Dirty(ent);
        _actions.SetToggled(args.Action.AsNullable(), ent.Comp.AutoMode);
        args.Handled = true;
    }

    /// <summary>
    /// Whether the given uid is the current wearer of the suit the smoke module lives on.
    /// </summary>
    public bool IsWearer(EntityUid suit, EntityUid user)
    {
        return Transform(suit).ParentUid == user;
    }

    /// <summary>
    /// Deploys the weakened automatic cloud if the automatic mode is armed and the suit has enough charge.
    /// Used by the other path abilities (cloak, clones, emergency teleport, caltrops).
    /// </summary>
    public void TryAutoSmoke(EntityUid suitUid)
    {
        if (!TryComp<NinjaGhostSmokeComponent>(suitUid, out var comp))
            return;

        if (!comp.AutoMode)
            return;

        var wearer = Transform(suitUid).ParentUid;
        if (!wearer.IsValid())
            return;

        TrySpawnSmoke((suitUid, comp), wearer, autoMode: true);
    }

    private bool TrySpawnSmoke(Entity<NinjaGhostSmokeComponent> ent, EntityUid user, bool autoMode)
    {
        var (suit, comp) = ent;

        if (autoMode && !comp.AutoMode)
            return false;

        var duration = autoMode ? comp.AutoModeDuration : comp.Duration;
        var spread = autoMode ? comp.AutoModeSpreadAmount : comp.SpreadAmount;
        var cost = autoMode ? comp.EnergyCostAutoMode : comp.EnergyCost;

        // The cloud needs a floor to spread on.
        var xform = Transform(user);
        var mapCoords = _xform.GetMapCoordinates(user, xform);
        if (!_map.TryFindGridAt(mapCoords, out var gridUid, out var gridComp) ||
            !_map.TryGetTileRef(gridUid, gridComp, xform.Coordinates, out var tileRef) ||
            tileRef.Tile.IsEmpty)
        {
            return false;
        }

        if (_spreader.RequiresFloorToSpread(new EntProtoId<EdgeSpreaderComponent>(comp.SmokePrototype.Id)) && _turf.IsSpace(tileRef))
            return false;

        if (!_suitPower.HasCharge(user, cost))
        {
            // Automatic mode never shows the no power popup, it just skips the cloud.
            if (!autoMode)
                _suitPower.PopupNoPower(user);

            return false;
        }

        if (!_suitPower.TryUseCharge(user, cost))
            return false;

        var coords = _map.MapToGrid(gridUid, mapCoords);
        var smoke = Spawn(comp.SmokePrototype, coords.SnapToGrid());
        if (!TryComp<SmokeComponent>(smoke, out var smokeComp))
        {
            QueueDel(smoke);
            return false;
        }

        _audio.PlayPvs(comp.SmokeSound, user);
        _smoke.StartSmoke(smoke, new Solution(), (float) duration.TotalSeconds, spread, smokeComp);
        return true;
    }

}