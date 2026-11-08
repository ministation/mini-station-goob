// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.DoAfter;
using Content.Shared._Lavaland.BossMechanics;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;

namespace Content.Server._Lavaland.Megafauna;

/// <summary>
/// TG parity for <see cref="HierophantClubBeaconComponent"/>.
/// </summary>
public sealed class HierophantClubBeaconSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionsComponent, HierophantClubBeaconActionEvent>(OnAction);
        SubscribeLocalEvent<HierophantClubBeaconComponent, HierophantClubDeployDoAfterEvent>(OnDeployDone);
        SubscribeLocalEvent<HierophantClubBeaconComponent, HierophantClubTeleportDoAfterEvent>(OnTeleportDone);
        SubscribeLocalEvent<HierophantBeaconComponent, AttackedEvent>(OnBeaconAttacked);
        SubscribeLocalEvent<HierophantClubBeaconComponent, EntityTerminatingEvent>(OnBeaconTerminating);
    }

    private void OnAction(Entity<ActionsComponent> user, ref HierophantClubBeaconActionEvent args)
    {
        if (args.Handled)
            return;

        Entity<HierophantClubBeaconComponent>? club = null;
        foreach (var held in _hands.EnumerateHeld(user.Owner))
        {
            if (TryComp<HierophantClubBeaconComponent>(held, out var clubComp))
            {
                club = (held, clubComp);
                break;
            }
        }

        if (club == null || club.Value.Comp.Teleporting)
            return;

        args.Handled = true;

        if (club.Value.Comp.Beacon == null || !Exists(club.Value.Comp.Beacon))
        {
            StartDeploy(club.Value, user);
            return;
        }

        var beacon = club.Value.Comp.Beacon.Value;
        var userPos = _xform.GetMapCoordinates(user);
        var beaconPos = _xform.GetMapCoordinates(beacon);
        if (userPos.MapId != beaconPos.MapId)
            return;

        var distance = (userPos.Position - beaconPos.Position).Length();
        if (distance < club.Value.Comp.MinBeaconDistance)
            return;

        club.Value.Comp.Teleporting = true;
        _audio.PlayPvs(club.Value.Comp.DepartSound, user);

        var doAfter = new DoAfterArgs(
            EntityManager, user, club.Value.Comp.TeleportDuration, new HierophantClubTeleportDoAfterEvent(), club.Value)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void StartDeploy(Entity<HierophantClubBeaconComponent> club, Entity<ActionsComponent> user)
    {
        var userCoords = Transform(user).Coordinates;
        if (_xform.GetGrid(userCoords) == null)
            return;

        var doAfter = new DoAfterArgs(
            EntityManager, user, club.Comp.DeployDuration, new HierophantClubDeployDoAfterEvent(), club)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _audio.PlayPvs(club.Comp.DeploySound, user);
    }

    private void OnDeployDone(Entity<HierophantClubBeaconComponent> club, ref HierophantClubDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || club.Comp.Beacon != null)
            return;

        args.Handled = true;
        var user = args.User;
        if (_xform.GetGrid(Transform(user).Coordinates) == null)
            return;

        club.Comp.Beacon = Spawn("LavalandHierophantBeacon", Transform(user).Coordinates);
        _audio.PlayPvs(club.Comp.DeploySound, club.Comp.Beacon.Value);
    }

    private void OnTeleportDone(Entity<HierophantClubBeaconComponent> club, ref HierophantClubTeleportDoAfterEvent args)
    {
        club.Comp.Teleporting = false;

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (club.Comp.Beacon == null || !Exists(club.Comp.Beacon))
            return;

        var beacon = club.Comp.Beacon.Value;
        var user = args.User;
        var userCoords = Transform(user).Coordinates;
        if (_xform.GetGrid(userCoords) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var beaconCoords = Transform(beacon).Coordinates;
        if (_xform.GetGrid(beaconCoords) is not { } beaconGridUid
            || !TryComp<MapGridComponent>(beaconGridUid, out var beaconGrid))
        {
            return;
        }

        var userTile = _map.LocalToTile(gridUid, grid, userCoords);
        var beaconTile = _map.LocalToTile(beaconGridUid, beaconGrid, beaconCoords);
        _audio.PlayPvs(club.Comp.DepartSound, user);
        _audio.PlayPvs(club.Comp.DeploySound, beaconCoords);

        // TG parity: everyone standing near the user fades over to the beacon, each
        // landing on the tile that mirrors their old offset around the beacon.
        foreach (var victim in _lookup.GetEntitiesInRange(userCoords, club.Comp.TeleportRange))
        {
            if (!HasComp<MobStateComponent>(victim) || !_mobState.IsAlive(victim))
                continue;

            var victimTile = _map.LocalToTile(gridUid, grid, Transform(victim).Coordinates);
            var offset = victimTile - userTile;
            var targetTile = beaconTile + offset;
            _xform.SetCoordinates(victim, _map.GridTileToLocal(beaconGridUid, beaconGrid, targetTile));
            _audio.PlayPvs(club.Comp.DeploySound, victim);
        }
    }

    // TG parity: striking the beacon with the club collects it back. The fork's
    // AttackedEvent has no target, so the beacon side listens for the club's hits.
    private void OnBeaconAttacked(Entity<HierophantBeaconComponent> beacon, ref AttackedEvent args)
    {
        if (!TryComp<HierophantClubBeaconComponent>(args.Used, out var club)
            || club.Beacon != beacon.Owner)
        {
            return;
        }

        QueueDel(beacon.Owner);
        club.Beacon = null;
    }

    private void OnBeaconTerminating(Entity<HierophantClubBeaconComponent> club, ref EntityTerminatingEvent args)
    {
        if (club.Comp.Beacon != args.Entity)
            return;

        club.Comp.Beacon = null;
    }
}
