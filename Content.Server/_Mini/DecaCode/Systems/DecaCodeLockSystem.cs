// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server.Construction;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Mini.DecaCode;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Content.Shared.Prying.Systems;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mini.DecaCode.Systems;

/// <summary>
/// Server side of the Deca-Code lock. Generates the secret code, owns the lock UI,
/// validates attempts and detonates the anti-tamper bomb when the crate is brute forced.
/// Every custom handler bails out while the lock is open, so the crate returns to vanilla behaviour.
/// The secret code is never included in any state sent to a client.
/// </summary>
public sealed class DecaCodeLockSystem : EntitySystem
{
    /// <summary>
    /// Anti-tamper explosion. These values mirror the Composition C-4 prototype.
    /// </summary>
    private const string BombExplosionType = "DemolitionCharge";
    private const float BombTotalIntensity = 60f;
    private const float BombIntensitySlope = 5f;
    private const float BombMaxIntensity = 30f;

    private static readonly ProtoId<TagPrototype> MultitoolTag = "Multitool";

    /// <summary>
    /// Crates whose charge has already gone off. Prevents a second handler, or the deletion that
    /// follows the blast itself, from detonating the same crate twice.
    /// </summary>
    private readonly HashSet<EntityUid> _detonated = new();

    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DecaCodeLockComponent, MapInitEvent>(OnMapInit);

        // Run before the native lock and storage handlers so a locked crate cannot be opened by a click.
        SubscribeLocalEvent<DecaCodeLockComponent, ActivateInWorldEvent>(
            OnActivate,
            before: new[] { typeof(LockSystem), typeof(SharedEntityStorageSystem), typeof(SharedStorageSystem) });

        // Run before every deconstruction path (construction, prying, welding) and before item insertion,
        // so a locked crate is never taken apart and tools are consumed by the bomb instead.
        SubscribeLocalEvent<DecaCodeLockComponent, InteractUsingEvent>(
            OnInteractUsing,
            before: new[]
            {
                typeof(ConstructionSystem),
                typeof(PryingSystem),
                typeof(WeldableSystem),
                typeof(SharedEntityStorageSystem),
                typeof(SharedStorageSystem),
            });

        SubscribeLocalEvent<DecaCodeLockComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<DecaCodeLockComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<DecaCodeLockComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<DecaCodeLockComponent, DecaCodeSubmitMessage>(OnSubmit);
    }

    private void OnMapInit(Entity<DecaCodeLockComponent> ent, ref MapInitEvent args)
    {
        if (IsValidCode(ent.Comp.SecretCode))
            return;

        ent.Comp.SecretCode = GenerateCode();
    }

    /// <summary>
    /// A locked crate never opens on a plain click and never shows the code window that way.
    /// The interaction is consumed so <see cref="SharedEntityStorageSystem"/> cannot lift the lid.
    /// </summary>
    private void OnActivate(Entity<DecaCodeLockComponent> ent, ref ActivateInWorldEvent args)
    {
        if (!ent.Comp.IsLocked)
            return;

        args.Handled = true;
    }

    /// <summary>
    /// While locked, the multitool is the only item that opens the Deca-Code window.
    /// Any deconstruction tool instead detonates the anti-tamper charge on the spot.
    /// </summary>
    private void OnInteractUsing(Entity<DecaCodeLockComponent> ent, ref InteractUsingEvent args)
    {
        if (!ent.Comp.IsLocked)
            return;

        // Checked first: the multitool also carries a ToolComponent and must never count as tampering.
        if (_tags.HasTag(args.Used, MultitoolTag))
        {
            args.Handled = true;
            OpenLockUi(ent.Owner, ent.Comp, args.User);
            return;
        }

        if (!IsTamperingTool(args.Used))
            return;

        // Consume the interaction so ConstructionSystem, PryingSystem and WeldableSystem
        // cannot start a deconstruction do-after on the doomed crate.
        args.Handled = true;
        Detonate(ent.Owner, ent.Comp, args.User);
    }

    /// <summary>
    /// Hitting a locked crate with anything at all sets the bomb off.
    /// </summary>
    private void OnAttacked(Entity<DecaCodeLockComponent> ent, ref AttackedEvent args)
    {
        if (!ent.Comp.IsLocked)
            return;

        Detonate(ent.Owner, ent.Comp, args.User);
    }

    /// <summary>
    /// A crate that leaves the world while still locked was destroyed: the charge goes off before
    /// the crate disappears. A blast this system already queued is skipped, and so is map, grid or
    /// round cleanup, which terminates every child entity without anyone destroying it.
    /// </summary>
    private void OnTerminating(Entity<DecaCodeLockComponent> ent, ref EntityTerminatingEvent args)
    {
        var alreadyDetonated = _detonated.Remove(ent.Owner);

        // The crate is going away, so its payload goes with it. Done first and unconditionally:
        // EntityStorage drops whatever it still holds when it shuts down, which would leave the
        // loot lying intact right next to the anti-tamper blast.
        PurgeContents(ent.Owner);

        var comp = ent.Comp;
        if (alreadyDetonated || !comp.IsLocked || !comp.BombArmed)
            return;

        if (Transform(ent.Owner).MapUid is not { } mapUid || TerminatingOrDeleted(mapUid))
            return;

        // The crate is already being deleted, so only the blast is queued here, never QueueDel.
        Explode(ent.Owner, null);
    }

    private void OnUiOpened(Entity<DecaCodeLockComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!args.UiKey.Equals(DecaCodeLockUiKey.Key))
            return;

        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnSubmit(Entity<DecaCodeLockComponent> ent, ref DecaCodeSubmitMessage args)
    {
        var comp = ent.Comp;

        if (!comp.IsLocked)
            return;

        // The lock is already spent (the crate was doomed): ignore further input.
        if (comp.FailedAttempts >= comp.MaxAttempts)
            return;

        var input = args.Code;
        if (!IsValidCode(input))
        {
            // Broken or malicious client input. Refresh the window, but do not burn an attempt.
            UpdateUi(ent.Owner, comp);
            return;
        }

        var (exact, partial) = CalculateMatches(comp.SecretCode, input);
        comp.Attempts.Add(new DecaCodeAttemptRecord(input, exact, partial));

        if (exact == DecaCodeLockComponent.CodeLength)
        {
            comp.IsLocked = false;
            Dirty(ent.Owner, comp);

            // Also release the native lock: it keeps the lid from being predicted open for a frame
            // and owns the lock overlay. Unlock() sets Locked, updates the appearance and dirties it.
            if (TryComp(ent.Owner, out LockComponent? lockComp) && lockComp.Locked)
                _lock.Unlock(ent.Owner, args.Actor, lockComp);

            UpdateUi(ent.Owner, comp);
            _ui.CloseUi(ent.Owner, DecaCodeLockUiKey.Key, args.Actor);
            return;
        }

        comp.FailedAttempts++;
        UpdateUi(ent.Owner, comp);

        if (comp.FailedAttempts >= comp.MaxAttempts)
            Detonate(ent.Owner, comp, args.Actor);
    }

    /// <summary>
    /// Pushes the sanitized state to the client and caches it for the next open.
    /// If the window is already open this updates the attempt table in place.
    /// </summary>
    private void UpdateUi(EntityUid uid, DecaCodeLockComponent comp)
    {
        var remaining = Math.Max(0, comp.MaxAttempts - comp.FailedAttempts);
        var state = new DecaCodeLockState(new List<DecaCodeAttemptRecord>(comp.Attempts), remaining);
        _ui.SetUiState(uid, DecaCodeLockUiKey.Key, state);
    }

    private void OpenLockUi(EntityUid uid, DecaCodeLockComponent comp, EntityUid user)
    {
        UpdateUi(uid, comp);
        // No-op when the actor already has this interface open, so this covers both
        // "open it" and "just refresh the table" without any chat feedback.
        _ui.TryOpenUi(uid, DecaCodeLockUiKey.Key, user);
    }

    /// <summary>
    /// Sets off the anti-tamper charge and removes the crate. An explicitly disarmed crate
    /// only keeps refusing access instead of exploding.
    /// </summary>
    private void Detonate(EntityUid uid, DecaCodeLockComponent comp, EntityUid? user)
    {
        if (!comp.BombArmed)
            return;

        if (!_detonated.Add(uid))
            return;

        // Destroy the payload before the crate, otherwise EntityStorage ejects it on shutdown
        // and the loot survives the blast on the floor.
        PurgeContents(uid);

        Explode(uid, user);

        // The blast is queued, so it still resolves after the crate and its UI are gone.
        QueueDel(uid);
    }

    /// <summary>
    /// Queues every entity inside the crate's <see cref="EntityStorageComponent"/> for deletion.
    /// Must run before the crate itself is removed while its contents are not supposed to survive.
    /// </summary>
    private void PurgeContents(EntityUid uid)
    {
        if (!TryComp(uid, out EntityStorageComponent? storage))
            return;

        // Copy first: the contained list must not be enumerated while it is being emptied.
        var contents = new List<EntityUid>(storage.Contents.ContainedEntities);
        foreach (var contained in contents)
        {
            if (!TerminatingOrDeleted(contained))
                QueueDel(contained);
        }
    }

    private void Explode(EntityUid uid, EntityUid? user)
    {
        _explosion.QueueExplosion(
            uid,
            BombExplosionType,
            BombTotalIntensity,
            BombIntensitySlope,
            BombMaxIntensity,
            canCreateVacuum: false,
            user: user);
    }

    /// <summary>
    /// Screwdrivers, crowbars, wirecutters, welders and every other construction tool carry
    /// <see cref="ToolComponent"/>, so one component check covers all deconstruction paths.
    /// </summary>
    private bool IsTamperingTool(EntityUid used)
    {
        return HasComp<ToolComponent>(used)
               || HasComp<WelderComponent>(used)
               || HasComp<MultipleToolComponent>(used);
    }

    /// <summary>
    /// Classic two pass mastermind scoring: first mark exact positions, then score the rest.
    /// </summary>
    private static (int Exact, int Partial) CalculateMatches(string secret, string input)
    {
        var length = secret.Length;
        var exact = 0;
        var partial = 0;
        var secretUsed = new bool[length];
        var inputUsed = new bool[length];

        for (var i = 0; i < length; i++)
        {
            if (secret[i] != input[i])
                continue;

            exact++;
            secretUsed[i] = true;
            inputUsed[i] = true;
        }

        for (var i = 0; i < length; i++)
        {
            if (inputUsed[i])
                continue;

            for (var j = 0; j < length; j++)
            {
                if (secretUsed[j] || input[i] != secret[j])
                    continue;

                partial++;
                secretUsed[j] = true;
                inputUsed[i] = true;
                break;
            }
        }

        return (exact, partial);
    }

    private static bool IsValidCode(string? code)
    {
        if (code == null || code.Length != DecaCodeLockComponent.CodeLength)
            return false;

        var seen = new bool[10];
        foreach (var c in code)
        {
            if (c is < '0' or > '9')
                return false;

            var digit = c - '0';
            if (seen[digit])
                return false;

            seen[digit] = true;
        }

        return true;
    }

    private string GenerateCode()
    {
        var pool = new List<char>(10);
        for (var c = '0'; c <= '9'; c++)
            pool.Add(c);

        var code = new char[DecaCodeLockComponent.CodeLength];
        for (var i = 0; i < code.Length; i++)
        {
            var index = _random.Next(pool.Count);
            code[i] = pool[index];
            pool.RemoveAt(index);
        }

        return new string(code);
    }
}
