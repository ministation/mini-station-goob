using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;


namespace Content.Goobstation.Shared.ObraDinn;

/// <summary>
/// This handles storing the entitys near a player specie  on death
/// </summary>
public sealed partial class ObraDinnBodySystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObraDinnBodyComponent, MobStateChangedEvent>(OnDeath);
    }

    private void OnDeath(Entity<ObraDinnBodyComponent> ent, ref MobStateChangedEvent args)
    {
        ent.Comp.Witnesses.Clear();
        ent.Comp.Map = null;

        Dirty(ent);

        if (args.NewMobState != MobState.Dead || TerminatingOrDeleted(ent))
            return;

        var mapCoords = _transform.GetMapCoordinates(ent.Owner);
        ent.Comp.Location = mapCoords.Position;
        ent.Comp.Map = mapCoords.MapId;

        var list = _lookup.GetEntitiesInRange(mapCoords, ent.Comp.WitnessRange);

        foreach (var possibleWitness in list)
        {
            if(!TryComp<MobStateComponent>(possibleWitness, out var mobStateComponent) )
                continue;

            MarkingSet markings = new MarkingSet();
            // copy the markings to make sure the markings e.g. hair and beard at the time of death is stored
            if (TryComp<HumanoidAppearanceComponent>(possibleWitness, out var appearanceComponent))
                markings = new MarkingSet(appearanceComponent.MarkingSet);

            ent.Comp.Witnesses.Add( new ObraDinnWitness( possibleWitness,
                Transform(possibleWitness).Coordinates,
                Identity.Name(possibleWitness,EntityManager,ent.Owner),
                mobStateComponent.CurrentState,
                markings
                ));
        }
        Dirty(ent);
    }
}
