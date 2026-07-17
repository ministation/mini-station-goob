// SPDX-License-Identifier: MIT

using Content.Shared.Speech;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.IntrinsicVoiceModulator;

[Serializable, NetSerializable]
public enum IntrinsicVoiceModulatorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class IntrinsicVoiceModulatorBoundUserInterfaceState(
    string currentName,
    ProtoId<SpeechVerbPrototype>? currentVerb,
    ProtoId<JobIconPrototype>? jobIcon)
    : BoundUserInterfaceState
{
    public string CurrentName { get; private set; } = currentName;
    public ProtoId<SpeechVerbPrototype>? CurrentVerb { get; private set; } = currentVerb;
    public ProtoId<JobIconPrototype>? JobIcon { get; private set; } = jobIcon;
}

[NetSerializable, Serializable]
public sealed class IntrinsicVoiceModulatorNameChangedMessage(string name) : BoundUserInterfaceMessage
{
    public string Name { get; private set; } = name;
}

[NetSerializable, Serializable]
public sealed class IntrinsicVoiceModulatorJobIconChangedMessage(ProtoId<JobIconPrototype> jobIconProtoId)
    : BoundUserInterfaceMessage
{
    public ProtoId<JobIconPrototype> JobIconProtoId { get; private set; } = jobIconProtoId;
}

[NetSerializable, Serializable]
public sealed class IntrinsicVoicemodulatorVerbChangedMessage(ProtoId<SpeechVerbPrototype>? speechProtoId)
    : BoundUserInterfaceMessage
{
    public ProtoId<SpeechVerbPrototype>? SpeechProtoId { get; private set; } = speechProtoId;
}
