// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


using Content.Shared._Mini.ERT;

namespace Content.Client._Mini.ERT;

public sealed class ErtResponceSystem : SharedErtResponceSystem
{
    public ErtAdminStateResponse? LastState { get; private set; }

    public event Action? OnStateUpdated;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ErtAdminStateResponse>(OnErtAdminStateResponse);
        SubscribeNetworkEvent<ErtAdminActionResult>(OnErtAdminActionResult);
    }

    private void OnErtAdminStateResponse(ErtAdminStateResponse msg, EntitySessionEventArgs args)
    {
        LastState = msg;
        OnStateUpdated?.Invoke();
    }

    private void OnErtAdminActionResult(ErtAdminActionResult msg, EntitySessionEventArgs args)
    {
        Log.Warning(msg.Message);
    }

    public void RequestAdminState()
    {
        RaiseNetworkEvent(new RequestErtAdminStateMessage());
    }

    public void AdminModifyEntry(string protoId, int seconds)
    {
        RaiseNetworkEvent(new AdminModifyErtEntryMessage(protoId, seconds));
    }

    public void AdminSetPoints(int points)
    {
        RaiseNetworkEvent(new AdminSetPointsMessage(points));
    }

    public void AdminSetCooldown(int seconds)
    {
        RaiseNetworkEvent(new AdminSetCooldownMessage(seconds));
    }

    public void AdminDeleteErt(string protoId)
    {
        RaiseNetworkEvent(new AdminDeleteErtMessage(protoId));
    }

    public void AdminSetReason(string protoId, string reason)
    {
        RaiseNetworkEvent(new AdminSetErtReasonMessage(protoId, reason));
    }

    public void AdminCallErt(string protoId, string reason)
    {
        RaiseNetworkEvent(new AdminCallErtMessage(protoId, reason));
    }
}
