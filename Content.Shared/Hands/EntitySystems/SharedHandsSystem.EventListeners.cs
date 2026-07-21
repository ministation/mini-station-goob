using Content.Shared.Hands.Components;
using Content.Shared.Stunnable;

namespace Content.Shared.Hands.EntitySystems;

/// <summary>
/// This is for events that don't affect normal hand functions but do care about hands.
/// </summary>
public abstract partial class SharedHandsSystem
{
    private void InitializeEventListeners()
    {
        SubscribeLocalEvent<HandsComponent, GetStandUpTimeEvent>(OnStandupArgs);
        SubscribeLocalEvent<HandsComponent, KnockedDownRefreshEvent>(OnKnockedDownRefresh);
    }

    /// <summary>
    /// Reduces the time it takes to stand up based on the number of hands we have available.
    /// </summary>
    private void OnStandupArgs(Entity<HandsComponent> ent, ref GetStandUpTimeEvent time)
    {
        if (!HasComp<KnockedDownComponent>(ent))
            return;

        var hands = GetEmptyHandCount(ent.Owner);

        if (hands == 0)
            return;

        time.DoAfterTime *= (float)ent.Comp.Count / (hands + ent.Comp.Count);
    }

    private void OnKnockedDownRefresh(Entity<HandsComponent> ent, ref KnockedDownRefreshEvent args)
    {
        // Can't crawl without any hands (e.g. severed / smite). Occupied hands no longer
        // scale crawl speed — holding items while prone was unusably slow.
        if (GetHandCount(ent.AsNullable()) == 0)
            args.SpeedModifier = 0f;
    }
}
