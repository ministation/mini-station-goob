using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking;

namespace Content.Client._Mini.TypanWar;

/// <summary>
/// Caches Typan war HUD state from server broadcasts.
/// </summary>
public sealed class TypanWarUiSystem : EntitySystem
{
    public TypanWarPhase Phase { get; private set; } = TypanWarPhase.Inactive;
    public TypanWarWinner Winner { get; private set; } = TypanWarWinner.None;
    public int NtAlive { get; private set; }
    public int TypanAlive { get; private set; }
    public float NtCapturePoints { get; private set; }
    public float TypanCapturePoints { get; private set; }
    public int CapturePointsToWin { get; private set; } = 100;
    public float TimeRemainingSeconds { get; private set; }
    public TypanWarCaptureZoneStatus[] CaptureZones { get; private set; } = Array.Empty<TypanWarCaptureZoneStatus>();
    public TypanWarAllyBlip[] AllyBlips { get; private set; } = Array.Empty<TypanWarAllyBlip>();
    public TypanWarMinimapGrid[] MinimapGrids { get; private set; } = Array.Empty<TypanWarMinimapGrid>();

    public bool BalanceActive { get; private set; }
    public bool AllowNanotrasen { get; private set; } = true;
    public bool AllowTypan { get; private set; } = true;
    public int NtJoined { get; private set; }
    public int TypanJoined { get; private set; }

    public event Action? StatusUpdated;
    public event Action? BalanceUpdated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TypanWarStatusEvent>(OnStatus);
        SubscribeNetworkEvent<TypanWarBalanceStatusEvent>(OnBalanceStatus);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public void RequestStatus()
    {
        RaiseNetworkEvent(new TypanWarStatusRequestEvent());
    }

    public void RequestBalanceStatus()
    {
        RaiseNetworkEvent(new TypanWarBalanceStatusRequestEvent());
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        ResetStatus();
        ResetBalance();
    }

    private void OnStatus(TypanWarStatusEvent ev)
    {
        Phase = ev.Phase;
        Winner = ev.Winner;
        NtAlive = ev.NtAlive;
        TypanAlive = ev.TypanAlive;
        NtCapturePoints = ev.NtCapturePoints;
        TypanCapturePoints = ev.TypanCapturePoints;
        CapturePointsToWin = ev.CapturePointsToWin;
        TimeRemainingSeconds = ev.TimeRemainingSeconds;
        CaptureZones = ev.CaptureZones;
        AllyBlips = ev.AllyBlips;
        MinimapGrids = ev.MinimapGrids;
        StatusUpdated?.Invoke();
    }

    private void OnBalanceStatus(TypanWarBalanceStatusEvent ev)
    {
        BalanceActive = ev.Active;
        AllowNanotrasen = ev.Active ? ev.AllowNanotrasen : true;
        AllowTypan = ev.Active ? ev.AllowTypan : true;
        NtJoined = ev.NtJoined;
        TypanJoined = ev.TypanJoined;
        BalanceUpdated?.Invoke();
    }

    private void ResetStatus()
    {
        Phase = TypanWarPhase.Inactive;
        Winner = TypanWarWinner.None;
        NtAlive = 0;
        TypanAlive = 0;
        NtCapturePoints = 0;
        TypanCapturePoints = 0;
        CapturePointsToWin = 100;
        TimeRemainingSeconds = 0;
        CaptureZones = Array.Empty<TypanWarCaptureZoneStatus>();
        AllyBlips = Array.Empty<TypanWarAllyBlip>();
        MinimapGrids = Array.Empty<TypanWarMinimapGrid>();
        StatusUpdated?.Invoke();
    }

    private void ResetBalance()
    {
        BalanceActive = false;
        AllowNanotrasen = true;
        AllowTypan = true;
        NtJoined = 0;
        TypanJoined = 0;
        BalanceUpdated?.Invoke();
    }
}
