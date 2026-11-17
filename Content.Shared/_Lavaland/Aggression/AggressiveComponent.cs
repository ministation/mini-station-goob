// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Aggression;

/// <summary>
///     Keeps track of whoever attacked our mob, so that it could prioritize or randomize targets.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AggressiveComponent : Component
{
    /// <summary>
    /// Active aggressors, that this aggressor will try to attack.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Aggressors = new();

    [ViewVariables]
    public TimeSpan NextUpdate;

    /// <summary>
    /// If specified, will forgive the target after it enters another map or
    /// goes farther than this range from it.
    /// </summary>
    [DataField]
    public float? ForgiveRange;

    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(10f);

    /// <summary>
    /// If false, aggressors are only added manually (e.g. Bubblegum blood step / custom rules).
    /// </summary>
    [DataField]
    public bool AutoAddOnDamage = true;

    /// <summary>
    /// Range in tiles at which this mob notices a nearby player and fights it without waiting to be hit,
    /// like TG hostile mobs finding targets inside their vision range. Zero disables proximity aggro.
    /// </summary>
    [DataField]
    public float ProximityAggroRange = 9f;

    [ViewVariables]
    public TimeSpan NextProximityScan;
}
