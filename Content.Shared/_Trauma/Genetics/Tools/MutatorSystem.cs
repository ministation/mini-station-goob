// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared._Trauma.Genetics.Console;
using Content.Shared._Trauma.Genetics.Mutations;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Trauma.Genetics.Tools;

public sealed partial class MutatorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MutationSystem _mutation = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutatorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MutatorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<MutatorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<MutatorComponent, MutatorDoAfterEvent>(OnDoAfter);
    }

    private void OnExamined(Entity<MutatorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var msg = IsSpent(ent.Comp)
            ? "mutator-examine-spent"
            : "mutator-examine-loaded";
        args.PushMarkup(Loc.GetString(msg));
    }

    private void OnAfterInteract(Entity<MutatorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not {} target)
            return;

        args.Handled = true;
        StartInject(ent, target, args.User);
    }

    private void OnUseInHand(Entity<MutatorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        StartInject(ent, args.User, args.User);
    }

    public void StartInject(Entity<MutatorComponent> ent, EntityUid target, EntityUid user)
    {
        if (IsSpent(ent.Comp))
        {
            _popup.PopupEntity(Loc.GetString("mutator-depleted"), user, user);
            return;
        }

        var targetName = Identity.Name(target, EntityManager);
        if (!_mutation.CanMutate(target))
        {
            _popup.PopupEntity(Loc.GetString("mutator-cant-mutate", ("target", targetName)), user, user);
            return;
        }

        // injecting someone else takes twice as long
        var delay = ent.Comp.InjectTime;
        if (user != target)
            delay *= 2;

        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            user,
            delay,
            new MutatorDoAfterEvent(),
            eventTarget: ent,
            target: target,
            used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        }))
            return;

        var userName = Identity.Name(user, EntityManager);
        var you = Loc.GetString("mutator-mutating-you", ("user", userName), ("item", ent));
        var others = Loc.GetString("mutator-mutating-others", ("user", userName), ("target", targetName), ("item", ent));
        _popup.PopupPredicted(you, others, ent, target);
    }

    private void OnDoAfter(Entity<MutatorComponent> ent, ref MutatorDoAfterEvent args)
    {
        if (!_timing.IsFirstTimePredicted || args.Cancelled || args.Target is not {} target)
            return;

        // prevent TOCTOU
        if (IsSpent(ent.Comp) || _mutation.GetMutatable(target) is not {} mutatable)
            return;

        args.Handled = true;

        var body = mutatable.AsNullable();
        if (ent.Comp.Remove)
        {
            _mutation.RemoveMutations(body, ent.Comp.Mutations, user: args.User, predicted: true);
            // TODO: maybe do genetic damage if it succeeded
        }
        else if (ent.Comp.Activator)
        {
            _mutation.ActivateMutations(mutatable, ent.Comp.Mutations, user: args.User, predicted: true);
        }
        else if (ent.Comp.InjectRandomMutation)
        {
            // The roll has to touch every mutation prototype and cannot be predicted,
            // so only the server does it and replicates the resulting mutation entity.
            if (_net.IsClient)
                return;

            TryAddRandomMutation(body, args.User);
        }
        else
        {
            _mutation.AddMutations(body, ent.Comp.Mutations, user: args.User, predicted: true);
        }

        // prevent reuse
        ent.Comp.Spent = true;
        ent.Comp.Mutations.Clear();
        Dirty(ent);
        UpdateAppearance(ent);
        // allow recycling in disposals
        _tag.AddTag(ent, TrashTag);
    }

    private bool TryAddRandomMutation(Entity<MutatableComponent?> target, EntityUid? user)
    {
        var candidates = _mutation.GetRandomMutations();

        // Requirements and conflicts can make an individual mutation fail, so keep
        // rolling until one sticks instead of wasting the injector.
        while (candidates.Count > 0)
        {
            var index = _random.Next(candidates.Count);
            var id = candidates[index];
            candidates.RemoveAt(index);

            if (_mutation.AddMutation(target, id, user: user, predicted: false))
                return true;
        }

        return false;
    }

    private static bool IsSpent(MutatorComponent component)
        => component.Spent || (!component.InjectRandomMutation && component.Mutations.Count == 0);

    private void UpdateAppearance(Entity<MutatorComponent> ent)
    {
        _appearance.SetData(ent, MutatorVisuals.Spent, IsSpent(ent.Comp));
    }

    #region Public API

    public void AddMutation(Entity<MutatorComponent?> ent, EntProtoId<MutationComponent> id)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.Mutations.Add(id);
        ent.Comp.Spent = false;
        Dirty(ent, ent.Comp);
        UpdateAppearance((ent, ent.Comp));
    }

    #endregion
}

[Serializable, NetSerializable]
public sealed partial class MutatorDoAfterEvent : SimpleDoAfterEvent;
