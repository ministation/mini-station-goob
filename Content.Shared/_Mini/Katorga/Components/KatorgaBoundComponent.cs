using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Katorga.Components;

/// <summary>
///     Компонент заключённого каторги. Сервер удерживает игрока на карте каторги,
///     клиент по этим полям рисует худ с прогрессом отработки срока.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KatorgaBoundComponent : Component
{
    /// <summary>
    ///     Какой срок оставался заключённому на момент прибытия на каторгу.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public TimeSpan SentenceTotal = TimeSpan.Zero;

    /// <summary>
    ///     Сколько срока оставалось на момент последнего серверного обновления.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public TimeSpan SentenceRemaining = TimeSpan.Zero;

    /// <summary>
    ///     Сколько срока всего сокращено добычей руды.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public TimeSpan WorkedOff = TimeSpan.Zero;
}
