
using Content.Server.Power.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Power;
using Robust.Server.GameObjects;
using Content.Server._Mini.ERTCall;
using Content.Shared._Mini.ERT;
using Content.Server.Station.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Cargo.Components;

namespace Content.Server._Mini.ERT;

public sealed class ErtResponceConsoleSystem : EntitySystem
{
    public InGameICChatType ChatType = InGameICChatType.Speak;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly PowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ErtResponceSystem _ertResponceSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ErtResponceConsoleComponent, ErtResponceConsoleUiButtonPressedMessage>(OnButtonPressed);
        SubscribeLocalEvent<ErtResponceConsoleComponent, AfterActivatableUIOpenEvent>(OnUIOpen);
        SubscribeLocalEvent<ErtResponceConsoleComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnButtonPressed(EntityUid uid, ErtResponceConsoleComponent component, ErtResponceConsoleUiButtonPressedMessage args)
    {
        // Handheld telephones have no APC receiver — IsPowered is true when missing.
        if (!_powerReceiverSystem.IsPowered(uid))
            return;

        if (string.IsNullOrEmpty(args.Team))
        {
            Announce(uid, Loc.GetString("ert-call-fail-no-team-selected"));
            return;
        }

        // Prefer the caller's station (handheld on CentComm has no usable owning station).
        var station = ResolveTargetStation(uid, args.Actor);

        switch (args.Button)
        {
            case ErtResponceConsoleUiButton.ResponceErt:
                {
                    var price = _ertResponceSystem.GetErtPrice(args.Team);
                    var balance = _ertResponceSystem.GetBalance();

                    if (balance < price)
                    {
                        Announce(uid, Loc.GetString(
                            "ert-call-fail-not-enough-points",
                            ("price", price),
                            ("balance", balance)));
                        break;
                    }

                    if (!_ertResponceSystem.TryCallErt(args.Team, station, out var reason, callReason: args.CallReason))
                    {
                        Announce(uid, reason ?? Loc.GetString("ert-responce-call-cancel"));
                    }
                    else
                    {
                        Announce(uid, Loc.GetString("ert-call-success-device"));
                    }

                    break;
                }

            default:
                break;
        }

        UpdateUserInterface((uid, component));
    }

    /// <summary>
    /// Wall consoles use the grid's station; handhelds on CentComm must target the playable station.
    /// </summary>
    private EntityUid? ResolveTargetStation(EntityUid device, EntityUid actor)
    {
        var fromActor = _station.GetOwningStation(actor);
        var fromDevice = _station.GetOwningStation(device);

        foreach (var candidate in new[] { fromActor, fromDevice })
        {
            if (candidate is { } station && IsPlayableStation(station))
                return station;
        }

        foreach (var station in _station.GetStations())
        {
            if (IsPlayableStation(station))
                return station;
        }

        return fromActor ?? fromDevice;
    }

    private bool IsPlayableStation(EntityUid station)
    {
        // CentComm jobs station has no cargo bank; main maps do.
        return HasComp<StationBankAccountComponent>(station);
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

    private void OnPowerChanged(EntityUid uid, ErtResponceConsoleComponent component, ref PowerChangedEvent args)
    {
        UpdateUserInterface((uid, component));
    }

    private void OnUIOpen(EntityUid uid, ErtResponceConsoleComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateUserInterface((uid, component));
    }

    public void UpdateUserInterface(Entity<ErtResponceConsoleComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        if (!TryComp<UserInterfaceComponent>(entity, out var userInterface))
            return;

        if (!_uiSystem.HasUi(entity, ErtResponceConsoleUiKey.Key, userInterface))
            return;

        if (!_powerReceiverSystem.IsPowered(entity))
        {
            _uiSystem.CloseUis((entity, userInterface));
            return;
        }

        var newState = GetUserInterfaceState((entity, entity.Comp));
        _uiSystem.SetUiState((entity, userInterface), ErtResponceConsoleUiKey.Key, newState);
    }

    private ErtResponceConsoleBoundUserInterfaceState GetUserInterfaceState(Entity<ErtResponceConsoleComponent?> console)
    {
        if (!Resolve(console, ref console.Comp, false))
            return default!;

        var balance = _ertResponceSystem.GetBalance();

        return new ErtResponceConsoleBoundUserInterfaceState(
            console.Comp.Teams,
            balance
        );
    }
}
