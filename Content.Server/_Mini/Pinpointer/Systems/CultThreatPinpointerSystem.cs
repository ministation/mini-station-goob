// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Shared.Devil;
using Content.Server.Pinpointer;
using Content.Server.RPSX.DarkForces.Ratvar.Righteous.Structures.Portal;
using Content.Server.WhiteDream.BloodCult.Runes.Apocalypse;
using Content.Server.WhiteDream.BloodCult.Runes.Rending;
using Content.Shared._DV.CosmicCult.Components;
using Content.Shared._Mini.Pinpointer;
using Content.Shared.Heretic;
using Content.Shared.Interaction;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Server.Shuttles.Events;
using Robust.Shared.Utility;

namespace Content.Server._Mini.Pinpointer.Systems;

/// <summary>
/// Modes + priority targeting for <see cref="CultThreatPinpointerComponent"/>.
/// </summary>
public sealed class CultThreatPinpointerSystem : EntitySystem
{
    [Dependency] private readonly SharedPinpointerSystem _pinpointer = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<CultThreatPinpointerComponent, ActivateInWorldEvent>(OnActivate,
            after: new[] { typeof(PinpointerSystem) });
        SubscribeLocalEvent<CultThreatPinpointerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFtlCompleted);
    }

    private void OnGetVerbs(Entity<CultThreatPinpointerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("pinpointer-cult-cycle-mode"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () => CycleMode(ent, user),
            Priority = 2,
        });
    }

    private void OnActivate(Entity<CultThreatPinpointerComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (!TryComp(ent, out PinpointerComponent? pinpointer) || !pinpointer.IsActive)
            return;

        ApplyModeTargets(ent, pinpointer);
    }

    private void OnFtlCompleted(ref FTLCompletedEvent ev)
    {
        var query = EntityQueryEnumerator<CultThreatPinpointerComponent, PinpointerComponent>();
        while (query.MoveNext(out var uid, out var cult, out var pinpointer))
        {
            if (!pinpointer.IsActive)
                continue;

            ApplyModeTargets((uid, cult), pinpointer);
        }
    }

    private void CycleMode(Entity<CultThreatPinpointerComponent> ent, EntityUid user)
    {
        var values = Enum.GetValues<CultThreatPinpointerMode>();
        var next = ((int) ent.Comp.Mode + 1) % values.Length;
        ent.Comp.Mode = (CultThreatPinpointerMode) next;
        Dirty(ent);

        _popup.PopupEntity(
            Loc.GetString("pinpointer-cult-mode-changed", ("mode", Loc.GetString(GetModeLoc(ent.Comp.Mode)))),
            user,
            user);

        if (TryComp(ent, out PinpointerComponent? pinpointer) && pinpointer.IsActive)
            ApplyModeTargets(ent, pinpointer);
    }

    private void ApplyModeTargets(Entity<CultThreatPinpointerComponent> ent, PinpointerComponent pinpointer)
    {
        var targets = ent.Comp.Mode switch
        {
            CultThreatPinpointerMode.Auto => FindAutoTargets(ent.Owner),
            CultThreatPinpointerMode.CosmicMonument => FindByTypes(ent.Owner, typeof(MonumentComponent)),
            CultThreatPinpointerMode.RatvarPortal => FindByTypes(ent.Owner, typeof(RatvarPortalComponent)),
            CultThreatPinpointerMode.NarSieRending => FindByTypes(ent.Owner,
                typeof(CultRuneRendingComponent),
                typeof(CultRuneApocalypseComponent)),
            CultThreatPinpointerMode.Heretic => FindByTypes(ent.Owner, typeof(HereticComponent)),
            CultThreatPinpointerMode.Devil => FindByTypes(ent.Owner, typeof(DevilComponent)),
            _ => new List<EntityUid>(),
        };

        _pinpointer.SetTargetName(ent.Owner, Loc.GetString(GetModeLoc(ent.Comp.Mode)), pinpointer);
        _pinpointer.SetTargets(ent.Owner, targets, pinpointer);
    }

    /// <summary>
    /// Priority: monument → Ratvar portal → Nar'Sie rending/apocalypse → heretic/devil.
    /// </summary>
    private List<EntityUid> FindAutoTargets(EntityUid pinpointerUid)
    {
        var monument = FindByTypes(pinpointerUid, typeof(MonumentComponent));
        if (monument.Count > 0)
            return monument;

        var ratvar = FindByTypes(pinpointerUid, typeof(RatvarPortalComponent));
        if (ratvar.Count > 0)
            return ratvar;

        var narsie = FindByTypes(pinpointerUid,
            typeof(CultRuneRendingComponent),
            typeof(CultRuneApocalypseComponent));
        if (narsie.Count > 0)
            return narsie;

        return FindByTypes(pinpointerUid, typeof(HereticComponent), typeof(DevilComponent));
    }

    private List<EntityUid> FindByTypes(EntityUid pinpointerUid, params Type[] componentTypes)
    {
        var list = new List<EntityUid>();

        if (!_xformQuery.TryGetComponent(pinpointerUid, out var pinXform))
            return list;

        var mapId = pinXform.MapID;

        foreach (var type in componentTypes)
        {
            foreach (var (otherUid, _) in EntityManager.GetAllComponents(type))
            {
                if (!_xformQuery.TryGetComponent(otherUid, out var otherXform) || otherXform.MapID != mapId)
                    continue;

                list.Add(otherUid);
            }
        }

        return list.Distinct().ToList();
    }

    private static string GetModeLoc(CultThreatPinpointerMode mode) => mode switch
    {
        CultThreatPinpointerMode.Auto => "pinpointer-cult-mode-auto",
        CultThreatPinpointerMode.CosmicMonument => "pinpointer-cult-mode-monument",
        CultThreatPinpointerMode.RatvarPortal => "pinpointer-cult-mode-ratvar",
        CultThreatPinpointerMode.NarSieRending => "pinpointer-cult-mode-narsie",
        CultThreatPinpointerMode.Heretic => "pinpointer-cult-mode-heretic",
        CultThreatPinpointerMode.Devil => "pinpointer-cult-mode-devil",
        _ => "pinpointer-cult-mode-auto",
    };
}
