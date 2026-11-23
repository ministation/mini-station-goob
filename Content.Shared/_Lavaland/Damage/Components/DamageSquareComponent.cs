// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Damage.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DamageSquareComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public DamageSpecifier Damage;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? DamageWhitelist;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? DamageBlacklist;

    /// <summary>
    /// Extra damage applied on top of <see cref="Damage"/> to lavaland fauna that is not player controlled.
    /// TG parity: hierophant blasts hit megafauna and asteroid fauna for extra brute damage.
    /// Empty by default, so drake, colossus and legion tiles are unchanged.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier? MonsterBonusDamage;

    [DataField, AutoNetworkedField]
    public SoundPathSpecifier? Sound;

    /// <summary>
    /// After how many seconds we should deal the damage to all entities above.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageDelay = 0.2f;

    /// <summary>
    /// Time when this square is going to deal damage. Used for prediction to work.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan DamageTime = TimeSpan.MaxValue;

    /// <summary>
    /// For how many seconds we add immunity to the entity we hit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ImmunityTime = 0.5f;
}
