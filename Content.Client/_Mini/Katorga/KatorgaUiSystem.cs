using Content.Shared._Mini.Katorga.Components;
using Robust.Client.Player;
using Robust.Shared.GameObjects;

namespace Content.Client._Mini.Katorga;

/// <summary>
///     Клиентская система: следит за состоянием каторжанина у локального игрока.
/// </summary>
public sealed class KatorgaUiSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

    /// <summary>Данные компонента локального игрока изменились (стейт, старт, удаление).</summary>
    public event Action? InmateStateChanged;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KatorgaBoundComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<KatorgaBoundComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<KatorgaBoundComponent, ComponentRemove>(OnRemove);
    }

    private void OnStartup(EntityUid uid, KatorgaBoundComponent component, ComponentStartup args)
    {
        Notify(uid);
    }

    private void OnHandleState(EntityUid uid, KatorgaBoundComponent component, ref ComponentHandleState args)
    {
        Notify(uid);
    }

    private void OnRemove(EntityUid uid, KatorgaBoundComponent component, ComponentRemove args)
    {
        Notify(uid);
    }

    private void Notify(EntityUid uid)
    {
        if (_player.LocalEntity == uid)
            InmateStateChanged?.Invoke();
    }

    /// <summary>Есть ли у локального игрока компонент заключённого.</summary>
    public bool TryGetInmate(out KatorgaBoundComponent? inmate)
    {
        inmate = null;
        return _player.LocalEntity is { } local && TryComp(local, out inmate);
    }
}
