/*
// CorvaxGoob: the RMC14 LinkAccount manager is disabled in this fork,
// so the upstream patron OOC-icon system is disabled together with it.
using Content.Goobstation.Shared.Patrons;
using Content.Server._RMC14.LinkAccount;

namespace Content.Goobstation.Server.Patrons;

public sealed partial class PatronOocIconSystem : EntitySystem
{
    [Dependency] private LinkAccountManager _linkAccount = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<ChangePatronOocIconEvent>(OnChangeIcon);
    }

    private void OnChangeIcon(ChangePatronOocIconEvent ev, EntitySessionEventArgs args)
    {
        _linkAccount.SetOocIcon(args.SenderSession.UserId, ev.Icon?.Id);
    }
}

*/
