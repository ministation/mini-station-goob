using Content.Shared.Item;
using Content.Shared.Tag;
using Robust.Shared.GameStates;

namespace Content.Shared.Borgs.Mini;

[RegisterComponent, NetworkedComponent]
public sealed partial class BorgHandsMiniSecurityComponent : Component
{

}

public sealed partial class BorgHandsMiniSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgHandsMiniSecurityComponent, PickupAttemptEvent>(OnPickupAttempt);
    }


    public void OnPickupAttempt(EntityUid uid, BorgHandsMiniSecurityComponent component, PickupAttemptEvent args)
    {
        if (_tagSystem.HasAnyTag(args.Item, "BorgItem")) return;

        args.Cancel();
    }

}
