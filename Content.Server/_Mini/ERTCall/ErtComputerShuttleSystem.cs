// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Power;
using Content.Server._Mini.ERTCall;
using Content.Shared._Mini.ERT;
using Content.Shared._Mini.TimeWindow;
using Content.Shared._NF.Shuttles;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Robust.Shared.Timing;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Tag;
using Content.Server.RoundEnd;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.ERT;

public sealed class ErtComputerShuttleSystem : EntitySystem
{
    public InGameICChatType ChatType = InGameICChatType.Speak;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly PowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private readonly TimedWindowSystem _timedWindowSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;

    private static readonly ProtoId<TagPrototype> DockTag = "DockCentcommERT";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ErtComputerShuttleComponent, ErtComputerShuttleUiButtonPressedMessage>(OnButtonPressed);
        SubscribeLocalEvent<ErtComputerShuttleComponent, AfterActivatableUIOpenEvent>(OnUIOpen);
        SubscribeLocalEvent<ErtComputerShuttleComponent, PowerChangedEvent>(OnPowerChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ErtComputerShuttleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!component.IsEvacuation)
                continue;

            if (!_timedWindowSystem.IsExpired(component.EvacuationWindow))
            {
                if (_timing.CurTime < component.NextAnnounceTime)
                    continue;

                var time = component.EvacuationWindow.Remaining - _timing.CurTime;
                var seconds = Math.Max(0, (int) Math.Ceiling(time.TotalSeconds));

                _chatSystem.TrySendInGameICMessage(
                    uid,
                    Loc.GetString("ert-computer-time-until-eval", ("time", seconds.ToString())),
                    InGameICChatType.Speak,
                    ChatTransmitRange.Normal,
                    true);

                component.NextAnnounceTime = _timing.CurTime + TimeSpan.FromSeconds(1);
                continue;
            }

            component.IsEvacuation = false;

            var shuttleUid = Transform(uid).GridUid;
            if (shuttleUid == null)
            {
                Announce(uid, Loc.GetString("ert-computer-evac-failed", ("reason", "no grid")));
                continue;
            }

            if (!TryComp(shuttleUid.Value, out ShuttleComponent? shuttleComp))
            {
                Announce(uid, Loc.GetString("ert-computer-evac-failed", ("reason", "not a shuttle")));
                continue;
            }

            if (!_shuttleSystem.CanFTL(shuttleUid.Value, out var reason) &&
                !IsOnlyMassLimitFailure(shuttleUid.Value, reason))
            {
                Announce(uid, Loc.GetString("ert-computer-evac-failed", ("reason", reason ?? "FTL blocked")));
                continue;
            }

            TryEvacToCentComm(uid, shuttleUid.Value, shuttleComp);
        }
    }

    /// <summary>
    /// ERT red/gamma grids often exceed the global FTL mass limit; evacuation must still work.
    /// </summary>
    private bool IsOnlyMassLimitFailure(EntityUid shuttleUid, string? reason)
    {
        if (reason == null)
            return false;

        if (reason != Loc.GetString("shuttle-console-mass"))
            return false;

        // Still block if already in FTL / prevented.
        if (HasComp<FTLComponent>(shuttleUid) || HasComp<PreventFTLComponent>(shuttleUid))
            return false;

        return true;
    }

    private void TryEvacToCentComm(EntityUid console, EntityUid shuttleUid, ShuttleComponent shuttleComp)
    {
        var centcommGrid = _roundEnd.GetCentcommGridEntity();
        if (centcommGrid == null || Deleted(centcommGrid.Value))
        {
            // Fallback: scan StationCentcommComponent directly (Entity may lag behind RoundEnd cache).
            var ccQuery = EntityQueryEnumerator<StationCentcommComponent>();
            while (ccQuery.MoveNext(out _, out var cc))
            {
                if (cc.Entity is { } grid && !Deleted(grid))
                {
                    centcommGrid = grid;
                    break;
                }
            }
        }

        if (centcommGrid == null || Deleted(centcommGrid.Value))
        {
            Announce(console, Loc.GetString("ert-computer-evac-centcomm-missing"));
            return;
        }

        // Full FTL (startup sound + hyperspace), not TryFTLDock which instant-teleports.
        if (!HasComp<FTLDriveComponent>(shuttleUid))
        {
            Announce(console, Loc.GetString("ert-computer-evac-failed", ("reason", "no FTL drive")));
            return;
        }

        if (HasComp<FTLComponent>(shuttleUid))
        {
            Announce(console, Loc.GetString("ert-computer-evac-failed", ("reason", "already FTL")));
            return;
        }

        _shuttleSystem.FTLToDock(shuttleUid, shuttleComp, centcommGrid.Value, priorityTag: DockTag.Id);
        Announce(console, Loc.GetString("ert-computer-evac-started"));
    }

    private void Announce(EntityUid uid, string message)
    {
        _chatSystem.TrySendInGameICMessage(
            uid,
            message,
            InGameICChatType.Speak,
            ChatTransmitRange.Normal,
            true);
    }

    private void OnButtonPressed(EntityUid uid, ErtComputerShuttleComponent component, ErtComputerShuttleUiButtonPressedMessage args)
    {
        if (!_powerReceiverSystem.IsPowered(uid))
            return;

        switch (args.Button)
        {
            case ErtComputerShuttleUiButton.Evacuation:
                _timedWindowSystem.Reset(component.EvacuationWindow);
                component.IsEvacuation = true;
                component.NextAnnounceTime = TimeSpan.Zero;
                break;
            case ErtComputerShuttleUiButton.CancelEvacuation:
                component.IsEvacuation = false;
                break;
        }

        UpdateUserInterface((uid, component));
    }

    private void OnPowerChanged(EntityUid uid, ErtComputerShuttleComponent component, ref PowerChangedEvent args)
    {
        UpdateUserInterface((uid, component));
    }

    private void OnUIOpen(EntityUid uid, ErtComputerShuttleComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateUserInterface((uid, component));
    }

    public void UpdateUserInterface(Entity<ErtComputerShuttleComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        if (!TryComp<UserInterfaceComponent>(entity, out var userInterface))
            return;

        if (!_uiSystem.HasUi(entity, ErtComputerShuttleUiKey.Key, userInterface))
            return;

        if (!_powerReceiverSystem.IsPowered(entity))
        {
            _uiSystem.CloseUis((entity, userInterface));
            return;
        }

        _uiSystem.SetUiState((entity, userInterface), ErtComputerShuttleUiKey.Key, new ErtComputerShuttleBoundUserInterfaceState());
    }
}
