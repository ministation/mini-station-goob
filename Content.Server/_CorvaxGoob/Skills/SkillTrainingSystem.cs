using System.Linq;

using Content.Shared._CorvaxGoob.Skills;

using Content.Shared.DoAfter;

using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;

using Content.Shared.Interaction;

using Content.Shared.Interaction.Events;

using Content.Shared.Mobs.Components;

using Content.Shared.Mobs.Systems;

using Content.Shared.Popups;

using Content.Shared.UserInterface;

using Content.Shared.Verbs;

using Robust.Server.GameObjects;

using Robust.Shared.Random;

using Robust.Shared.Timing;

using Robust.Shared.Utility;

using SkillTypes = Content.Shared._CorvaxGoob.Skills.Skills;



namespace Content.Server._CorvaxGoob.Skills;



public sealed class SkillTrainingSystem : EntitySystem

{

    private const int SelfStudyStages = 3;

    private const int FastStages = 2;

    private const int ExpertWithBookStages = 1;



    private const float SelfReadDelay = 30f;

    private const float ExpertReadDelay = 10f;



    private const float ExpertWithBookDelay = 15f;

    private const float FastDoAfterDelay = 25f;



    private const int SelfStudyMinDelay = 30;

    private const int SelfStudyMaxDelay = 90;

    private const float TrainingCooldownSeconds = 60f;



    [Dependency] private readonly SkillsSystem _skills = default!;

    [Dependency] private readonly MobStateSystem _mobState = default!;

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    [Dependency] private readonly SharedPopupSystem _popup = default!;

    [Dependency] private readonly SharedHandsSystem _hands = default!;

    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    private const float TrainingInteractionRange = 2.5f;



    public override void Initialize()

    {

        base.Initialize();



        SubscribeLocalEvent<SkillBookComponent, UseInHandEvent>(OnBookUseInHand);

        SubscribeLocalEvent<SkillBookComponent, SkillBookReadDoAfterEvent>(OnBookReadDoAfter);

        SubscribeLocalEvent<MobStateComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<MobStateComponent, GetVerbsEvent<InteractionVerb>>(OnGetTeachVerbs);

        SubscribeLocalEvent<MobStateComponent, SkillTrainingSelectSkillMessage>(OnSelectSkillFromUi);

        SubscribeLocalEvent<MobStateComponent, BoundUIClosedEvent>(OnTrainingUiClosed);

        SubscribeLocalEvent<SkillTrainingStudentComponent, SkillTrainingDoAfterEvent>(OnTrainingDoAfter);

    }



    private void OnGetTeachVerbs(Entity<MobStateComponent> ent, ref GetVerbsEvent<InteractionVerb> args)

    {

        if (!args.CanInteract || !args.CanAccess)

            return;



        if (!_skills.IsSkillsEnabled() || !IsValidStudent(ent.Owner))

            return;



        if (!GetAvailableSkills(args.User, ent.Owner).Any())

            return;



        var teacher = args.User;

        var student = ent.Owner;

        args.Verbs.Add(new InteractionVerb

        {

            Text = Loc.GetString("skill-training-verb-open"),

            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),

            Act = () => TryOpenTrainingUi(teacher, student),

        });

    }



    private void OnSelectSkillFromUi(Entity<MobStateComponent> ent, ref SkillTrainingSelectSkillMessage args)

    {

        if (args.Actor == default)

            return;



        if (!TryComp<SkillTrainingUiComponent>(ent, out var session) || session.Teacher != args.Actor)

            return;



        var teacher = args.Actor;

        var student = ent.Owner;



        if (!_skills.IsSkillsEnabled() || !IsValidStudent(student))

            return;



        if (_skills.HasSkill(student, args.Skill))

        {

            _popup.PopupEntity(Loc.GetString("skill-training-fail-already", ("skill", GetSkillLocName(args.Skill))), teacher, teacher);

            return;

        }



        if (!GetAvailableSkills(teacher, student).Contains(args.Skill))

            return;



        _ui.CloseUi(student, SkillTrainingUiKey.Key, teacher);



        if (TryGetSkillBookInHand(teacher, args.Skill, out var book))

        {

            TryStartReading(teacher, book, args.Skill, student);

            return;

        }



        if (CanTeachWithoutBook(teacher, args.Skill, out _))

        {

            TryStartExpertTeach(teacher, student, args.Skill);

            return;

        }



        _popup.PopupEntity(Loc.GetString("skill-training-fail-no-skill"), teacher, teacher);

    }



    private void OnTrainingUiClosed(Entity<MobStateComponent> ent, ref BoundUIClosedEvent args)

    {

        if (!Equals(args.UiKey, SkillTrainingUiKey.Key))

            return;



        RemComp<SkillTrainingUiComponent>(ent);

    }



    private void TryOpenTrainingUi(EntityUid teacher, EntityUid student)

    {

        if (!_skills.IsSkillsEnabled() || !IsValidStudent(student))

            return;



        var skills = GetAvailableSkills(teacher, student).ToList();

        if (skills.Count == 0)

        {

            _popup.PopupEntity(Loc.GetString("skill-training-fail-nothing-to-teach"), teacher, teacher);

            return;

        }



        var session = EnsureComp<SkillTrainingUiComponent>(student);

        session.Teacher = teacher;

        Dirty(student, session);



        _ui.SetUiState(student, SkillTrainingUiKey.Key, new SkillTrainingBoundUserInterfaceState(skills));

        _ui.TryOpenUi(student, SkillTrainingUiKey.Key, teacher);

    }



    private void OnInteractUsing(Entity<MobStateComponent> ent, ref InteractUsingEvent args)

    {

        if (args.Handled || !TryComp<SkillBookComponent>(args.Used, out var book))

            return;



        args.Handled = true;



        if (!_skills.IsSkillsEnabled())

            return;



        if (!IsValidStudent(ent.Owner))

            return;



        if (_skills.HasSkill(ent.Owner, book.Skill))

        {

            _popup.PopupEntity(Loc.GetString("skill-training-fail-already", ("skill", GetSkillLocName(book.Skill))), ent, args.User);

            return;

        }



        if (args.User == ent.Owner)

        {

            TryStartReading(args.User, args.Used, book.Skill);

            return;

        }



        if (!CanTeachStudentWithBook(args.User, ent.Owner, book.Skill, out var fail))

        {

            _popup.PopupEntity(fail, args.User, args.User);

            return;

        }



        TryStartReading(args.User, args.Used, book.Skill, ent.Owner);

    }



    private void OnBookUseInHand(Entity<SkillBookComponent> ent, ref UseInHandEvent args)

    {

        if (args.Handled)

            return;



        args.Handled = true;



        if (!_skills.IsSkillsEnabled())

            return;



        if (!IsValidStudent(args.User))

            return;



        if (_skills.HasSkill(args.User, ent.Comp.Skill))

        {

            _popup.PopupEntity(Loc.GetString("skill-training-fail-already", ("skill", GetSkillLocName(ent.Comp.Skill))), args.User, args.User);

            return;

        }



        TryStartReading(args.User, ent.Owner, ent.Comp.Skill);

    }



    private void OnBookReadDoAfter(Entity<SkillBookComponent> ent, ref SkillBookReadDoAfterEvent args)

    {

        if (args.Cancelled)

        {

            ClearActiveTraining(args.User);

            return;

        }



        if (args.Target != null && TryGetEntity(args.Target, out var target) && target != args.User)

        {

            TryStartBookTeaching(args.User, target.Value, ent.Owner, ent.Comp.Skill);

            return;

        }



        TrySelfStudy(args.User, ent.Comp.Skill);

    }



    private void TryStartReading(EntityUid user, EntityUid book, SkillTypes skill, EntityUid? target = null)

    {

        if (!TryComp<SkillBookComponent>(book, out var bookComp) || bookComp.Skill != skill)

            return;



        if (target != null)

        {

            if (!IsValidStudent(target.Value))

                return;



            if (!CanTeachStudentWithBook(user, target.Value, skill, out var fail))

            {

                _popup.PopupEntity(fail, user, user);

                return;

            }

        }

        else if (!IsValidStudent(user))

        {

            return;

        }



        var session = target ?? user;

        if (!CanStartTrainingSession(user, session, out var sessionFail))

        {

            _popup.PopupEntity(Loc.GetString(sessionFail), user, user);

            return;

        }



        var readEv = new SkillBookReadDoAfterEvent

        {

            Skill = skill,

            Target = target != null ? GetNetEntity(target.Value) : null,

        };



        var delay = GetReadDelay(user, skill, target);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay, readEv, book, target: target)

        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
        };



        if (!TryBeginTrainingDoAfter(user, session, doAfterArgs))

            return;



        _popup.PopupEntity(Loc.GetString("skill-training-reading"), user, user);

    }



    private void TryStartExpertTeach(EntityUid teacher, EntityUid student, SkillTypes skill)

    {

        if (!CanStartTrainingSession(teacher, student, out var sessionFail))

        {

            _popup.PopupEntity(Loc.GetString(sessionFail), teacher, teacher);

            return;

        }



        if (!CanTeachWithoutBook(teacher, skill, out var fail))

        {

            _popup.PopupEntity(fail, teacher, teacher);

            return;

        }



        var studentComp = EnsureStudent(student, skill, SkillTrainingMode.ExpertTeach);

        studentComp.Stage = 1;

        StartDoAfterTraining(teacher, student, null, studentComp);

    }



    private void TryStartBookTeaching(EntityUid teacher, EntityUid student, EntityUid book, SkillTypes skill)

    {

        if (!TryComp<SkillBookComponent>(book, out var bookComp) || bookComp.Skill != skill)

            return;



        if (!CanStartTrainingSession(teacher, student, out var sessionFail))

        {

            _popup.PopupEntity(Loc.GetString(sessionFail), teacher, teacher);

            ClearActiveTraining(teacher);

            return;

        }



        if (!TryResolveTrainingMode(teacher, skill, out var mode, out var fail))

        {

            _popup.PopupEntity(fail, teacher, teacher);

            ClearActiveTraining(teacher);

            return;

        }



        var studentComp = EnsureStudent(student, skill, mode);

        studentComp.Stage = 1;

        StartDoAfterTraining(teacher, student, book, studentComp);

    }



    private void TrySelfStudy(EntityUid user, SkillTypes skill)

    {

        var studentComp = EnsureStudent(user, skill, SkillTrainingMode.SelfStudy);



        if (studentComp.NextStageTime != TimeSpan.Zero && _timing.CurTime < studentComp.NextStageTime)

        {

            _popup.PopupEntity(Loc.GetString("skill-training-fail-wait"), user, user, PopupType.MediumCaution);

            return;

        }



        studentComp.Stage++;

        Dirty(user, studentComp);



        if (studentComp.Stage > SelfStudyStages)

        {

            CompleteTraining(user, skill);

            RemComp<SkillTrainingStudentComponent>(user);

            ClearActiveTraining(user);

            return;

        }



        var delay = _random.Next(SelfStudyMinDelay, SelfStudyMaxDelay);

        studentComp.NextStageTime = _timing.CurTime + TimeSpan.FromSeconds(delay);

        Dirty(user, studentComp);

        _popup.PopupEntity(

            Loc.GetString("skill-training-self-advance", ("skill", GetSkillLocName(skill)), ("stage", studentComp.Stage), ("total", SelfStudyStages)),

            user,

            user,

            PopupType.Medium);

    }



    private void OnTrainingDoAfter(Entity<SkillTrainingStudentComponent> ent, ref SkillTrainingDoAfterEvent args)

    {

        if (args.Cancelled)

        {

            ClearActiveTraining(args.User);

            return;

        }



        if (ent.Comp.Mode == SkillTrainingMode.SelfStudy)

            return;



        var student = ent.Owner;



        var requiredStages = GetRequiredStages(ent.Comp.Mode);

        if (ent.Comp.Stage < requiredStages)

        {

            ent.Comp.Stage++;

            Dirty(student, ent.Comp);

            var book = ent.Comp.Mode is SkillTrainingMode.ExpertWithBook or SkillTrainingMode.LibrarianWithBook
                && TryGetSkillBookInHand(args.User, ent.Comp.Skill, out var heldBook)
                ? heldBook
                : (EntityUid?)null;
            if (!StartDoAfterTraining(args.User, student, book, ent.Comp))

                return;

            _popup.PopupEntity(

                Loc.GetString("skill-training-progress", ("skill", GetSkillLocName(ent.Comp.Skill)), ("stage", ent.Comp.Stage), ("total", requiredStages)),

                student,

                args.User,

                PopupType.Medium);

            return;

        }



        if (student != args.User)

            ApplyTrainingCooldown(args.User);

        else

            ClearActiveTraining(args.User);

        CompleteTraining(student, ent.Comp.Skill);

        RemComp<SkillTrainingStudentComponent>(student);

    }



    private bool StartDoAfterTraining(EntityUid teacher, EntityUid student, EntityUid? book, SkillTrainingStudentComponent studentComp)

    {

        var delay = GetDoAfterDelay(studentComp.Mode);

        var args = new DoAfterArgs(EntityManager, teacher, delay, new SkillTrainingDoAfterEvent(), student, target: student)
        {
            NeedHand = book != null,
            BreakOnMove = true,
            BreakOnDamage = true,
        };



        if (TryBeginTrainingDoAfter(teacher, student, args))

            return true;

        RemComp<SkillTrainingStudentComponent>(student);

        ClearActiveTraining(teacher);

        return false;

    }

    private void ApplyTeachingDoAfterRules(DoAfterArgs args, bool continuingSession = false)
    {
        args.RequireCanInteract = false;
        args.DistanceThreshold = args.Target != null ? TrainingInteractionRange : null;
        args.BlockDuplicate = !continuingSession;
        args.CancelDuplicate = false;
        args.DuplicateCondition = DuplicateConditions.All;
    }

    private bool IsContinuingTrainingSession(SkillTrainingTeacherComponent comp, EntityUid sessionEntity)
    {
        return comp.ActiveStudent is { } active
               && TryGetEntity(active, out var activeEntity)
               && activeEntity == sessionEntity;
    }

    private bool CanStartTrainingSession(EntityUid teacher, EntityUid sessionEntity, out string failLocale)
    {
        failLocale = "skill-training-fail-busy";

        // Mobs without a DoAfterComponent (e.g. guardians) cannot run a training do-after; it would only log an invalid user error.
        if (!HasComp<DoAfterComponent>(teacher))
        {
            failLocale = "skill-training-fail-cannot-do-after";
            return false;
        }

        var comp = EnsureComp<SkillTrainingTeacherComponent>(teacher);

        if (_timing.CurTime < comp.CooldownUntil)
        {
            failLocale = "skill-training-fail-cooldown";
            return false;
        }

        var continuing = IsContinuingTrainingSession(comp, sessionEntity);

        if (!continuing && HasComp<ActiveDoAfterComponent>(teacher))
            return false;

        if (comp.ActiveStudent is { } active
            && TryGetEntity(active, out var activeEntity)
            && activeEntity != sessionEntity)
            return false;

        return true;
    }

    private bool TryBeginTrainingDoAfter(EntityUid teacher, EntityUid sessionEntity, DoAfterArgs args)
    {
        if (!CanStartTrainingSession(teacher, sessionEntity, out var failLocale))
        {
            _popup.PopupEntity(Loc.GetString(failLocale), teacher, teacher);
            return false;
        }

        var continuing = TryComp<SkillTrainingTeacherComponent>(teacher, out var teacherComp)
                           && IsContinuingTrainingSession(teacherComp, sessionEntity);

        ApplyTeachingDoAfterRules(args, continuing);

        if (args.Target != null
            && !_interaction.InRangeUnobstructed(teacher, args.Target.Value, TrainingInteractionRange))
        {
            _popup.PopupEntity(Loc.GetString("skill-training-fail-range"), teacher, teacher);
            return false;
        }

        if (args.NeedHand && !TryComp<HandsComponent>(teacher, out _))
        {
            _popup.PopupEntity(Loc.GetString("skill-training-fail-hands"), teacher, teacher);
            return false;
        }

        if (!_doAfter.TryStartDoAfter(args))
        {
            _popup.PopupEntity(Loc.GetString("skill-training-fail-busy"), teacher, teacher);
            return false;
        }

        var comp = EnsureComp<SkillTrainingTeacherComponent>(teacher);
        comp.ActiveStudent = GetNetEntity(sessionEntity);
        Dirty(teacher, comp);
        return true;
    }

    private void ClearActiveTraining(EntityUid teacher)
    {
        if (!TryComp<SkillTrainingTeacherComponent>(teacher, out var comp) || comp.ActiveStudent == null)
            return;

        comp.ActiveStudent = null;
        Dirty(teacher, comp);
    }

    private void ApplyTrainingCooldown(EntityUid teacher)
    {
        var comp = EnsureComp<SkillTrainingTeacherComponent>(teacher);
        comp.CooldownUntil = _timing.CurTime + TimeSpan.FromSeconds(TrainingCooldownSeconds);
        comp.ActiveStudent = null;
        Dirty(teacher, comp);
    }



    private SkillTrainingStudentComponent EnsureStudent(EntityUid student, SkillTypes skill, SkillTrainingMode mode)

    {

        var comp = EnsureComp<SkillTrainingStudentComponent>(student);

        if (comp.Skill != skill || comp.Mode != mode)

        {

            comp.Skill = skill;

            comp.Mode = mode;

            comp.Stage = 1;

            comp.NextStageTime = TimeSpan.Zero;

            Dirty(student, comp);

        }



        return comp;

    }



    private void CompleteTraining(EntityUid student, SkillTypes skill)

    {

        _skills.GrantSkill(student, skill);

        _popup.PopupEntity(Loc.GetString("skill-training-complete", ("skill", GetSkillLocName(skill))), student, student, PopupType.Large);

    }



    private bool CanTeachWithoutBook(EntityUid teacher, SkillTypes skill, out string fail)

    {

        fail = Loc.GetString("skill-training-fail-no-skill");



        if (IsLibrarian(teacher))

            return true;



        return _skills.HasSkill(teacher, skill);

    }



    private bool CanTeachStudentWithBook(EntityUid teacher, EntityUid student, SkillTypes skill, out string fail)

    {

        fail = Loc.GetString("skill-training-fail-cannot-teach");



        if (_skills.HasSkill(teacher, skill) || IsLibrarian(teacher))

            return true;



        return false;

    }



    private bool TryResolveTrainingMode(EntityUid teacher, SkillTypes skill, out SkillTrainingMode mode, out string fail)

    {

        mode = SkillTrainingMode.ExpertWithBook;

        fail = Loc.GetString("skill-training-fail-cannot-teach");



        if (_skills.HasSkill(teacher, skill))

        {

            mode = SkillTrainingMode.ExpertWithBook;

            return true;

        }



        if (IsLibrarian(teacher))

        {

            mode = SkillTrainingMode.LibrarianWithBook;

            return true;

        }



        return false;

    }



    private bool IsLibrarian(EntityUid user) => HasComp<LibrarianSkillTrainerComponent>(user);



    private bool TryGetSkillBookInHand(EntityUid user, SkillTypes skill, out EntityUid book)

    {

        book = default;



        if (!_hands.TryGetActiveItem(user, out var held) || !TryComp<SkillBookComponent>(held, out var bookComp))

            return false;



        if (bookComp.Skill != skill)

            return false;



        book = held.Value;

        return true;

    }



    private bool IsValidStudent(EntityUid uid)
    {
        return HasComp<MobStateComponent>(uid) && _mobState.IsAlive(uid);
    }



    private IEnumerable<SkillTypes> GetAvailableSkills(EntityUid teacher, EntityUid student)

    {

        IEnumerable<SkillTypes> source;



        if (IsLibrarian(teacher))

        {

            source = Enum.GetValues<SkillTypes>().Where(s => s != SkillTypes.All);

        }

        else if (_skills.TryGetSkills(teacher, out var teacherSkills))
        {
            source = GetTeachableSkills(teacherSkills);
        }
        else
        {
            yield break;
        }



        foreach (var skill in source)

        {

            if (!_skills.HasSkill(student, skill))

                yield return skill;

        }

    }



    private static IEnumerable<SkillTypes> GetTeachableSkills(HashSet<SkillTypes> teacherSkills)

    {

        if (teacherSkills.Contains(SkillTypes.All))

        {

            return Enum.GetValues<SkillTypes>().Where(s => s != SkillTypes.All);

        }



        return teacherSkills.Where(s => s != SkillTypes.All);

    }



    private static int GetRequiredStages(SkillTrainingMode mode) => mode switch

    {

        SkillTrainingMode.ExpertWithBook or SkillTrainingMode.LibrarianWithBook => ExpertWithBookStages,

        SkillTrainingMode.SelfStudy => SelfStudyStages,

        _ => FastStages,

    };



    private static float GetDoAfterDelay(SkillTrainingMode mode) => mode switch

    {

        SkillTrainingMode.ExpertWithBook or SkillTrainingMode.LibrarianWithBook => ExpertWithBookDelay,

        _ => FastDoAfterDelay,

    };



    private float GetReadDelay(EntityUid user, SkillTypes skill, EntityUid? target)

    {

        if (target == null)

            return SelfReadDelay;



        if (_skills.HasSkill(user, skill))

            return ExpertReadDelay;



        if (IsLibrarian(user))

            return ExpertReadDelay;



        return SelfReadDelay;

    }



    private string GetSkillLocName(SkillTypes skill) => Loc.GetString($"skill-training-name-{skill}");

}


