using Content.Server.Mind;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.RPSX.DarkForces.Ratvar.Righteous.Roles;

namespace Content.Server.RPSX.DarkForces.Ratvar;

public sealed class RatvarDoorSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PinionDoorComponent, BeforeDoorOpenedEvent>(OnBeforeDoorOpen);
    }

    private void OnBeforeDoorOpen(EntityUid uid, PinionDoorComponent comp, BeforeDoorOpenedEvent args)
    {
        if (args.User == null || CanUsePinionDoor(args.User.Value))
            return;

        args.Cancel();

        // Access-denied style feedback (BeforeDoorOpened cancel alone is silent).
        if (TryComp<DoorComponent>(uid, out var door))
            _door.Deny(uid, door, args.User, predicted: true);
    }

    private bool CanUsePinionDoor(EntityUid user)
    {
        if (HasComp<RatvarRighteousComponent>(user))
            return true;

        return HasComp<RatvarMarauderShellComponent>(user)
               && _mind.TryGetMind(user, out _, out _);
    }
}

[RegisterComponent]
public sealed partial class PinionDoorComponent : Component { }
