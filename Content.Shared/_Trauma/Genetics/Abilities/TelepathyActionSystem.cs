using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared._Trauma.Genetics.Abilities;

public sealed partial class TelepathyActionSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TelepathyActionComponent, TelepathyActionEvent>(OnTelepathyPrompt);

        Subs.BuiEvents<TelepathyActionComponent>(TelepathyUiKey.Key, subs =>
        {
            subs.Event<TelepathyChosenMessage>(OnTelepathyChosen);
        });
    }

    private void OnTelepathyPrompt(Entity<TelepathyActionComponent> ent, ref TelepathyActionEvent args)
    {
        if (_net.IsClient)
            return;

        var user = args.Performer;
        var target = args.Target;
        ent.Comp.Target = target;

        if (!_ui.TryOpenUi(ent.Owner, TelepathyUiKey.Key, user))
            Log.Error($"Failed to open UI for {ToPrettyString(ent)} of {ToPrettyString(user)}");
    }

    private void OnTelepathyChosen(Entity<TelepathyActionComponent> ent, ref TelepathyChosenMessage args)
    {
        var user = args.Actor;
        if (ent.Comp.Target is not { } target)
            return;

        ent.Comp.Target = null;

        var msg = args.Message.Trim();
        if (msg.Length > ent.Comp.MaxLength)
            return;

        if (_net.IsClient)
            return;

        var ident = Identity.Entity(target, EntityManager);
        if (!_actorQuery.HasComp(target))
        {
            _popup.PopupEntity(Loc.GetString("MutationTelepathy-popup-mindless", ("target", ident)), user, user);
            return;
        }

        _actions.StartUseDelay(ent.Owner);
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"{user:user} sent a telepathic message to {target:target}: {msg}");
        _popup.PopupEntity(Loc.GetString("MutationTelepathy-message-wrap", ("message", FormattedMessage.EscapeText(msg))), target, target, PopupType.Large);
    }
}
