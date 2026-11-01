// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Lavaland.Weapons.Lava;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._Lavaland.Weapons;

/// <summary>
/// Staff of lava: summons or removes lava pools around the user.
/// </summary>
public sealed class StaffOfLavaSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const string LavaPool = "LavalandLavaPool";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StaffOfLavaActionEvent>(OnAction);
    }

    private void OnAction(StaffOfLavaActionEvent args)
    {
        if (args.Handled)
            return;

        Entity<StaffOfLavaComponent>? staff = null;
        foreach (var held in _hands.EnumerateHeld(args.Performer))
        {
            if (TryComp<StaffOfLavaComponent>(held, out var staffComp))
            {
                staff = (held, staffComp);
                break;
            }
        }

        if (staff == null)
            return;

        args.Handled = true;
        var user = args.Performer;
        var comp = staff.Value.Comp;
        var coords = Transform(user).Coordinates;

        // Second use: dispel nearby pools.
        var pools = _lookup.GetEntitiesInRange<TransformComponent>(coords, comp.Radius + 1f);
        var removed = false;
        foreach (var pool in pools)
        {
            if (pool.Owner == user)
                continue;
            if (!TryComp<MetaDataComponent>(pool, out var meta) || meta.EntityPrototype?.ID != LavaPool)
                continue;

            QueueDel(pool);
            removed = true;
        }

        if (removed)
        {
            _popup.PopupEntity(Loc.GetString("staff-of-lava-removed"), staff.Value, user);
            _audio.PlayPvs("/Audio/Effects/Fluids/splat.ogg", staff.Value, AudioParams.Default.WithVolume(-4f));
            return;
        }

        // First use: a ring of lava around the user. The user should run out of it.
        var ring = 8;
        for (var i = 0; i < ring; i++)
        {
            var angle = i * (MathF.PI * 2f / ring);
            var offset = new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * comp.Radius;
            Spawn(LavaPool, coords.Offset(offset));
        }

        _popup.PopupEntity(Loc.GetString("staff-of-lava-summoned"), staff.Value, user);
        _audio.PlayPvs("/Audio/Effects/Fluids/splat.ogg", staff.Value, AudioParams.Default.WithVolume(0f));
    }
}
