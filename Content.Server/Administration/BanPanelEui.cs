// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Shared.Network;

namespace Content.Server.Administration;

public sealed class BanPanelEui : BaseEui
{
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IAdminManager _admins = default!;

    private readonly ISawmill _sawmill;

    private NetUserId? PlayerId { get; set; }
    private string PlayerName { get; set; } = string.Empty;
    private IPAddress? LastAddress { get; set; }
    private ImmutableTypedHwid? LastHwid { get; set; }
    private const int Ipv4_CIDR = CreateBanInfo.DefaultMaskIpv4;
    private const int Ipv6_CIDR = CreateBanInfo.DefaultMaskIpv6;

    public BanPanelEui()
    {
        IoCManager.InjectDependencies(this);

        _sawmill = _log.GetSawmill("admin.bans_eui");
    }

    public override EuiStateBase GetNewState()
    {
        var hasBan = _admins.HasAdminFlag(Player, AdminFlags.Ban);
        return new BanPanelEuiState(PlayerName, hasBan);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case BanPanelEuiStateMsg.CreateBanRequest r:
                BanPlayer(r.Ban);
                break;
            case BanPanelEuiStateMsg.CreateVoiceBanRequest r:
                VoiceBanPlayer(r.Ban);
                break;
            case BanPanelEuiStateMsg.GetPlayerInfoRequest r:
                ChangePlayer(r.PlayerUsername);
                break;
        }
    }

    private async void BanPlayer(Ban ban)
    {
        if (!CheckBanPermission())
            return;

        var target = await ResolveBanTarget(ban);
        if (target is not { } resolved)
            return;

        var isRoleBan = ban.BannedJobs?.Length > 0 || ban.BannedAntags?.Length > 0;

        CreateBanInfo banInfo = isRoleBan ? new CreateRoleBanInfo(ban.Reason) : new CreateServerBanInfo(ban.Reason);

        banInfo.WithBanningAdmin(Player.UserId);
        banInfo.WithSeverity(ban.Severity);
        if (ban.BanDurationMinutes > 0)
            banInfo.WithMinutes(ban.BanDurationMinutes);

        if (resolved.AddressRange != null)
            banInfo.AddAddressRange(resolved.AddressRange.Value);

        if (resolved.TargetUid != null)
            banInfo.AddUser(resolved.TargetUid.Value, ban.Target!);

        banInfo.AddHWId(resolved.TargetHwid);

        if (isRoleBan)
        {
            var roleBanInfo = (CreateRoleBanInfo)banInfo;
            foreach (var row in ban.BannedJobs ?? [])
            {
                roleBanInfo.AddJob(row);
            }

            foreach (var row in ban.BannedAntags ?? [])
            {
                roleBanInfo.AddAntag(row);
            }

            _banManager.CreateRoleBan(roleBanInfo);
        }
        else
        {
            if (ban.Erase && resolved.TargetUid is not null)
            {
                try
                {
                    if (_entities.TrySystem(out AdminSystem? adminSystem))
                        adminSystem.Erase(resolved.TargetUid.Value);
                }
                catch (Exception e)
                {
                    _sawmill.Error($"Error while erasing banned player:\n{e}");
                }
            }

            _banManager.CreateServerBan((CreateServerBanInfo)banInfo);
        }

        Close();
    }

    private async void VoiceBanPlayer(Ban ban)
    {
        if (!CheckBanPermission())
            return;

        var target = await ResolveBanTarget(ban);
        if (target is not { } resolved)
            return;

        // Voice bans do not use roles and never erase chat messages or the player from the round.
        var banInfo = new CreateVoiceBanInfo(ban.Reason);

        banInfo.WithBanningAdmin(Player.UserId);
        banInfo.WithSeverity(ban.Severity);
        if (ban.BanDurationMinutes > 0)
            banInfo.WithMinutes(ban.BanDurationMinutes);

        if (resolved.AddressRange != null)
            banInfo.AddAddressRange(resolved.AddressRange.Value);

        if (resolved.TargetUid != null)
            banInfo.AddUser(resolved.TargetUid.Value, ban.Target!);

        banInfo.AddHWId(resolved.TargetHwid);

        _banManager.CreateVoiceBan(banInfo);

        Close();
    }

    private bool CheckBanPermission()
    {
        if (_admins.HasAdminFlag(Player, AdminFlags.Ban))
            return true;

        _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to create a ban with no ban flag");

        return false;
    }

    private readonly record struct ResolvedBanTarget(
        NetUserId? TargetUid,
        (IPAddress Address, int Mask)? AddressRange,
        ImmutableTypedHwid? TargetHwid);

    /// <summary>
    /// Resolves the target user, address range and HWID of a ban request from the ban panel.
    /// Returns null and notifies the admin if the request is invalid.
    /// </summary>
    private async Task<ResolvedBanTarget?> ResolveBanTarget(Ban ban)
    {
        if (ban.Target == null && string.IsNullOrWhiteSpace(ban.IpAddress) && ban.Hwid == null)
        {
            _chat.DispatchServerMessage(Player, Loc.GetString("ban-panel-no-data"));

            return null;
        }

        (IPAddress, int)? addressRange = null;
        if (ban.IpAddress is not null)
        {
            if (!IPAddress.TryParse(ban.IpAddress, out var ipAddress) || !uint.TryParse(ban.IpAddressHid, out var hidInt) || hidInt > Ipv6_CIDR || hidInt > Ipv4_CIDR && ipAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                _chat.DispatchServerMessage(Player, Loc.GetString("ban-panel-invalid-ip"));
                return null;
            }

            if (hidInt == 0)
                hidInt = (uint) (ipAddress.AddressFamily == AddressFamily.InterNetworkV6 ? Ipv6_CIDR : Ipv4_CIDR);

            addressRange = (ipAddress, (int) hidInt);
        }

        var targetUid = ban.Target is not null ? PlayerId : null;
        addressRange = ban.UseLastIp && LastAddress is not null ? (LastAddress, LastAddress.AddressFamily == AddressFamily.InterNetworkV6 ? Ipv6_CIDR : Ipv4_CIDR) : addressRange;
        var targetHWid = ban.UseLastHwid ? LastHwid : ban.Hwid;
        if (ban.Target != null && ban.Target != PlayerName || Guid.TryParse(ban.Target, out var parsed) && parsed != PlayerId)
        {
            var located = await _playerLocator.LookupIdByNameOrIdAsync(ban.Target);
            if (located == null)
            {
                _chat.DispatchServerMessage(Player, Loc.GetString("cmd-ban-player"));
                return null;
            }
            targetUid = located.UserId;
            var targetAddress = located.LastAddress;
            if (ban.UseLastIp && targetAddress != null)
            {
                if (targetAddress.IsIPv4MappedToIPv6)
                    targetAddress = targetAddress.MapToIPv4();

                // Ban /64 for IPv6, /32 for IPv4.
                var hid = targetAddress.AddressFamily == AddressFamily.InterNetworkV6 ? Ipv6_CIDR : Ipv4_CIDR;
                addressRange = (targetAddress, hid);
            }
            targetHWid = ban.UseLastHwid ? located.LastHWId : ban.Hwid;
        }

        return new ResolvedBanTarget(targetUid, addressRange, targetHWid);
    }

    public async void ChangePlayer(string playerNameOrId)
    {
        var located = await _playerLocator.LookupIdByNameOrIdAsync(playerNameOrId);
        ChangePlayer(located?.UserId, located?.Username ?? string.Empty, located?.LastAddress, located?.LastHWId);
    }

    public void ChangePlayer(NetUserId? playerId, string playerName, IPAddress? lastAddress, ImmutableTypedHwid? lastHwid)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        LastAddress = lastAddress;
        LastHwid = lastHwid;
        StateDirty();
    }

    public override async void Opened()
    {
        base.Opened();
        _admins.OnPermsChanged += OnPermsChanged;
    }

    public override void Closed()
    {
        base.Closed();
        _admins.OnPermsChanged -= OnPermsChanged;
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player != Player)
        {
            return;
        }

        StateDirty();
    }
}
