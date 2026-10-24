using Content.Server._Mini.Katorga;

namespace Content.Server._Mini.Katorga.Components;

/// <summary>
///     Маркер сущности заключённого каторги. Такой игрок телепортируется обратно,
///     если покинет карту каторги, а убийство его сокамерником продлевает срок убийце.
/// </summary>
[RegisterComponent]
[Access(typeof(KatorgaSystem))]
public sealed partial class KatorgaBoundComponent : Component
{
}
