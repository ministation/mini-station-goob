using Content.Shared.Actions;
using Content.Shared.Silicons.Borgs;
using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.Serialization;

namespace Content.Shared.Borgs
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class MimicryComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("MimicryAction")]
        public EntProtoId MimicryAction = "Mimicry";

        [ViewVariables]
        public EntityUid? MimicryActionEntity;

        [DataField("realState")]
        public string RealState = "synd_engi";
        [DataField("engState")]
        public string EngState = "engineer";
        [DataField("deadState")]
        public string DeadState = "synd_engi";
        [DataField]
        public string RealState1 = "synd_engi_e";
        [DataField]
        public string RealState2 = "synd_engi_l";
        [DataField]
        public string EngState1 = "engineer_e_r";
        [DataField]
        public string EngState2 = "engineer_l";

        [DataField]
        public string EngSpritePath = "Mobs/Silicon/chassis.rsi";

        [DataField]
        public Dictionary<ProtoId<BorgTypePrototype>, ProtoId<BorgTypePrototype>> DisguiseTypeMap = new();


        [ViewVariables(VVAccess.ReadWrite)]
        public bool Mimicked = false;
    }

    [NetSerializable, Serializable]
    public enum MiniBorgVisuals : byte
    {
        Real,
        Eng,
        Dead,
    }
    [NetSerializable, Serializable]
    public enum BorgVisualsLight : byte
    {
        Alive,
        Dead,
    }
    [Serializable, NetSerializable]
    public enum SabotafeBorgVisualLayers : byte
    {
        Light
    }
    public sealed partial class MimicryEvent : InstantActionEvent
    {
    }
}
