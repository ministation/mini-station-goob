// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._CorvaxGoob.Skills;
using Content.Server.Body.Systems;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared._Lavaland.Necropolis;
using Content.Shared.Destructible;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;

namespace Content.Server._Lavaland.Ashwalker;

/// <summary>
/// TG ash walker eggs: hatching is limited to one spawn per ckey per tribe (sacrifices can
/// clear it, see <see cref="Necropolis.AshwalkerNestSystem"/>), and a broken egg leaves the
/// half-formed walker ("yolk") which is gibbed on the spot.
/// </summary>
public sealed class AshwalkerEggSystem : EntitySystem
{
    [Dependency] private readonly AshwalkerTribeSystem _tribe = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRoles = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string Ashwalker = "MobAshwalker";
    private const string Yolk = "MobAshwalkerPlayable";
    private const string YolkName = "ashwalker-egg-yolk-name";

    /// <summary>
    /// How close a nest must be for a freshly spawned egg to join its tribe.
    /// </summary>
    private const float NestRange = 10f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AshwalkerEggComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AshwalkerEggComponent, TakeGhostRoleEvent>(OnTakeGhostRole);
        SubscribeLocalEvent<AshwalkerEggComponent, DestructionEventArgs>(OnDestroyed);
    }

    private void OnMapInit(Entity<AshwalkerEggComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Nest.IsValid())
            return;

        var eggPosition = Transform(ent.Owner).WorldPosition;
        var query = EntityQueryEnumerator<AshwalkerNestComponent, TransformComponent>();
        EntityUid? closest = null;
        var closestDistance = NestRange;

        while (query.MoveNext(out var nest, out _, out var nestXform))
        {
            var distance = (nestXform.WorldPosition - eggPosition).Length();
            if (distance >= closestDistance)
                continue;

            closest = nest;
            closestDistance = distance;
        }

        if (closest is not { } nestUid)
            return;

        ent.Comp.Nest = nestUid;
        _tribe.EnsureTribe(nestUid);
    }

    private void OnTakeGhostRole(Entity<AshwalkerEggComponent> ent, ref TakeGhostRoleEvent args)
    {
        if (!TryComp<GhostRoleComponent>(ent.Owner, out var role) || role.Taken || MetaData(ent.Owner).EntityPaused)
            return;

        // The nest can die before its eggs, and then there is no spawn limit left to enforce.
        TryComp<AshwalkerTribeComponent>(ent.Comp.Nest, out var tribe);

        if (tribe != null && tribe.SpawnedCkeys.Contains(args.Player.UserId))
        {
            _popup.PopupEntity(Loc.GetString("ashwalker-exhausted"), ent.Owner, args.Player);
            return;
        }

        var mob = Spawn(Ashwalker, Transform(ent.Owner).Coordinates);
        _transform.AttachToGridOrMap(mob);
        RaiseLocalEvent(mob, new GhostRoleSpawnerUsedEvent(ent.Owner, mob), true);

        if (role.MakeSentient)
            _mind.MakeSentient(mob, role.AllowMovement, role.AllowSpeech);

        EnsureComp<MindContainerComponent>(mob);
        _ghostRoles.GhostRoleInternalCreateMindAndTransfer(args.Player, ent.Owner, mob, role);
        // The egg replaces the ghost role mob spawner, so its declared skills must be applied here.
        _skills.GrantSkillOrDefault(mob, role.Skills, true);
        _ghostRoles.SetTaken(role, true);

        tribe?.SpawnedCkeys.Add(args.Player.UserId);
        QueueDel(ent.Owner);
        args.TookRole = true;
    }

    private void OnDestroyed(Entity<AshwalkerEggComponent> ent, ref DestructionEventArgs args)
    {
        var yolk = Spawn(Yolk, Transform(ent.Owner).Coordinates);
        _metaData.SetEntityName(yolk, Loc.GetString(YolkName));
        _body.GibBody(yolk, true);
    }
}
