// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
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

    /// <summary>Separate throttle for unprompted (no name) replies in local chat.</summary>
    [ViewVariables]
    public TimeSpan NextProactiveResponse = TimeSpan.Zero;

    /// <summary>Throttle for small talk with another bot (neuroplayer.bot_chatter_cooldown_seconds).</summary>
    [ViewVariables]
    public TimeSpan NextBotChatter = TimeSpan.Zero;

    /// <summary>
    /// When this bot's last spoken line was itself a reply to another bot. Nobody answers such a
    /// line, which keeps two bots from ping-ponging LLM requests.
    /// </summary>
    [ViewVariables]
    public TimeSpan? LastChatterReply;

    /// <summary>Evac shuttle this bot was sent to; it walks back aboard after being scared off.</summary>
    [ViewVariables]
    public EntityUid? EvacShuttle;

    /// <summary>Per-bot offset on the evac shuttle, so the group does not stack on one tile.</summary>
    [ViewVariables]
    public Vector2 EvacOffset;

    /// <summary>Admin mute (neurobots mute): the bot hears but never speaks or spends requests.</summary>
    [ViewVariables]
    public bool Muted;

    /// <summary>Timed mute; ignored when <see cref="Muted"/> is set.</summary>
    [ViewVariables]
    public TimeSpan MutedUntil;

    /// <summary>Cry (canned phrase) cooldown.</summary>
    [ViewVariables]
    public TimeSpan NextCry = TimeSpan.Zero;

    /// <summary>Separate, shorter cooldown for "отстаньте" when somebody grabs, cuffs or seats the bot.</summary>
    [ViewVariables]
    public TimeSpan NextResistCry = TimeSpan.Zero;

    /// <summary>Fleeing until this time (NeuroFleeCompound is the active root task).</summary>
    [ViewVariables]
    public TimeSpan? DangerUntil;

    /// <summary>Fighting hostiles until this time (SimpleHumanoidHostileCompound is active).</summary>
    [ViewVariables]
    public TimeSpan? HostileUntil;

    /// <summary>Visiting a POI until this time (NeuroVisitCompound is the active root task).</summary>
    [ViewVariables]
    public TimeSpan? VisitUntil;

    [ViewVariables]
    public float PoiAccumulator;
}
