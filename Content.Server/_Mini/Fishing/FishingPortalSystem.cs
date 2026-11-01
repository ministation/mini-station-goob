// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.Emag.Systems;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// The fish-porter 3000: lets the player pick any fishing source and fish right at the machine.
/// </summary>
public sealed partial class FishingPortalSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingPortalComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<FishingPortalComponent, FishingPortalSelectMessage>(OnSelect);
        SubscribeLocalEvent<FishingPortalComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FishingPortalComponent, GotEmaggedEvent>(OnEmagged);
    }

    private void OnEmagged(Entity<FishingPortalComponent> ent, ref GotEmaggedEvent args)
    {
        if (ent.Comp.Emagged)
            return;

        // tg unlocks the syndicate dimension when the fish-porter is emagged.
        ent.Comp.Emagged = true;
        Dirty(ent.Owner, ent.Comp);
        args.Handled = true;
    }

    private void OnGetVerbs(Entity<FishingPortalComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (args.Using is not { } fish || !HasComp<FishComponent>(fish))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("fishing-portal-release-verb"),
            Act = () => ReleaseFish(ent.Owner, fish, user),
        });
    }

    private void ReleaseFish(EntityUid portal, EntityUid fish, EntityUid user)
    {
        if (!Exists(fish))
            return;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Fluids/watersplash.ogg"), portal);
        _popup.PopupEntity(
            Loc.GetString("fishing-portal-release-done", ("fish", Name(fish)), ("target", Name(portal))),
            user, user);
        QueueDel(fish);
    }

    private void OnUiOpened(Entity<FishingPortalComponent> ent, ref BoundUIOpenedEvent args)
    {
        _ui.SetUiState(ent.Owner, FishingPortalUiKey.Key, new FishingPortalBoundUserInterfaceState(GetOptions(ent)));
    }

    /// <summary>
    /// The destinations the fish-porter currently offers. Only real portal dimensions, never the
    /// world fishing spots, and the syndicate dimension stays hidden until the machine is emagged.
    /// </summary>
    public List<FishingPortalOption> GetOptions(Entity<FishingPortalComponent> ent)
    {
        var options = new List<FishingPortalOption>();

        foreach (var source in _proto.EnumeratePrototypes<FishingSourcePrototype>().OrderBy(p => p.ID))
        {
            if (!IsAvailable(source, ent.Comp.Emagged))
                continue;

            var name = source.RadialName is { } radial
                ? Loc.GetString(radial)
                : source.CatalogDescription is { } key
                    ? Loc.GetString(key)
                    : Loc.GetString("fishing-source-secret");

            options.Add(new FishingPortalOption(
                source.ID,
                name,
                new SpriteSpecifier.Rsi(new ResPath("_Goobstation/Interface/Fishing/radial.rsi"), source.RadialState)));
        }

        return options;
    }

    private static bool IsAvailable(FishingSourcePrototype source, bool emagged)
    {
        return source.PortalDestination && (!source.RequiresEmag || emagged);
    }

    private void OnSelect(Entity<FishingPortalComponent> ent, ref FishingPortalSelectMessage args)
    {
        if (args.SourceId == null)
        {
            RemComp<FishingSourceComponent>(ent.Owner);
            ent.Comp.ActiveSource = null;
            Dirty(ent, ent.Comp);
            return;
        }

        if (!_proto.TryIndex(args.SourceId, out FishingSourcePrototype? selected) || !IsAvailable(selected, ent.Comp.Emagged))
            return;

        var source = EnsureComp<FishingSourceComponent>(ent.Owner);
        source.Source = selected.ID;
        Dirty(ent.Owner, source);

        ent.Comp.ActiveSource = selected.ID;
        Dirty(ent, ent.Comp);
    }
}
