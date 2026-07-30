// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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
