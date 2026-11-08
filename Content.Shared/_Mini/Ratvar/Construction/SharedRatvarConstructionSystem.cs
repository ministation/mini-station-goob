// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.RPSX.DarkForces.Ratvar.Righteous.Roles;
using Content.Shared.UserInterface;

namespace Content.Shared.RPSX.DarkForces.Ratvar.Construction;

public sealed class SharedRatvarConstructionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RatvarConstructionComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(Entity<RatvarConstructionComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<RatvarRighteousComponent>(args.User))
            args.Cancel();
    }
}
