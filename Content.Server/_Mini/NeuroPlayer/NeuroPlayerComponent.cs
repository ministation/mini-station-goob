// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Content.Shared._Mini.NeuroPlayer;

namespace Content.Server._Mini.NeuroPlayer;

[RegisterComponent]
public sealed partial class NeuroPlayerComponent : Component
{
    [DataField]
    public ProtoId<NeuroPersonaPrototype> PersonaId = default!;

    /// <summary>Rolling window of recently heard speech, sent to the LLM as context.</summary>
    [ViewVariables]
    public readonly Queue<(string Speaker, string Message)> Context = new();

    /// <summary>Bots must not fire a new LLM request while one is already in flight.</summary>
    [ViewVariables]
    public bool RequestInFlight;

    [ViewVariables]
    public TimeSpan NextAllowedResponse = TimeSpan.Zero;
}
