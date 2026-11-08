// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Aquarium;
using Robust.Client.GameObjects;

namespace Content.Client._Mini.Fishing.Aquarium;

/// <summary>
/// Picks the tank sprite layers from the fluid, the glass state and the installed upgrades.
/// </summary>
public sealed partial class AquariumVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AquariumComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AquariumComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnStartup(Entity<AquariumComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent);
    }

    private void OnHandleState(Entity<AquariumComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<AquariumComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var prefix = ent.Comp.Upgrades.HasFlag(AquariumUpgradeFlags.BioelectricGenerator) ? "bioelec" : "aquarium";
        var water = ent.Comp.Fluid == AquariumFluid.Air ? "air" : "water";
        var broken = ent.Comp.GlassBroken ? "_broken" : string.Empty;

        _sprite.LayerSetRsiState(ent.Owner, "base", $"{prefix}_base");
        _sprite.LayerSetRsiState(ent.Owner, "glass", $"{prefix}_glass_{water}{broken}");
        _sprite.LayerSetRsiState(ent.Owner, "borders", $"{prefix}_borders");
    }
}
