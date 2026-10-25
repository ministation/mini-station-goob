// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._CorvaxGoob.TTS;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Ghost tier 4: the chameleon. Scans a crew member and copies their appearance, name and voice,
/// draining the suit cell while the disguise is active. The component lives on the suit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaGhostDisguiseComponent : Component
{
    /// <summary>
    /// Suit charge spent by a single scan.
    /// </summary>
    [DataField]
    public float EnergyCost = 50f;

    /// <summary>
    /// Suit charge drained per second while the disguise is active.
    /// </summary>
    [DataField("drainRate")]
    public float DrainRate = 5f;

    /// <summary>
    /// Duration of the scan do-after.
    /// </summary>
    [DataField]
    public TimeSpan ScanDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Duration of the apply do-after.
    /// </summary>
    [DataField]
    public TimeSpan ApplyDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum distance to the scanner target.
    /// </summary>
    [DataField]
    public float ScanRange = 2.5f;

    /// <summary>
    /// How many scanned appearances are kept. The oldest one is dropped when the limit is reached.
    /// </summary>
    [DataField]
    public int MaxEntries = 3;

    /// <summary>
    /// Whether a disguise is currently active. Networked so other systems and the client can check it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Disguised;

    /// <summary>
    /// Index of the active appearance in <see cref="Entries"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ActiveIndex = -1;

    /// <summary>
    /// Scanned appearances. Server only: the client does not render the disguise database.
    /// </summary>
    [DataField]
    public List<NinjaGhostDisguiseEntry> Entries = new();

    /// <summary>
    /// Snapshot of the own appearance of the ninja, used to restore the look when the disguise is dropped.
    /// </summary>
    [ViewVariables]
    public NinjaGhostDisguiseEntry? Original;

    /// <summary>
    /// Items whose clothing visuals were replaced by the disguise, restored when it is dropped.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> DisguisedItems = new();

    [ViewVariables]
    public DoAfterId? ActiveDoAfter;

    [ViewVariables]
    public int PendingIndex = -1;

    [ViewVariables]
    public EntityUid? PendingScanTarget;
}

/// <summary>
/// One saved appearance of the chameleon module.
/// </summary>
[DataDefinition]
public sealed partial class NinjaGhostDisguiseEntry
{
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public NinjaGhostDisguiseAppearance? Appearance;

    [DataField]
    public bool HasIdCard;

    [DataField]
    public string? IdCardName;

    [DataField]
    public string? IdCardJobTitle;

    [DataField]
    public ProtoId<JobPrototype>? IdCardJobPrototype;

    /// <summary>
    /// Clothing prototype worn by the scanned target per inventory slot, used to mimic the outfit.
    /// </summary>
    [DataField]
    public Dictionary<string, EntProtoId> Clothing = new();
}

/// <summary>
/// Appearance values copied by the chameleon module.
/// </summary>
[DataDefinition]
public sealed partial class NinjaGhostDisguiseAppearance
{
    [DataField]
    public MarkingSet MarkingSet = new();

    [DataField]
    public ProtoId<SpeciesPrototype> Species = default;

    [DataField]
    public Color SkinColor = Color.FromHex("#C0967F");

    [DataField]
    public Color EyeColor = Color.Brown;

    [DataField]
    public Sex Sex = Sex.Male;

    [DataField]
    public Gender Gender;

    [DataField]
    public int Age = 18;

    [DataField]
    public ProtoId<TTSVoicePrototype> Voice = default;

    [DataField]
    public HashSet<HumanoidVisualLayers> PermanentlyHidden = new();

    [DataField]
    public Dictionary<HumanoidVisualLayers, SlotFlags> HiddenLayers = new();

    [DataField]
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CustomBaseLayers = new();

    [DataField]
    public float Height = 1f;

    [DataField]
    public float Width = 1f;

    [DataField]
    public ProtoId<MarkingPrototype>? UndergarmentTop;

    [DataField]
    public ProtoId<MarkingPrototype>? UndergarmentBottom;
}

/// <summary>
/// Raised when the appearance scanner action is used on a target.
/// </summary>
public sealed partial class NinjaGhostDisguiseScanEvent : EntityTargetActionEvent;

/// <summary>
/// Raised when the chameleon menu action is used: applies the last scanned appearance or drops the current one.
/// </summary>
public sealed partial class NinjaGhostDisguiseMenuEvent : InstantActionEvent;

/// <summary>
/// Raised when the scan do-after finishes.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class NinjaGhostDisguiseScanDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}

/// <summary>
/// Raised when the apply do-after finishes.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class NinjaGhostDisguiseApplyDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}
