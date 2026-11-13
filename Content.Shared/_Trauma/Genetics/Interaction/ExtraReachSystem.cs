// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Trauma.Genetics.YamlCompat;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Interaction;

namespace Content.Shared._Trauma.Genetics.Interaction;

/// <summary>
/// Increases an entity's interaction range when using the entity-entity InRangeUnobstructed check.
/// Body compatible, each part adds its reach to the user.
/// </summary>
/// <remarks>
/// Won't work for anything that manually passes coords instead of using the entity-entity check.
/// </remarks>
public sealed class ExtraReachSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExtraReachComponent, ComponentStartup>(OnPartStartup);
        SubscribeLocalEvent<ExtraReachComponent, ComponentShutdown>(OnPartShutdown);
        SubscribeLocalEvent<ExtraReachComponent, BodyPartAddedEvent>(OnPartAdded);
        SubscribeLocalEvent<ExtraReachComponent, BodyPartRemovedEvent>(OnPartRemoved);
        // run before TK so it can use the extra reach for its check
        SubscribeLocalEvent<ExtraReachComponent, InRangeOverrideEvent>(OnRangeOverride,
            before: new[] { typeof(TelekinesisSystem) });
    }

    private void OnPartStartup(Entity<ExtraReachComponent> ent, ref ComponentStartup args)
    {
        if (TryGetBody(ent, out var body))
            ModifyReach(body, ent.Comp.Bonus);
    }

    private void OnPartShutdown(Entity<ExtraReachComponent> ent, ref ComponentShutdown args)
    {
        if (TryGetBody(ent, out var body))
            ModifyReach(body, -ent.Comp.Bonus);
    }

    private void OnPartAdded(Entity<ExtraReachComponent> ent, ref BodyPartAddedEvent args)
    {
        // the event is raised on the body too, only the part itself is a reach source
        if (args.Part.Owner != ent.Owner)
            return;

        if (args.Part.Comp.Body is { } body)
            ModifyReach(body, ent.Comp.Bonus);
    }

    private void OnPartRemoved(Entity<ExtraReachComponent> ent, ref BodyPartRemovedEvent args)
    {
        if (args.Part.Owner != ent.Owner)
            return;

        if (args.Part.Comp.Body is { } body)
            ModifyReach(body, -ent.Comp.Bonus);
    }

    private void OnRangeOverride(Entity<ExtraReachComponent> ent, ref InRangeOverrideEvent args)
    {
        // a non-positive range means the caller only wants an obstruction check, don't turn it into a limit
        if (args.Range > 0f)
            args.Range += ent.Comp.Bonus;
    }

    /// <summary>
    /// Gets the mob this body part or organ belongs to, if it is attached to one.
    /// The aggregated reach component lives on the mob itself and is ignored here.
    /// </summary>
    private bool TryGetBody(EntityUid uid, out EntityUid body)
    {
        if (TryComp(uid, out BodyPartComponent? part) && part.Body is { } partBody)
        {
            body = partBody;
            return true;
        }

        if (TryComp(uid, out OrganComponent? organ) && organ.Body is { } organBody)
        {
            body = organBody;
            return true;
        }

        body = default;
        return false;
    }

    public void ModifyReach(EntityUid uid, float reach)
    {
        // don't care if the body is being deleted
        if (TerminatingOrDeleted(uid))
            return;

        var comp = EnsureComp<ExtraReachComponent>(uid);
        comp.Bonus += reach;
        Dirty(uid, comp);

        // remove the component if it goes to 0f
        if (Math.Abs(comp.Bonus) < 0.001f)
            RemComp(uid, comp);
    }
}
