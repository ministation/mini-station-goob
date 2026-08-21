using Content.Goobstation.Common.Religion;
using Content.Shared.CombatMode;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Shared._Trauma.Genetics.Abilities;

public sealed partial class MindReadActionSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MindReadActionComponent, MindReadActionEvent>(OnMindRead);
    }

    private void OnMindRead(Entity<MindReadActionComponent> ent, ref MindReadActionEvent args)
    {
        var user = args.Performer;
        var target = args.Target;

        if (!_actorQuery.HasComp(user))
            return;

        args.Handled = true;

        var identity = Identity.Name(target, EntityManager);
        if (!_mind.TryGetMind(target, out _, out var mind))
        {
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-target-mindless", ("target", identity)), user, user);
            return;
        }

        if (_mob.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-target-dead", ("target", identity)), user, user);
            return;
        }

        var ev = new BeforeCastTouchSpellEvent(target);
        RaiseLocalEvent(target, ev);
        if (ev.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-mind-protected", ("target", identity)), user, user);
            return;
        }

        if (user == target)
        {
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-self"), user, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-plunge", ("target", identity)), user, user);

        if (_net.IsClient)
            return;

        if (_random.Prob(ent.Comp.AlertProb))
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-alert"), target, target, PopupType.MediumCaution);

        var combat = _combatMode.IsInCombatMode(target);
        _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-combat-mode", ("target", target), ("combat", combat)), user, user);

        if (mind.CharacterName is { } name && name != identity)
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-true-identity", ("target", target), ("name", name)), user, user, PopupType.MediumCaution);
    }
}
