// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Fauna;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Lavaland.Fauna;

/// <summary>
/// Handles filling food troughs with ore and handing ore out to feeders.
/// </summary>
public sealed class FoodTroughSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FoodTroughComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<FoodTroughComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var comp = ent.Comp;
        if (comp.OreCount >= comp.MaxOre)
            return;

        // One ore piece per interaction, like tossing handfuls into the trough.
        if (!_tag.HasTag(args.Used, comp.OreTag))
            return;

        args.Handled = true;

        if (!EntityManager.TryGetComponent(args.Used, out StackComponent? stackComp) || !_stack.TryUse((args.Used, stackComp), 1))
            return;

        comp.OreCount++;
        _audio.PlayPvs("/Audio/Effects/Fluids/splat.ogg", ent, AudioParams.Default.WithVolume(-8f));
    }

    /// <summary>
    /// Takes one stored ore piece out of the trough for a feeding animal.
    /// </summary>
    public bool TryTakeOre(Entity<FoodTroughComponent> ent)
    {
        if (ent.Comp.OreCount <= 0)
            return false;

        ent.Comp.OreCount--;
        return true;
    }
}
