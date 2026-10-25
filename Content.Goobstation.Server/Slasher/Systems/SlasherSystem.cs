using Content.Goobstation.Shared.Slasher.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Server.Body.Components;
using Content.Shared.Rejuvenate;
using Content.Shared.Standing;

namespace Content.Goobstation.Server.Slasher.Systems;

/// <summary>
/// Moves the slasher's brain from the head into the chest
/// </summary>
public sealed partial class SlasherSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    private const string BrainSlot = "brain";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlasherComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlasherComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnMapInit(Entity<SlasherComponent> ent, ref MapInitEvent args) =>
        MoveBrainToChest(ent);

    private void OnRejuvenate(Entity<SlasherComponent> ent, ref RejuvenateEvent args) =>
        MoveBrainToChest(ent);

    private void MoveBrainToChest(Entity<SlasherComponent> ent)
    {
        if (!TryComp<BodyComponent>(ent, out var bodyComp)
            || !_body.TryGetBodyOrganEntityComps<BrainComponent>((ent.Owner, bodyComp), out var brains))
            return;

        var brain = brains[0];

        // Only move a brain that actually sits in the head slot, the way standard humanoid bodies are built.
        // Species such as slimes keep their brain elsewhere, a slime core in the groin. Pulling such an organ
        // out debrains the body and hands the mind over to the organ itself, so non-standard anatomy is left
        // untouched instead.
        if (_body.GetParentPartOrNull(brain.Owner) is not { } head
            || !TryComp<BodyPartComponent>(head, out var headPart)
            || headPart.PartType != BodyPartType.Head
            || brain.Comp2.SlotId != BrainSlot)
            return;

        EntityUid? chestPart = null;
        foreach (var (partId, _) in _body.GetBodyChildrenOfType(ent.Owner, BodyPartType.Chest, bodyComp))
        {
            chestPart = partId;
            break;
        }

        if (chestPart is not { } chest || !_body.TryCreateOrganSlot(chest, BrainSlot, out _))
            return;

        if (!_body.RemoveOrgan(brain.Owner, brain.Comp2))
            return;

        // Never leave the slasher debrained with their brain on the floor: if the organ cannot be
        // inserted into the chest, put it back into the slot it came from.
        if (!_body.InsertOrgan(chest, brain.Owner, BrainSlot))
        {
            _body.InsertOrgan(head, brain.Owner, brain.Comp2.SlotId, headPart, brain.Comp2);
            return;
        }

        _standing.Stand(ent.Owner, force: true);
    }
}
