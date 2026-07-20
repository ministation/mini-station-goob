using System.Threading;
using Content.Server.Administration.Logs;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Server.Speech.EntitySystems;
using Content.Shared._Mini.DNALocker;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.Forensics.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Speech.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Mini.DNALocker;

public sealed class DNALockerSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly VocalSystem _vocal = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DNALockerComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<DNALockerComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<DNALockerComponent, StoreDNAActionEvent>(OnDNAStore);
        SubscribeLocalEvent<DNALockerComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<DNALockerComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerb);
        SubscribeLocalEvent<DNALockerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, DNALockerComponent comp, ComponentShutdown args)
    {
        CancelExplosion(comp);
    }

    public void OnEquip(EntityUid uid, DNALockerComponent comp, GotEquippedEvent args)
    {
        if (comp.Activated || !comp.DNAWasStored || !comp.IsLocked)
            return;

        // Defer one tick so equip/polymorph/inventory transfer can finish applying DnaComponent.
        var equipment = args.Equipment;
        var equipee = args.Equipee;
        Timer.Spawn(0, () => TryAuthorizeOrRetaliate(equipment, equipee));
    }

    private void TryAuthorizeOrRetaliate(EntityUid equipment, EntityUid equipee)
    {
        if (!EntityManager.EntityExists(equipment) || !EntityManager.EntityExists(equipee))
            return;

        if (!TryComp(equipment, out DNALockerComponent? comp))
            return;

        if (comp.Activated || !comp.DNAWasStored || !comp.IsLocked)
            return;

        // Still worn?
        if (!_inventory.TryGetSlotEntity(equipee, "outerClothing", out var worn) || worn != equipment)
            return;

        if (IsAuthorized(equipee, comp))
            return;

        // No usable DNA on wearer: never gib — unequip if possible.
        if (!TryComp(equipee, out DnaComponent? dna) || string.IsNullOrEmpty(dna.DNA))
        {
            RejectNonlethal(equipment, equipee);
            return;
        }

        if (comp.Nonlethal)
        {
            RejectNonlethal(equipment, equipee);
            return;
        }

        StartSelfDestruct(equipment, equipee, comp);
    }

    private bool IsAuthorized(EntityUid equipee, DNALockerComponent comp)
    {
        if (!TryComp(equipee, out DnaComponent? dna) || string.IsNullOrEmpty(dna.DNA))
            return false;

        return string.Equals(comp.DNA, dna.DNA, StringComparison.Ordinal);
    }

    private void RejectNonlethal(EntityUid equipment, EntityUid equipee)
    {
        _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-error"), equipee, equipee);
        _inventory.TryUnequip(equipee, "outerClothing", true, true);
    }

    private void StartSelfDestruct(EntityUid equipment, EntityUid equipee, DNALockerComponent comp)
    {
        CancelExplosion(comp);
        comp.Activated = true;
        comp.ExplosionToken = new CancellationTokenSource();
        var token = comp.ExplosionToken.Token;

        _adminLogger.Add(LogType.Trigger, LogImpact.Medium,
            $"{ToPrettyString(equipee):user} activated hardsuit self destruction of {ToPrettyString(equipment):target} (stored DNA mismatch)");

        EnsureComp<UnremoveableComponent>(equipment);
        _audio.PlayPvs(comp.LockerExplodeSound, equipment);
        _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-error-spikes"), equipee, equipee, Shared.Popups.PopupType.Large);

        Timer.Spawn(1000, () =>
        {
            if (token.IsCancellationRequested || !EntityManager.EntityExists(equipment))
                return;
            _chat.TrySendInGameICMessage(equipment, Loc.GetString("hardsuit-identification-error"), InGameICChatType.Speak, true);
        }, token);

        Timer.Spawn(1500, () => TryScream(equipee, token), token);
        Timer.Spawn(2000, () => SpeakCountdown(equipment, "3", token), token);
        Timer.Spawn(2500, () => TryScream(equipee, token), token);
        Timer.Spawn(3000, () => SpeakCountdown(equipment, "2", token), token);
        Timer.Spawn(3500, () => TryScream(equipee, token), token);
        Timer.Spawn(4000, () =>
        {
            SpeakCountdown(equipment, "1", token);
            TryScream(equipee, token);
        }, token);

        Timer.Spawn(5000, () =>
        {
            if (token.IsCancellationRequested || !EntityManager.EntityExists(equipment))
                return;

            _explosionSystem.QueueExplosion(equipment, ExplosionSystem.DefaultExplosionPrototypeId,
                4, 1, 2, maxTileBreak: 0);

            if (EntityManager.EntityExists(equipee)
                && _inventory.TryGetSlotEntity(equipee, "outerClothing", out var hardsuitEntity)
                && hardsuitEntity == equipment
                && TryComp<BodyComponent>(equipee, out var body))
            {
                var ents = _bodySystem.GibBody(equipee, true, body, false);
                foreach (var part in ents)
                {
                    if (HasComp<BodyPartComponent>(part))
                        QueueDel(part);
                }
            }

            EntityManager.DeleteEntity(equipment);
        }, token);
    }

    private void SpeakCountdown(EntityUid equipment, string text, CancellationToken token)
    {
        if (token.IsCancellationRequested || !EntityManager.EntityExists(equipment))
            return;
        _chat.TrySendInGameICMessage(equipment, text, InGameICChatType.Speak, true);
    }

    private void TryScream(EntityUid equipee, CancellationToken token)
    {
        if (token.IsCancellationRequested || !EntityManager.EntityExists(equipee))
            return;
        if (TryComp(equipee, out VocalComponent? vocal))
            _vocal.TryPlayScreamSound(equipee, vocal);
    }

    private static void CancelExplosion(DNALockerComponent comp)
    {
        if (comp.ExplosionToken != null)
        {
            comp.ExplosionToken.Cancel();
            comp.ExplosionToken.Dispose();
            comp.ExplosionToken = null;
        }

        comp.Activated = false;
    }

    private void OnGetActions(EntityUid uid, DNALockerComponent comp, GetItemActionsEvent args)
    {
        if (!comp.DNAWasStored)
            args.AddAction(ref comp.ActionEntity, comp.Action);
    }

    public void OnDNAStore(EntityUid uid, DNALockerComponent comp, StoreDNAActionEvent args)
    {
        if (args.Handled)
            return;

        if (comp.DNAWasStored)
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-dna-already-stored"), args.Performer, args.Performer);
            args.Handled = true;
            return;
        }

        if (!TryComp(args.Performer, out DnaComponent? dna) || string.IsNullOrEmpty(dna.DNA))
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-dna-not-presented"), args.Performer, args.Performer);
            args.Handled = true;
            return;
        }

        comp.DNA = dna.DNA;
        comp.DNAWasStored = true;
        CancelExplosion(comp);
        RemComp<UnremoveableComponent>(uid);

        if (comp.ActionEntity is { } action)
            _actions.RemoveProvidedAction(args.Performer, uid, action);

        _audio.PlayPvs(comp.LockSound, uid);
        _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-dna-was-stored"), args.Performer, args.Performer);
        args.Handled = true;
    }

    public void OnEmagged(EntityUid uid, DNALockerComponent comp, GotEmaggedEvent args)
    {
        _audio.PlayPvs(comp.SparkSound, uid);

        if (comp.Activated)
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-on-emagged-late"), uid);
        else
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-on-emagged"), uid);

        CancelExplosion(comp);
        RemComp<UnremoveableComponent>(uid);
        EntityManager.RemoveComponent<DNALockerComponent>(uid);

        args.Handled = true;
    }

    private void OnAltVerb(EntityUid uid, DNALockerComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!component.IsLocked)
            return;

        AlternativeVerb verbDNALock = new()
        {
            Act = () => MakeUnlocked(uid, component, args.User),
            Text = Loc.GetString("dna-locker-verb-name"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/fold.svg.192dpi.png")),
        };
        args.Verbs.Add(verbDNALock);
    }

    private void MakeUnlocked(EntityUid uid, DNALockerComponent component, EntityUid userUid)
    {
        if (IsAuthorized(userUid, component))
        {
            CancelExplosion(component);
            RemComp<UnremoveableComponent>(uid);
            _audio.PlayPvs(component.LockSound, userUid);
            _popupSystem.PopupEntity(Loc.GetString("dna-locker-unlock"), uid, userUid);
            component.DNA = string.Empty;
            component.DNAWasStored = false;
        }
        else
        {
            _audio.PlayPvs(component.DeniedSound, userUid);
            _popupSystem.PopupEntity(Loc.GetString("dna-locker-failure"), uid, userUid);
        }
    }
}
