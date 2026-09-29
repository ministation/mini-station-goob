using Content.Shared.Chat;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;

namespace Content.Shared.Implants;

public abstract partial class SharedSubdermalImplantSystem
{
    public void InitializeRelay()
    {
        SubscribeLocalEvent<ImplantedComponent, MobStateChangedEvent>(RelayToImplantEvent);
        SubscribeLocalEvent<ImplantedComponent, AfterInteractUsingEvent>(RelayToImplantEvent);
        SubscribeLocalEvent<ImplantedComponent, SuicideEvent>(RelayToImplantEvent);
        SubscribeLocalEvent<ImplantedComponent, TransformSpeakerNameEvent>(RelayToImplantEvent);
        SubscribeLocalEvent<ImplantedComponent, TransformSpeechEvent>(RelayToImplantEvent);
        SubscribeLocalEvent<ImplantedComponent, SeeIdentityAttemptEvent>(RelayToImplantEvent);
    }

    /// <summary>
    /// Relays events from the implanted to their implants.
    /// </summary>
    private void RelayToImplantEvent<T>(EntityUid uid, ImplantedComponent component, T args) where T : notnull
    {
        if (!_container.TryGetContainer(uid, ImplanterComponent.ImplantSlotId, out var implantContainer))
            return;

        var relayEv = new ImplantRelayEvent<T>(args, uid);

        // Copy first: an implant may answer by dropping implants. A microbomb triggers on death and
        // gibs its host, taking the whole container with it, which broke the loop mid-enumeration.
        var implants = new List<EntityUid>(implantContainer.ContainedEntities);
        foreach (var implant in implants)
        {
            if (args is HandledEntityEventArgs { Handled: true })
                return;

            if (TerminatingOrDeleted(implant))
                continue;

            RaiseLocalEvent(implant, relayEv);
        }
    }
}

/// <summary>
/// Wrapper for relaying events from an implanted entity to their implants.
/// </summary>
public sealed class ImplantRelayEvent<T> where T : notnull
{
    public readonly T Event;

    public readonly EntityUid ImplantedEntity;

    public ImplantRelayEvent(T ev, EntityUid implantedEntity)
    {
        Event = ev;
        ImplantedEntity = implantedEntity;
    }
}
