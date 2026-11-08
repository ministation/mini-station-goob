// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

using Content.Shared._Mini.Fishing.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Robust.Shared.Player;

namespace Content.Shared._Mini.Fishing.Systems;

/// <summary>
/// Keeps a cast float out of other people's hands and keeps the rod from swinging.
/// Runs on both sides, so the client does not predict the pickup or the melee arc
/// that copies the rod sprite next to the float.
/// </summary>
public sealed partial class FishingLureSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActorComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<FishingLureComponent, GettingPickedUpAttemptEvent>(OnLurePickedUp);
    }

    private void OnAttackAttempt(Entity<ActorComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Weapon is { } weapon && HasComp<FishingRodComponent>(weapon.Owner))
            args.Cancel();
    }

    private void OnLurePickedUp(Entity<FishingLureComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        // A float that is in the water belongs to the rod, not to the hands of passers-by.
        args.Cancel();
    }
}
