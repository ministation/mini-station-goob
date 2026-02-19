// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Research.TechnologyDisk.Components;

[RegisterComponent]
public sealed partial class DiskConsoleComponent : Component
{
    /// <summary>
    /// How much it costs to print a disk
    /// </summary>
    [DataField("pricePerDisk"), ViewVariables(VVAccess.ReadWrite)]
    public int PricePerDisk = 1000;

    /// <summary>
    /// The prototype of what's being printed
    /// </summary>
    [DataField("diskPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string DiskPrototype = "TechnologyDisk";

    /// <summary>
    /// How long it takes to print <see cref="DiskPrototype"/>
    /// </summary>
    [DataField("printDuration"), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan PrintDuration = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The sound made when printing occurs
    /// </summary>
    [DataField("printSound")]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    /// <summary>
    /// Whether the console should continuously print disks while enough points are available.
    /// </summary>
    [DataField("autoPrint"), ViewVariables(VVAccess.ReadWrite)]
    public bool AutoPrint = false;

    /// <summary>
    /// Whether printed technology disks should be auto-fed into a nearby converter instead of spawning as items.
    /// </summary>
    [DataField("autoFeedAdjacentConverter"), ViewVariables(VVAccess.ReadWrite)]
    public bool AutoFeedAdjacentConverter = false;

    /// <summary>
    /// Maximum local distance to search for a converter when <see cref="AutoFeedAdjacentConverter"/> is enabled.
    /// </summary>
    [DataField("adjacentConverterRange"), ViewVariables(VVAccess.ReadWrite)]
    public float AdjacentConverterRange = 1.5f;
}
