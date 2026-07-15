using Content.Shared.RPSX.DarkForces.Ratvar.Righteous.Roles;
using Content.Shared.Access.Components;
using Content.Server.EUI;
using Content.Server.RPSX.DarkForces.Ratvar.UI;
using Robust.Shared.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Content.Server.Radio;
using Content.Shared.Radio.Components;
using Content.Server.Radio.EntitySystems;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Progress;

public sealed partial class RatvarProgressSystem
{
    [Dependency] private readonly EuiManager _euiMan = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;

    public void SetupRighteous(EntityUid uid)
    {
        if (_progressEntity?.Comp is not { } comp)
            return;

        // Выдаём доступ к дверям Ратвара
        var access = EnsureComp<AccessComponent>(uid);
        access.Tags.Add("RatvarRighteous");
        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(uid);
        var radio = EnsureComp<ActiveRadioComponent>(uid);
        radio.Channels.Add("Ratvar");
        transmitter.Channels.Add("Ratvar");
        AddObjectivesToRighteous(
            uid,
            comp.RatvarBeaconsObjective,
            comp.RatvarConvertObjective,
            comp.RatvarPowerObjective,
            comp.RatvarSummonObjective
        );

        // Открываем приветственное окно
        if (TryComp<ActorComponent>(uid, out var actor))
        {
            _euiMan.OpenEui(new RatvarRoundStartEui(), actor.PlayerSession);
        }
    }

    private bool CanUseRatvarItems(EntityUid uid)
    {
        return HasComp<RatvarRighteousComponent>(uid);
    }
}
