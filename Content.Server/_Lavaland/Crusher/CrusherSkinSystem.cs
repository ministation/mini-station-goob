// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server.Popups;
using Content.Shared._Lavaland.Weapons.CrusherSkin;
using Content.Shared.Verbs;

namespace Content.Server._Lavaland.Crusher;

public sealed class CrusherSkinSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrusherSkinComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<CrusherSkinComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Skins.Count < 2)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("crusher-skin-verb"),
            Act = () => CycleSkin(ent, user),
        });
    }

    private void CycleSkin(Entity<CrusherSkinComponent> ent, EntityUid user)
    {
        ent.Comp.Index = (ent.Comp.Index + 1) % ent.Comp.Skins.Count;
        Dirty(ent.Owner, ent.Comp);
        _popup.PopupEntity(Loc.GetString("crusher-skin-changed", ("skin", Loc.GetString(ent.Comp.Skins[ent.Comp.Index].Name))), ent.Owner, user);
    }
}
