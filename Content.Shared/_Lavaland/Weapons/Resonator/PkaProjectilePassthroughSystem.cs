// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Humanoid;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Lavaland.Weapons.Resonator;

/// <summary>
/// Handles projectile modkit collision changes on both client and server.
/// </summary>
public sealed class PkaProjectilePassthroughSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PkaProjectilePassthroughComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<PkaProjectilePassthroughComponent> ent, ref PreventCollideEvent args)
    {
        if (HasComp<HumanoidAppearanceComponent>(args.OtherEntity))
            args.Cancelled = true;
    }
}
