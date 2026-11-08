// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Humanoid;
using Content.Shared.Silicons.Borgs.Components;

namespace Content.Shared._Mini.FootWalk;

/// <summary>
/// Ensures every humanoid with feet gets <see cref="FootWalkAnimationComponent"/>.
/// Borg chassis are skipped; IPC keeps the component via its own prototype.
/// </summary>
public sealed class SharedFootWalkAnimationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidAppearanceComponent, MapInitEvent>(OnHumanoidMapInit);
    }

    private void OnHumanoidMapInit(Entity<HumanoidAppearanceComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<BorgChassisComponent>(ent.Owner))
            return;

        EnsureComp<FootWalkAnimationComponent>(ent.Owner);
    }
}
