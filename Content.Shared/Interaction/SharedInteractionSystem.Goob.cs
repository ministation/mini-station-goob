using Content.Goobstation.Common.CCVar;
using Content.Shared._ST.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared.Interaction;

public abstract partial class SharedInteractionSystem
{
    [Dependency] private INetManager _net = default!;

    [Dependency] private IConfigurationManager _cfg = default!;

    private bool _interactionParticlesSuppressed;
    private bool _interactionParticlesEnabled = true;

    /// <summary>
    /// Mini: how close another player has to be to see interaction particles. The item icon that pops
    /// above the head of the performer is only visible to the performer and to players within arm's reach,
    /// instead of everyone whose PVS includes the performer.
    /// </summary>
    private const float InteractionParticleVisibilityRange = InteractionRange;

    private void InitializeInteractionParticles()
    {
        Subs.CVar(_cfg, GoobCVars.InteractionParticlesEnabled, v => _interactionParticlesEnabled = v, true);
    }

    public void SetInteractionParticlesSuppressed(bool suppressed)
    {
        _interactionParticlesSuppressed = suppressed;
    }

    public void DoContactInteraction(EntityUid uidA,
        EntityUid? uidB,
        EntityUid? used,
        bool predicted,
        HandledEntityEventArgs? args = null,
        bool interactionParticles = true,
        StellarInteractionParticleType interactionParticleType = StellarInteractionParticleType.Use)
    {
        DoContactInteraction(uidA, uidB, args);

        if (!interactionParticles || !_interactionParticlesEnabled || _interactionParticlesSuppressed || uidB is not { } target || args?.Handled == false || uidA == target)
            return;

        if (!TryComp(uidA, out MetaDataComponent? metaA) || metaA.EntityPaused
            || !TryComp(target, out MetaDataComponent? metaB) || metaB.EntityPaused
            || HasComp<VirtualItemComponent>(target))
            return;

        if (metaA.CreationTick == _gameTiming.CurTick || metaB.CreationTick == _gameTiming.CurTick)
            return;

        if (_net.IsServer)
        {
            // Mini: don't broadcast interaction particles to everyone in PVS, the item icon above the head
            // is only shown to the performer and to players standing very close to them.
            var filter = Filter.Empty().AddInRange(_transform.GetMapCoordinates(uidA), InteractionParticleVisibilityRange, entMan: EntityManager);
            if (TryComp(uidA, out ActorComponent? actor))
                filter.AddPlayer(actor.PlayerSession);

            RaiseNetworkEvent(new StellarInteractionParticleEvent(GetNetEntity(uidA), GetNetEntity(used), GetNetEntity(target), false, interactionParticleType), filter);
        }
        else if (predicted && _gameTiming.IsFirstTimePredicted)
        {
            RaiseLocalEvent(new StellarInteractionParticleEvent(GetNetEntity(uidA), GetNetEntity(used), GetNetEntity(target), true, interactionParticleType));
        }
    }
}
