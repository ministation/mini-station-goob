namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Raised after war stations are merged onto one map and trade posts repositioned.
/// </summary>
public sealed class TypanWarLayoutReadyEvent : EntityEventArgs
{
    public EntityUid Rule;
    public EntityUid NtStation;
    public EntityUid TypanStation;

    public TypanWarLayoutReadyEvent(EntityUid rule, EntityUid ntStation, EntityUid typanStation)
    {
        Rule = rule;
        NtStation = ntStation;
        TypanStation = typanStation;
    }
}
