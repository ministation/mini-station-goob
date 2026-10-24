namespace Content.Server._Mini.Katorga.Components;

/// <summary>
///     Маркер приёмника руды на каторге. Руда, загруженная в такой приёмник,
///     сокращает срок заключённого, который её загрузил.
/// </summary>
[RegisterComponent]
[Access(typeof(KatorgaSystem))]
public sealed partial class KatorgaOreProcessorComponent : Component
{
}
