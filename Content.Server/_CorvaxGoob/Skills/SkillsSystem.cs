using System.Linq;
using Content.Server.GameTicking.Events;
using Content.Shared._CorvaxGoob.CCCVars;
using Content.Shared._CorvaxGoob.Skills;
using SkillTypes = Content.Shared._CorvaxGoob.Skills.Skills;
using Content.Shared.Implants;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxGoob.Skills;

public sealed partial class SkillsSystem : SharedSkillsSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public static readonly ProtoId<TagPrototype> SkillsTag = "Skills";
    private bool _skillsEnabled = true;

    private readonly HashSet<HashSet<SkillTypes>> _ownedSkillSets = new();

    public override void Initialize()
    {
        base.Initialize();

        _skillsEnabled = _cfg.GetCVar(CCCVars.SkillsEnabled);
        Subs.CVar(_cfg, CCCVars.SkillsEnabled, value => _skillsEnabled = value);

        SubscribeLocalEvent<ImplantImplantedEvent>(OnImplantImplanted);
        SubscribeLocalEvent<EntitySkillsComponent, ComponentInit>(OnSkillsComponentInit);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);

        DedupeAllEntitySkills();
    }

    public bool IsSkillsEnabled() => _skillsEnabled;

    public override bool HasSkill(EntityUid entity, SkillTypes skill)
    {
        if (!_skillsEnabled)
            return true;

        if (HasComp<IgnoreSkillsComponent>(entity))
            return true;

        if (!TryComp<EntitySkillsComponent>(entity, out var skills))
            return false;

        EnsureSkillsInstance(entity, skills);

        if (skills.Skills.Contains(SkillTypes.All))
            return true;

        return skills.Skills.Contains(skill);
    }

    public bool TryGetSkills(EntityUid entity, out HashSet<SkillTypes> skills)
    {
        skills = new HashSet<SkillTypes>();

        if (!TryComp<EntitySkillsComponent>(entity, out var comp))
            return false;

        EnsureSkillsInstance(entity, comp);
        skills = comp.Skills;
        return true;
    }

    private void OnSkillsComponentInit(Entity<EntitySkillsComponent> ent, ref ComponentInit args)
    {
        EnsureSkillsInstance(ent.Owner, ent.Comp);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        _ownedSkillSets.Clear();
        DedupeAllEntitySkills();
    }

    private void DedupeAllEntitySkills()
    {
        var query = EntityQueryEnumerator<EntitySkillsComponent>();
        while (query.MoveNext(out var uid, out var skills))
            EnsureSkillsInstance(uid, skills);
    }

    private void EnsureSkillsInstance(EntityUid uid, EntitySkillsComponent skills)
    {
        if (_ownedSkillSets.Add(skills.Skills))
            return;

        skills.Skills = new HashSet<SkillTypes>(skills.Skills);
        Dirty(uid, skills);
        _ownedSkillSets.Add(skills.Skills);
    }

    private EntitySkillsComponent EnsureSkills(EntityUid entity)
    {
        var comp = EnsureComp<EntitySkillsComponent>(entity);
        EnsureSkillsInstance(entity, comp);
        return comp;
    }

    private void OnImplantImplanted(ref ImplantImplantedEvent ev)
    {
        if (!_tag.HasTag(ev.Implant, SkillsTag))
            return;

        GrantAllSkills(ev.Implanted);
    }

    public void GrantAllSkills(EntityUid entity) => GrantSkill(entity, SkillTypes.All);

    public void GrantSkill(EntityUid entity, HashSet<SkillTypes> skills, bool clearSkills = false)
    {
        var comp = EnsureSkills(entity);
        var oldSkills = new HashSet<SkillTypes>(comp.Skills);
        var incoming = new HashSet<SkillTypes>(skills);
        var bodySkills = new HashSet<SkillTypes>(comp.Skills);

        if (clearSkills)
            bodySkills.Clear();

        if (incoming.Count < 1)
        {
            comp.Skills = bodySkills;
            Dirty(entity, comp);
            return;
        }

        if (incoming.Contains(SkillTypes.All))
            bodySkills = new HashSet<SkillTypes> { SkillTypes.All };
        else
            bodySkills.UnionWith(incoming);

        comp.Skills = bodySkills;
        Dirty(entity, comp);
        _ownedSkillSets.Add(comp.Skills);

        var newSkills = new HashSet<SkillTypes>(comp.Skills);
        newSkills.ExceptWith(oldSkills);

        if (newSkills.Count < 1)
            return;

        var skillsMassive = string.Join(", ", newSkills.Select(s => s.ToString()));
        Log.Info($"Grant {(incoming.Contains(SkillTypes.All) ? $"{SkillTypes.All}" : skillsMassive)} skills to entity {entity.Id}. Clear skills: {clearSkills}");
    }

    public void GrantSkill(EntityUid entity, bool clearSkills = false, params SkillTypes[] skills)
        => GrantSkill(entity, new HashSet<SkillTypes>(skills), clearSkills);

    public void GrantSkill(EntityUid entity, SkillTypes skill, bool clearSkills = false)
        => GrantSkill(entity, new HashSet<SkillTypes> { skill }, clearSkills);

    public void RevokeSkill(EntityUid entity, HashSet<SkillTypes> skills)
    {
        if (!TryComp<EntitySkillsComponent>(entity, out var comp))
            return;

        EnsureSkillsInstance(entity, comp);

        if (skills.Count < 1)
            return;

        var oldSkills = new HashSet<SkillTypes>(comp.Skills);
        var incoming = new HashSet<SkillTypes>(skills);
        var bodySkills = new HashSet<SkillTypes>(comp.Skills);

        if (incoming.Contains(SkillTypes.All))
            bodySkills.Clear();
        else
        {
            foreach (var skill in incoming)
                bodySkills.Remove(skill);
        }

        comp.Skills = bodySkills;
        Dirty(entity, comp);
        _ownedSkillSets.Add(comp.Skills);

        var revokedSkills = new HashSet<SkillTypes>(oldSkills);
        revokedSkills.ExceptWith(comp.Skills);

        if (revokedSkills.Count < 1)
            return;

        var skillsMassive = string.Join(", ", revokedSkills.Select(s => s.ToString()));
        Log.Info($"Revoke {(incoming.Contains(SkillTypes.All) ? $"{SkillTypes.All}" : skillsMassive)} skills from entity {entity.Id}");
    }

    public void RevokeSkill(EntityUid entity, params SkillTypes[] skills)
        => RevokeSkill(entity, new HashSet<SkillTypes>(skills));

    public void RevokeSkill(EntityUid entity, SkillTypes skill)
        => RevokeSkill(entity, new HashSet<SkillTypes> { skill });
}
