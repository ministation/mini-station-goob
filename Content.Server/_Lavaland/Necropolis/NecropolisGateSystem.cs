// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Managers;
using System.Numerics;
using System.Threading;
using Content.Shared._Lavaland.Necropolis;
using Content.Shared.Chat;
using Content.Shared.Flash;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Necropolis;

/// <summary>
/// Builds the necropolis gate landmark and releases Legion when it is knocked open.
/// Port of TG's necropolis_gate.dm, adapted to the procedural lavaland planet.
/// </summary>
public sealed class NecropolisGateSystem : EntitySystem
{
    private static readonly SoundPathSpecifier KnockSound = new("/Audio/Effects/metal_slam2.ogg");
    private static readonly SoundPathSpecifier OpenSound = new("/Audio/Effects/stonedoor_openclose.ogg");
    private static readonly SoundPathSpecifier ReleaseSound = new("/Audio/_Lavaland/Mobs/legion_spawn.ogg");

    private const string StoneTile = "LavalandStoneTilePristine";
    private const string Wall = "WallNecropolisIndestructible";
    private const string Gate = "LavalandNecropolisGate";
    private const string Arch = "LavalandNecropolisArch";

    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecropolisGateSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
        SubscribeLocalEvent<NecropolisGateComponent, InteractHandEvent>(OnGateInteract);
    }

    private void OnSpawnerMapInit(Entity<NecropolisGateSpawnerComponent> ent, ref MapInitEvent args)
    {
        BuildStructure(ent);
        QueueDel(ent);
    }

    private void BuildStructure(Entity<NecropolisGateSpawnerComponent> ent)
    {
        var comp = ent.Comp;
        var baseCoords = Transform(ent).Coordinates;
        var gateCoords = baseCoords.Offset(comp.GateOffset);

        // Stone arena patch under the whole landmark.
        for (var x = -comp.ArenaRadius; x <= comp.ArenaRadius; x++)
        {
            for (var y = -comp.ArenaRadius; y <= comp.ArenaRadius + comp.TowerHeight + 1; y++)
            {
                Spawn(StoneTile, baseCoords.Offset(new Vector2(x, y)));
            }
        }

        // Wall row with a gap for the gate.
        var gateX = comp.GateOffset.X;
        for (var x = -comp.ArenaRadius + 2; x <= comp.ArenaRadius - 2; x++)
        {
            if (x == gateX)
                continue;
            Spawn(Wall, baseCoords.Offset(new Vector2(x, comp.GateOffset.Y)));
        }

        // Massive stone tower behind the gate.
        for (var x = gateX - 2; x <= gateX + 2; x++)
        {
            for (var y = 1; y <= comp.TowerHeight; y++)
            {
                Spawn(Wall, baseCoords.Offset(new Vector2(x, comp.GateOffset.Y + y)));
            }
        }

        Spawn(Gate, gateCoords);
        Spawn(Arch, gateCoords);
    }

    private void OnGateInteract(Entity<NecropolisGateComponent> ent, ref InteractHandEvent args)
    {
        if (ent.Comp.Open)
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("necropolis-gate-open"), ent, args.User);
            return;
        }

        if (args.Handled)
            return;
        args.Handled = true;

        var now = _timing.CurTime;
        if (ent.Comp.LastKnockAttempt is { } lastAttempt
            && now - lastAttempt < TimeSpan.FromSeconds(ent.Comp.KnockCooldown))
        {
            return;
        }

        ent.Comp.LastKnockAttempt = now;
        if (ent.Comp.LastKnock is not { } windowStart
            || now - windowStart > TimeSpan.FromSeconds(ent.Comp.KnockWindow))
        {
            ent.Comp.Knocks = 0;
            ent.Comp.LastKnock = now;
        }

        ent.Comp.Knocks++;

        _audio.PlayPvs(KnockSound, ent);
        _popup.PopupEntity(Loc.GetString("necropolis-gate-knock-self"), ent, args.User);
        _popup.PopupEntity(
            Loc.GetString("necropolis-gate-knock-others", ("user", args.User)),
            ent);

        if (ent.Comp.Knocks >= ent.Comp.KnocksRequired)
            OpenGate(ent);
    }

    private void OpenGate(Entity<NecropolisGateComponent> ent)
    {
        ent.Comp.Open = true;
        _appearance.SetData(ent, NecropolisGateVisuals.State, NecropolisGateVisualState.Opening);

        _audio.PlayPvs(OpenSound, ent);
        _popup.PopupEntity(Loc.GetString("necropolis-gate-opening"), ent);

        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(3.1f), () => FinishOpening(ent), CancellationToken.None);
    }

    private void FinishOpening(Entity<NecropolisGateComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _appearance.SetData(ent, NecropolisGateVisuals.State, NecropolisGateVisualState.Open);
        _physics.SetCanCollide(ent, false);

        var coords = Transform(ent).Coordinates.Offset(new Vector2(0, -1));
        Spawn(ent.Comp.SpawnOnOpen, coords);

        _popup.PopupEntity(Loc.GetString("necropolis-gate-released"), ent);
        AnnounceRelease(ent);
    }

    private void AnnounceRelease(Entity<NecropolisGateComponent> ent)
    {
        var mapId = Transform(ent).MapID;
        var filter = Filter.Empty();

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } player
                || !TryComp<TransformComponent>(player, out var xform)
                || xform.MapID != mapId)
            {
                continue;
            }

            filter.AddPlayer(session);
            _flash.Flash(player, null, null, TimeSpan.FromSeconds(2f), 1f, displayPopup: false, ignoreProtection: true);
        }

        var message = Loc.GetString("necropolis-gate-whispers");
        _chat.ChatMessageToManyFiltered(
            filter,
            ChatChannel.Local,
            message,
            message,
            ent,
            false,
            true,
            Color.FromHex("#C20000"),
            ReleaseSound.Path.ToString());
    }
}
