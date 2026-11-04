// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Interaction.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Bottled wendigo blood: gives the drinker the bear form action and is used up.
/// </summary>
[RegisterComponent]
public sealed partial class WendigoBloodComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionWendigoBearForm";

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/drink.ogg");
}

public sealed class WendigoBloodSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WendigoBloodComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<WendigoBloodComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _actions.AddAction(args.User, ent.Comp.Action.Id);
        _audio.PlayPvs(ent.Comp.Sound, args.User);
        QueueDel(ent);
    }
}
