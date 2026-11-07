using Content.Shared._CorvaxGoob.Skills;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxGoob.Skills;

[RegisterComponent]
public sealed partial class SkillBookComponent : Component
{
    [DataField(required: true)]
    public Skills Skill;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkillTrainingStudentComponent : Component
{
    [DataField, AutoNetworkedField]
    public Skills Skill;

    [DataField, AutoNetworkedField]
    public SkillTrainingMode Mode;

    [DataField, AutoNetworkedField]
    public int Stage = 1;

    [DataField, AutoNetworkedField]
    public TimeSpan NextStageTime = TimeSpan.Zero;
}

[Serializable, NetSerializable]
public enum SkillTrainingMode : byte
{
    SelfStudy,
    ExpertTeach,
    ExpertWithBook,
    LibrarianWithBook,
}

[Serializable, NetSerializable]
public sealed partial class SkillTrainingDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class SkillBookReadDoAfterEvent : SimpleDoAfterEvent
{
    public Skills Skill;
    public NetEntity? Target;
}
