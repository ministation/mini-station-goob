namespace Content.Server._Mini.Katorga.Components;

/// <summary>
///     Точка спавна заключённых на карте каторги. В отличие от SpawnPointComponent
///     не участвует в обычном спавне игроков GameTicker.
/// </summary>
[RegisterComponent]
[Access(typeof(KatorgaSystem))]
public sealed partial class KatorgaSpawnPointComponent : Component
{
}
