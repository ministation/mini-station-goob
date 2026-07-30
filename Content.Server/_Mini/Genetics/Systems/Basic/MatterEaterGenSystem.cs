using Content.Shared.Genetics;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Genetics.System;

/// <summary>
/// Mini stub of Wega Matter Eater gene — deletes the do-after target (food/items).
/// </summary>
public sealed partial class MatterEaterGenSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MatterEaterGenComponent, MatterEaterDoAfterEvent>(OnDoAfter);
    }

    private void OnDoAfter(Entity<MatterEaterGenComponent> ent, ref MatterEaterDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return;

        QueueDel(target);
        _audio.PlayPvs("/Audio/Items/eatfood.ogg", ent);
        _popup.PopupEntity(Loc.GetString("genetics-matter-eater-nom", ("entity", target)), ent, ent);
    }
}
