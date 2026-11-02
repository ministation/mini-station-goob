// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Server.Chat.Systems;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// Lets held fish be used to commit suicide, like the tg /obj/item/fish/suicide_act overrides.
/// </summary>
public sealed class FishSuicideSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishComponent, SuicideByEnvironmentEvent>(OnSuicideByEnvironment);
    }

    private void OnSuicideByEnvironment(Entity<FishComponent> ent, ref SuicideByEnvironmentEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryFishSuicide(ent, args.Victim);
    }

    /// <summary>
    /// Applies the fish suicide message and lethal damage. Public so tests can suicide without a player session.
    /// </summary>
    public bool TryFishSuicide(Entity<FishComponent> fish, EntityUid user)
    {
        if (!TryComp<DamageableComponent>(user, out var damageable))
            return false;

        FishPrototype? proto = null;
        if (fish.Comp.Fish is { } protoId)
            _proto.TryIndex(protoId, out proto);

        var message = proto?.SuicideMessage ?? "fishing-suicide-slap";
        _chat.TrySendInGameICMessage(user,
            Loc.GetString(message, ("user", Identity.Name(user, EntityManager)), ("fish", Name(fish))),
            InGameICChatType.Emote,
            false);

        if (proto?.SuicideConsumes == true)
            QueueDel(fish);

        // shortcut: a flat lethal dose instead of tg per-fish damage numbers, suicide must be lethal.
        var type = _proto.Index(proto?.SuicideDamage ?? "Blunt");
        _damage.ChangeDamage((user, damageable), new DamageSpecifier(type, 1000), true, targetPart: TargetBodyPart.Chest);
        return true;
    }
}
