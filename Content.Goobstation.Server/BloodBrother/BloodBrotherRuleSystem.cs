// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.BloodBrother;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Charges;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives;
using Content.Server.Objectives.Components;
using Content.Server.Popups;
using Content.Shared.Charges.Components;
using Content.Shared.Flash;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Goobstation.Server.BloodBrother;

/// <summary>
/// Handles the blood brothers antagonist: lone founders, flash based recruitment,
/// shared objectives and round-end text.
/// </summary>
public sealed partial class BloodBrotherRuleSystem : GameRuleSystem<BloodBrotherRuleComponent>
{
    private static readonly EntProtoId MindRole = "MindRoleBloodBrother";
    private static readonly EntProtoId ConvertObjective = "BloodBrotherConvertObjective";
    private static readonly EntProtoId EscapeObjective = "BloodBrotherEscapeObjective";
    private static readonly EntProtoId HijackObjective = "HijackShuttleObjective";
    private static readonly EntProtoId Flash = "Flash";

    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private ChargesSystem _charges = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ObjectivesSystem _objectivePool = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedObjectivesSystem _objectives = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBrotherRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<BloodBrotherComponent, AfterFlashedEvent>(OnBrotherFlashed);
        SubscribeLocalEvent<BloodBrotherTeamObjectiveComponent, StealConditionHoldersEvent>(OnTeamObjectiveHolders);
    }

    private void OnAntagSelected(Entity<BloodBrotherRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        // A player is only ever the founder of a single conspiracy.
        if (HasComp<BloodBrotherComponent>(args.EntityUid))
            return;

        if (!_mind.TryGetMind(args.EntityUid, out var mindId, out var mind))
            return;

        var team = CreateTeam(ent);

        var brother = EnsureComp<BloodBrotherComponent>(args.EntityUid);
        brother.Rule = ent.Owner;
        brother.TeamId = team.Id;
        brother.ConversionsLeft = ent.Comp.StartingConversions;
        Dirty(args.EntityUid, brother);

        team.Minds.Add(mindId);
        ent.Comp.TeamByMind[mindId] = team;

        // Every brother receives their objectives right away, even while still alone.
        ForgeObjectives(ent, team, mindId, mind);

        DirtyBrothers();
    }

    #region Teams

    /// <summary>
    /// Creates a brand new conspiracy with a single founder.
    /// The identifier mixes the rule entity id with a local counter, so two rules
    /// running at the same time can never produce the same team id.
    /// </summary>
    private BloodBrotherTeam CreateTeam(Entity<BloodBrotherRuleComponent> ent)
    {
        var team = new BloodBrotherTeam
        {
            Id = ((long) ent.Owner.Id << 32) | (uint) ent.Comp.NextTeamId++,
        };

        ent.Comp.Teams.Add(team);
        return team;
    }

    private static BloodBrotherTeam? GetTeam(BloodBrotherRuleComponent rule, long teamId)
    {
        foreach (var team in rule.Teams)
        {
            if (team.Id == teamId)
                return team;
        }

        return null;
    }

    /// <summary>
    /// A newly added brother has never received the state of the other brothers' components,
    /// so all of them have to be dirtied again for the new client to see the team icons.
    /// </summary>
    private void DirtyBrothers()
    {
        var query = AllEntityQuery<BloodBrotherComponent>();
        while (query.MoveNext(out var uid, out var comp))
            Dirty(uid, comp);
    }

    #endregion

    #region Objectives

    private void ForgeObjectives(Entity<BloodBrotherRuleComponent> ent, BloodBrotherTeam team, EntityUid mindId, MindComponent mind)
    {
        if (TryCreateTeamObjective(team, mindId, mind, ConvertObjective) is { } convert)
            team.Objectives.Add(convert);

        var hijack = RobustRandom.Prob(ent.Comp.HijackChance);
        var amount = Math.Max(1, ent.Comp.ObjectiveAmount - (hijack ? 1 : 0));

        for (var i = 0; i < amount; i++)
        {
            if (PickTeamObjective(ent, team, mindId, mind) is not { } objective)
                break;

            team.Objectives.Add(objective);
        }

        var escapeProto = hijack ? HijackObjective : EscapeObjective;
        if (TryCreateTeamObjective(team, mindId, mind, escapeProto) is { } escape)
            team.Objectives.Add(escape);
    }

    /// <summary>
    /// Makes sure that a newly joined brother is not the target of one of the team's objectives.
    /// Founders get their objectives before they know who they will recruit, so a kill or steal
    /// objective can randomly point at the person they later bring into the conspiracy.
    /// </summary>
    private void RerollConflictingObjectives(Entity<BloodBrotherRuleComponent> ent, BloodBrotherTeam team, EntityUid mindId)
    {
        if (team.Minds.Count < 2 || !TryComp<MindComponent>(team.Minds[0], out var ownerMind))
            return;

        for (var i = team.Objectives.Count - 1; i >= 0; i--)
        {
            var objective = team.Objectives[i];
            if (!TryComp<TargetObjectiveComponent>(objective, out var target) || target.Target != mindId)
                continue;

            RemoveTeamObjective(team, objective);
            if (PickTeamObjective(ent, team, team.Minds[0], ownerMind) is { } replacement)
                team.Objectives.Add(replacement);
        }
    }

    private void RemoveTeamObjective(BloodBrotherTeam team, EntityUid objective)
    {
        team.Objectives.Remove(objective);

        foreach (var mindId in team.Minds)
        {
            if (!TryComp<MindComponent>(mindId, out var mind))
                continue;

            var index = mind.Objectives.IndexOf(objective);
            if (index >= 0)
                _mind.TryRemoveObjective(mindId, mind, index);
        }
    }

    /// <summary>
    /// Picks a random objective for the team, making sure it does not target a member of the team.
    /// </summary>
    private EntityUid? PickTeamObjective(Entity<BloodBrotherRuleComponent> ent,
        BloodBrotherTeam team,
        EntityUid mindId,
        MindComponent mind)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var objective = _objectivePool.GetRandomObjective(mindId, mind, ent.Comp.ObjectiveGroup, ent.Comp.MaxObjectiveDifficulty);
            if (objective == null)
                return null;

            if (TargetsTeamMember(objective.Value, team))
            {
                Del(objective.Value);
                continue;
            }

            AddComp(objective.Value, new BloodBrotherTeamObjectiveComponent { Team = team });
            AddObjectiveToTeam(team, objective.Value);
            return objective.Value;
        }

        return null;
    }

    private bool TargetsTeamMember(EntityUid objective, BloodBrotherTeam team)
    {
        return team.Minds.Count > 0 &&
               TryComp<TargetObjectiveComponent>(objective, out var target) &&
               target.Target is { } targetMind &&
               team.Minds.Contains(targetMind);
    }

    private void AddObjectiveToTeam(BloodBrotherTeam team, EntityUid objective)
    {
        foreach (var mindId in team.Minds)
        {
            if (!TryComp<MindComponent>(mindId, out var mind) || mind.Objectives.Contains(objective))
                continue;

            _mind.AddObjective(mindId, mind, objective);
        }
    }

    private EntityUid? TryCreateTeamObjective(BloodBrotherTeam team, EntityUid mindId, MindComponent mind, EntProtoId proto)
    {
        var objective = _objectives.TryCreateObjective(mindId, mind, proto.Id);
        if (objective == null)
            return null;

        AddComp(objective.Value, new BloodBrotherTeamObjectiveComponent { Team = team });
        AddObjectiveToTeam(team, objective.Value);
        return objective.Value;
    }

    /// <summary>
    /// Brothers share a single objective entity, so an item held by any of them
    /// counts for the whole conspiracy. The steal condition asks the objective for
    /// extra bodies to search, and this handler supplies the other brothers.
    /// </summary>
    private void OnTeamObjectiveHolders(Entity<BloodBrotherTeamObjectiveComponent> ent, ref StealConditionHoldersEvent args)
    {
        if (ent.Comp.Team is not { } team)
            return;

        foreach (var mindId in team.Minds)
        {
            if (mindId == args.MindId)
                continue;

            if (TryComp<MindComponent>(mindId, out var mind) && mind.OwnedEntity is { } body)
                args.Holders.Add(body);
        }
    }

    private bool IsObjectiveTarget(BloodBrotherTeam team, EntityUid mindId)
    {
        foreach (var objective in team.Objectives)
        {
            if (TryComp<TargetObjectiveComponent>(objective, out var target) && target.Target == mindId)
                return true;
        }

        return false;
    }

    #endregion

    #region Conversion

    private void OnBrotherFlashed(Entity<BloodBrotherComponent> ent, ref AfterFlashedEvent args)
    {
        if (!args.Melee || args.User != ent.Owner)
            return;

        if (!TryComp<BloodBrotherRuleComponent>(ent.Comp.Rule, out var rule))
            return;

        var team = GetTeam(rule, ent.Comp.TeamId);
        if (team == null)
            return;

        var target = args.Target;

        if (target == ent.Owner)
            return;

        // The recruiter spends their own charge, not a shared team pool.
        if (ent.Comp.ConversionsLeft <= 0)
        {
            _popup.PopupEntity(Loc.GetString("blood-brother-convert-fail-empty"), target, ent.Owner);
            return;
        }

        if (team.Minds.Count >= rule.MaxTeamSize)
        {
            _popup.PopupEntity(Loc.GetString("blood-brother-convert-fail-full"), target, ent.Owner);
            return;
        }

        if (!_mobState.IsAlive(target))
        {
            _popup.PopupEntity(Loc.GetString("blood-brother-convert-fail-unconscious"), target, ent.Owner);
            return;
        }

        // Only humanoids with a player behind them can become brothers.
        if (!HasComp<HumanoidAppearanceComponent>(target) || !HasComp<ActorComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("blood-brother-convert-fail-vacant"), target, ent.Owner);
            return;
        }

        if (!_mind.TryGetMind(target, out var targetMindId, out var targetMind))
            return;

        if (HasComp<MindShieldComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("blood-brother-convert-fail-mindshield"), target, ent.Owner);
            return;
        }

        if (HasComp<BloodBrotherComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("blood-brother-convert-fail-already"), target, ent.Owner);
            return;
        }

        if (HasComp<AntagImmuneComponent>(target) || _role.MindIsAntagonist(targetMindId))
        {
            _popup.PopupEntity(Loc.GetString("blood-brother-convert-fail-antag"), target, ent.Owner);
            return;
        }

        if (IsObjectiveTarget(team, targetMindId))
        {
            _popup.PopupEntity(Loc.GetString("blood-brother-convert-fail-target"), target, ent.Owner);
            return;
        }

        Convert(ent, rule, team, target, targetMindId, targetMind, args.Used);
    }

    private void Convert(Entity<BloodBrotherComponent> source,
        BloodBrotherRuleComponent rule,
        BloodBrotherTeam team,
        EntityUid target,
        EntityUid targetMindId,
        MindComponent targetMind,
        EntityUid? used)
    {
        _role.MindAddRole(targetMindId, MindRole, targetMind, true);

        var brother = EnsureComp<BloodBrotherComponent>(target);
        brother.Rule = source.Comp.Rule;
        brother.TeamId = team.Id;
        brother.ConversionsLeft = 0;

        // The recruiter spends their flash and their conversion charge.
        source.Comp.ConversionsLeft = 0;
        Dirty(source.Owner, source.Comp);

        team.Minds.Add(targetMindId);
        rule.TeamByMind[targetMindId] = team;

        // The new brother must not be the target of the conspiracy's own objectives.
        RerollConflictingObjectives((source.Comp.Rule, rule), team, targetMindId);

        // A partner inherits every objective of the founder, including the ones forged at spawn.
        foreach (var objective in team.Objectives)
        {
            if (!targetMind.Objectives.Contains(objective))
                _mind.AddObjective(targetMindId, targetMind, objective);
        }

        // Rarely the recruit gets their own flash and can bring in a third brother.
        // A conspiracy that is already at its limit gets nothing to spend it on.
        if (team.Minds.Count < rule.MaxTeamSize && RobustRandom.Prob(rule.PartnerFlashChance))
        {
            brother.ConversionsLeft = 1;
            GiveFlash(target);
            _antag.SendBriefing(target, Loc.GetString("blood-brother-partner-flash"), Color.Crimson, null);
        }

        Dirty(target, brother);
        DirtyBrothers();
        NotifyTeam(team, targetMindId, target, source.Owner);

        // The flash burns out after a successful recruitment, like in TG.
        BurnFlash(used);
    }

    /// <summary>
    /// Hands a fresh flash to a brother that rolled the partner flash chance.
    /// If their hands are full the flash simply drops at their feet.
    /// </summary>
    private void GiveFlash(EntityUid target)
    {
        var flash = Spawn(Flash, Transform(target).Coordinates);
        _hands.TryPickupAnyHand(target, flash);
    }

    private void BurnFlash(EntityUid? used)
    {
        if (used is not { } flash ||
            !TryComp<LimitedChargesComponent>(flash, out var charges) ||
            _charges.GetCurrentCharges((flash, charges)) <= 0)
        {
            return;
        }

        _charges.SetCharges((flash, charges), 0);
        _appearance.SetData(flash, FlashVisuals.Burnt, true);
    }

    private void NotifyTeam(BloodBrotherTeam team, EntityUid newMindId, EntityUid newBody, EntityUid? recruiter)
    {
        var others = new List<string>();

        foreach (var mindId in team.Minds)
        {
            if (mindId == newMindId)
                continue;

            if (!TryComp<MindComponent>(mindId, out var mind) || mind.OwnedEntity is not { } body)
                continue;

            others.Add(Name(body));

            _antag.SendBriefing(body,
                Loc.GetString("blood-brother-partner-converted", ("name", Name(newBody))),
                Color.Crimson,
                null);
        }

        if (others.Count == 0)
            return;

        var brothers = string.Join(", ", others);
        var greeting = recruiter is { } source
            ? Loc.GetString("blood-brother-converted-greeting",
                ("recruiter", Name(source)),
                ("brothers", brothers))
            : Loc.GetString("blood-brother-partner-list", ("brothers", brothers));

        _antag.SendBriefing(newBody, greeting, Color.Crimson, null);
    }

    #endregion

    protected override void AppendRoundEndText(EntityUid uid,
        BloodBrotherRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        if (component.Teams.Count == 0)
            return;

        args.AddLine(Loc.GetString("blood-brother-round-end-count", ("count", component.Teams.Count)));

        var index = 1;
        foreach (var team in component.Teams)
        {
            var members = new List<string>();
            Entity<MindComponent>? evaluator = null;

            foreach (var mindId in team.Minds)
            {
                if (!TryComp<MindComponent>(mindId, out var mind))
                    continue;

                evaluator ??= (mindId, mind);

                if (mind.OwnedEntity is { } body)
                    members.Add(Name(body));
            }

            var completed = 0;
            if (evaluator is { } eval)
            {
                foreach (var objective in team.Objectives)
                {
                    if (_objectives.IsCompleted(objective, eval))
                        completed++;
                }
            }

            args.AddLine(Loc.GetString("blood-brother-round-end-team",
                ("index", index++),
                ("members", string.Join(", ", members)),
                ("completed", completed),
                ("total", team.Objectives.Count)));
        }
    }
}
