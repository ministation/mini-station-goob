// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Aggression;
using Content.Server.DoAfter;
using Content.Shared._Lavaland.BossMechanics;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Drives the molecular accelerators of The Thing arena. A machine pops out of the floor while
/// the boss is invulnerable and overloads it when used, which pushes the boss into the next phase.
/// </summary>
public sealed class ThingAcceleratorSystem : EntitySystem
{
    private static readonly SoundPathSpecifier ShockSound = new("/Audio/Effects/explosion_small3.ogg");

    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ThingBossSystem _thing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThingAcceleratorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ThingAcceleratorComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<ThingAcceleratorComponent, ThingAcceleratorOverloadDoAfterEvent>(OnOverload);
    }

    private void OnMapInit(Entity<ThingAcceleratorComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, ThingAcceleratorVisuals.State, "retracted");
        _physics.SetCanCollide(ent, false);
    }

    private void OnInteractHand(Entity<ThingAcceleratorComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!ent.Comp.Functional)
        {
            _popup.PopupEntity(Loc.GetString("thing-accelerator-broken"), ent, args.User);
            return;
        }

        if (ent.Comp.Busy)
            return;

        if (ent.Comp.Boss is not { } boss
            || !TryComp<ThingBossComponent>(boss, out var thing)
            || !thing.Invulnerable)
        {
            _popup.PopupEntity(Loc.GetString("thing-accelerator-idle"), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.OverloadTime,
            new ThingAcceleratorOverloadDoAfterEvent(), ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        ent.Comp.Busy = true;
        _popup.PopupEntity(Loc.GetString("thing-accelerator-overloading"), ent, args.User);
    }

    private void OnOverload(Entity<ThingAcceleratorComponent> ent, ref ThingAcceleratorOverloadDoAfterEvent args)
    {
        ent.Comp.Busy = false;

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Functional = false;
        _appearance.SetData(ent, ThingAcceleratorVisuals.State, "broken");
        _physics.SetCanCollide(ent, false);

        _audio.PlayPvs(ShockSound, ent);
        Spawn("EffectSparks", Transform(ent).Coordinates);

        if (ent.Comp.Boss is not { } boss || !TryComp<ThingBossComponent>(boss, out var thing))
            return;

        _thing.Overload((boss, thing));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ThingAcceleratorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Functional)
                continue;

            var deployed = comp.Boss is { } boss
                           && TryComp<ThingBossComponent>(boss, out var thing)
                           && thing.Invulnerable;

            if (deployed == comp.Deployed)
                continue;

            comp.Deployed = deployed;
            _appearance.SetData(uid, ThingAcceleratorVisuals.State, deployed ? "deployed" : "retracted");
            _physics.SetCanCollide(uid, deployed);
        }
    }
}
