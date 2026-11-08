// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Humanoid;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.NeuroPlayer;

/// <summary>
/// A neuro player persona: passenger bot with an LLM-driven personality.
/// </summary>
[Prototype("neuroPersona")]
public sealed partial class NeuroPersonaPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Bot display name (also the trigger word players use to address it).</summary>
    [DataField(required: true)]
    public string Name = default!;

    /// <summary>Character description, injected into the LLM system prompt.</summary>
    [DataField(required: true)]
    public string Character = default!;

    [DataField]
    public ProtoId<JobPrototype> Job = "Passenger";

    /// <summary>
    /// Sex of the spawned body, which also picks the TTS voice. Without it the persona and the body
    /// drifted apart: "Марта Штейн, дама средних лет" could spawn as a young man with a male voice.
    /// </summary>
    [DataField]
    public Sex? Sex;

    /// <summary>Age of the spawned body; null leaves it random.</summary>
    [DataField]
    public int? Age;

    [DataField]
    public int Order;
}
