// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Devil;
using Content.Goobstation.Shared.Religion;
using Content.Shared._DV.CosmicCult.Components;
using Content.Shared.Heretic;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.RPSX.DarkForces.Ratvar.Righteous.Roles;
using Content.Shared.WhiteDream.BloodCult.BloodCultist;
using Content.Shared.WhiteDream.BloodCult.Constructs;

namespace Content.Server._Mini.Religion;

/// <summary>
/// Unholy antagonists take holy damage. WeakToHoly without AlwaysTakeHoly zeroes Holy.
/// </summary>
public sealed class CultWeakToHolySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodCultistComponent, ComponentStartup>(OnBloodCultist);
        SubscribeLocalEvent<ConstructComponent, ComponentStartup>(OnConstruct);
        SubscribeLocalEvent<RatvarRighteousComponent, ComponentStartup>(OnRatvar);
        SubscribeLocalEvent<CosmicCultComponent, ComponentStartup>(OnCosmic);
        SubscribeLocalEvent<DevilComponent, ComponentStartup>(OnDevil);
        SubscribeLocalEvent<HereticComponent, MindGotAddedEvent>(OnHereticMindAdded);
    }

    private void OnBloodCultist(Entity<BloodCultistComponent> ent, ref ComponentStartup args) => EnsureTakeHoly(ent);
    private void OnConstruct(Entity<ConstructComponent> ent, ref ComponentStartup args) => EnsureTakeHoly(ent);
    private void OnRatvar(Entity<RatvarRighteousComponent> ent, ref ComponentStartup args) => EnsureTakeHoly(ent);
    private void OnCosmic(Entity<CosmicCultComponent> ent, ref ComponentStartup args) => EnsureTakeHoly(ent);
    private void OnDevil(Entity<DevilComponent> ent, ref ComponentStartup args) => EnsureTakeHoly(ent);

    private void OnHereticMindAdded(Entity<HereticComponent> ent, ref MindGotAddedEvent args)
    {
        if (TerminatingOrDeleted(args.Container) || !HasComp<MobStateComponent>(args.Container))
            return;

        EnsureTakeHoly(args.Container);
    }

    private void EnsureTakeHoly(EntityUid uid)
    {
        EnsureComp<WeakToHolyComponent>(uid).AlwaysTakeHoly = true;
    }
}
