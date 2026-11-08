// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// TG parity: hurts everything nearby when the entity dies without being gibbed.
/// </summary>
[RegisterComponent]
public sealed partial class DeathExplosionComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public float Radius = 1f;
}
