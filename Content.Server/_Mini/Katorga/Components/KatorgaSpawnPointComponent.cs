using Content.Shared._Mini.Katorga;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Katorga.Components;

/// <summary>
///     Точка спавна заключённых на карте каторги. В отличие от SpawnPointComponent
///     не участвует в обычном спавне игроков GameTicker. Точки с Faction спавнят
///     только заключённых этой фракции, без Faction — любые (общие).
/// </summary>
[RegisterComponent]
[Access(typeof(KatorgaSystem), typeof(KatorgaFactionSystem))]
public sealed partial class KatorgaSpawnPointComponent : Component
{
    [DataField]
    public ProtoId<KatorgaFactionPrototype>? Faction;
}
