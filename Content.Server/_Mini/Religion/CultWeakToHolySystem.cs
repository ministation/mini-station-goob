// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Devil;
using Content.Goobstation.Shared.Religion;
using Content.Shared.RPSX.DarkForces.Ratvar.Righteous.Roles;
using Content.Shared.WhiteDream.BloodCult.BloodCultist;
using Content.Shared.WhiteDream.BloodCult.Constructs;

namespace Content.Server._Mini.Religion;

/// <summary>
/// Unholy antagonists take holy damage. WeakToHoly without AlwaysTakeHoly zeroes Holy.
/// Cosmic cult / heretics: do not subscribe to their ComponentStartup/MindGotAdded —
/// those event slots are already taken; they get AlwaysTakeHoly via YAML / WeakToHolySystem.
/// </summary>
public sealed class CultWeakToHolySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodCultistComponent, ComponentStartup>(OnBloodCultist);
        SubscribeLocalEvent<ConstructComponent, ComponentStartup>(OnConstruct);
        SubscribeLocalEvent<RatvarRighteousComponent, ComponentStartup>(OnRatvar);
        SubscribeLocalEvent<DevilComponent, ComponentStartup>(OnDevil);
    }

    private void OnBloodCultist(Entity<BloodCultistComponent> ent, ref ComponentStartup args) => EnsureTakeHoly(ent);
    private void OnConstruct(Entity<ConstructComponent> ent, ref ComponentStartup args) => EnsureTakeHoly(ent);
    private void OnRatvar(Entity<RatvarRighteousComponent> ent, ref ComponentStartup args) => EnsureTakeHoly(ent);
    private void OnDevil(Entity<DevilComponent> ent, ref ComponentStartup args) => EnsureTakeHoly(ent);

    private void EnsureTakeHoly(EntityUid uid)
    {
        EnsureComp<WeakToHolyComponent>(uid).AlwaysTakeHoly = true;
    }
}
