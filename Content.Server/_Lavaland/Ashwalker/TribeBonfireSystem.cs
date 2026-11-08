// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server.Atmos.EntitySystems;
using Content.Shared._Lavaland.Ashwalker;
using Content.Shared.Atmos.Components;
using Content.Shared.Audio;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Temperature.Components;

namespace Content.Server._Lavaland.Ashwalker;

/// <summary>
/// TG tribal bonfire: lit by a hot item, burns everything on its tile and lights the area.
/// </summary>
public sealed class TribeBonfireSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedIgnitionSourceSystem _ignition = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambience = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;

    /// <summary>
    /// The bonfire is not a full tile, so only entities on the bonfire tile get burned.
    /// </summary>
    private const float BurnRadius = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TribeBonfireComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TribeBonfireComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnMapInit(Entity<TribeBonfireComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent.Owner, TribeBonfireVisuals.Lit, ent.Comp.Lit);

        if (ent.Comp.Lit)
            SetLit(ent, true);
    }

    private void OnInteractUsing(Entity<TribeBonfireComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Lit || !IsHot(args.Used))
            return;

        SetLit(ent, true);
        args.Handled = true;
    }

    private bool IsHot(EntityUid item)
    {
        if (TryComp<FlammableComponent>(item, out var flammable) && flammable.OnFire)
            return true;

        if (TryComp<IgnitionSourceComponent>(item, out var ignition) && ignition.Ignited)
            return true;

        return TryComp<TemperatureComponent>(item, out var temperature)
               && temperature.CurrentTemperature >= 373f;
    }

    private void SetLit(Entity<TribeBonfireComponent> ent, bool lit)
    {
        ent.Comp.Lit = lit;
        _appearance.SetData(ent.Owner, TribeBonfireVisuals.Lit, lit);
        _light.SetEnabled(ent.Owner, lit);
        _ambience.SetAmbience(ent.Owner, lit);
        _ignition.SetIgnited(ent.Owner, lit);

        if (lit)
            EnsureComp<AlwaysHotComponent>(ent.Owner);
        else
            RemComp<AlwaysHotComponent>(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TribeBonfireComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bonfire, out var xform))
        {
            if (!bonfire.Lit)
                continue;

            bonfire.Accumulator += frameTime;
            if (bonfire.Accumulator < bonfire.BurnInterval)
                continue;

            var seconds = bonfire.Accumulator;
            bonfire.Accumulator = 0f;

            foreach (var other in _lookup.GetEntitiesInRange(xform.Coordinates, BurnRadius))
            {
                if (other == uid || !HasComp<FlammableComponent>(other))
                    continue;

                // shortcut: the SS14 flammable system puts fires out in an oxygenless environment, and the
                // Lavaland surface is a vacuum, so the burn tick only sustains fire indoors or in a pressurised room.
                _flammable.AdjustFireStacks(other, bonfire.FireStacksPerSecond * seconds, ignite: true);
            }
        }
    }
}
