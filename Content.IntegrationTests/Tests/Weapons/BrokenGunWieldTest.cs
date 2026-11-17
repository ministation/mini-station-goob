// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Weapons;

/// <summary>
/// Reproduces the production line "system.gun: Can't resolve
/// \"Content.Shared.Weapons.Ranged.Components.GunComponent\" on entity поломаный L6 SAW
/// (4316920/n4316920, WeaponLightMachineGunL6)!". A broken weapon keeps its
/// <see cref="GunWieldBonusComponent"/> while the <see cref="GunComponent"/> is removed (the silly
/// map stores the broken L6 SAW with the Gun component missing). Wielding such a weapon used to
/// resolve the missing component with logging, although the state is intended.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedGunSystem))]
public sealed class BrokenGunWieldTest
{
    private sealed class ErrorLogCapture : ILogHandler
    {
        public readonly List<string> Messages = [];

        public void Log(string sawmillName, LogEvent message)
        {
            if (message.Level < LogEventLevel.Error)
                return;

            lock (Messages)
                Messages.Add(message.RenderMessage());
        }

        public void Clear()
        {
            lock (Messages)
                Messages.Clear();
        }

        public string[] Snapshot()
        {
            lock (Messages)
                return Messages.ToArray();
        }
    }

    [Test]
    public async Task WieldBrokenGunWithoutGunComponentDoesNotLogError()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var logMan = server.ResolveDependency<ILogManager>();
        var map = await pair.CreateTestMap();

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        try
        {
            await server.WaitPost(() =>
            {
                var gun = entMan.SpawnEntity("WeaponLightMachineGunL6", map.GridCoords);
                var user = entMan.SpawnEntity("MobHuman", map.GridCoords);

                // The same state as the broken L6 SAW on the silly map: the gun component is removed,
                // the wield bonus and the wieldable component stay.
                entMan.RemoveComponent<GunComponent>(gun);
                Assert.That(entMan.HasComponent<GunComponent>(gun), Is.False);
                Assert.That(entMan.HasComponent<GunWieldBonusComponent>(gun), Is.True);
                Assert.That(entMan.HasComponent<WieldableComponent>(gun), Is.True);

                errors.Clear();

                // SharedWieldableSystem.TryWield raises this event on the used item.
                var wielded = new ItemWieldedEvent(user);
                entMan.EventBus.RaiseLocalEvent(gun, ref wielded);

                var resolveErrors = errors.Snapshot()
                    .Where(message => message.Contains("Can't resolve") && message.Contains(nameof(GunComponent)))
                    .ToArray();
                Assert.That(resolveErrors, Is.Empty,
                    $"wielding a broken gun without a GunComponent must not resolve it: {string.Join('\n', resolveErrors)}");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }
}
