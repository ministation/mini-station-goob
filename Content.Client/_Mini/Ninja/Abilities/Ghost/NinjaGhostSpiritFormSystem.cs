// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Abilities.Ghost;
using Robust.Client.GameObjects;

namespace Content.Client._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Tints the sprite of the suit wearer while the spirit form is active.
/// </summary>
public sealed class NinjaGhostSpiritFormSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    /// <summary>
    /// Suit to the wearer whose sprite is currently tinted.
    /// </summary>
    private readonly Dictionary<EntityUid, EntityUid> _tinted = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaGhostSpiritFormComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<NinjaGhostSpiritFormComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NinjaGhostSpiritFormComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnState(Entity<NinjaGhostSpiritFormComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Active)
            Apply(ent.Owner, ent.Comp);
        else
            RestoreSuit(ent.Owner);
    }

    private void OnStartup(Entity<NinjaGhostSpiritFormComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Active)
            Apply(ent.Owner, ent.Comp);
    }

    private void OnShutdown(Entity<NinjaGhostSpiritFormComponent> ent, ref ComponentShutdown args)
    {
        RestoreSuit(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaGhostSpiritFormComponent>();
        while (query.MoveNext(out var suit, out var comp))
        {
            if (comp.Active)
                Apply(suit, comp);
        }
    }

    private void Apply(EntityUid suit, NinjaGhostSpiritFormComponent comp)
    {
        var wearer = Transform(suit).ParentUid;
        if (!wearer.IsValid())
            return;

        if (_tinted.TryGetValue(suit, out var previous) && previous != wearer)
            Restore(previous);

        if (!TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        _sprite.SetColor((wearer, (SpriteComponent?) sprite), comp.SpiritFormColor.WithAlpha(comp.SpiritFormAlpha));
        _tinted[suit] = wearer;
    }

    private void RestoreSuit(EntityUid suit)
    {
        if (!_tinted.Remove(suit, out var wearer))
            return;

        Restore(wearer);
    }

    private void Restore(EntityUid wearer)
    {
        if (!wearer.IsValid() || !TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        _sprite.SetColor((wearer, (SpriteComponent?) sprite), Color.White);
    }
}
