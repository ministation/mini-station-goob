#nullable enable

using Content.IntegrationTests.Pair;
using Content.Server._Mini.Pinpointer;
using Content.Server._Mini.Pinpointer.Systems;
using Content.Shared.Pinpointer;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// Регрессия на пинпоинтер ОБР: переключение цели между шаттлом отряда и ядерной бомбой.
/// </summary>
[TestFixture]
[TestOf(typeof(ErtShuttlePinpointerSystem))]
public sealed class ErtShuttlePinpointerTest
{
    [Test]
    public async Task ToggleSwitchesBetweenShuttleAndBomb()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
        });
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IServerEntityManager>();
        var ertPin = sEntMan.System<ErtShuttlePinpointerSystem>();

        await server.WaitIdleAsync();
        await pair.CreateTestMap();

        EntityUid pin = default;
        EntityUid marker = default;
        await server.WaitPost(() =>
        {
            var coords = pair.TestMap!.GridCoords;
            pin = sEntMan.SpawnEntity("PinpointerNuclear", coords);
            marker = sEntMan.SpawnEntity("CryogenicSleepUnitSpawnerERT", coords);
        });
        await server.WaitRunTicks(2);

        var pinComp = sEntMan.GetComponent<PinpointerComponent>(pin);
        await server.WaitPost(() => ertPin.AttachShuttleTarget(pin, marker, null));
        var ertComp = sEntMan.GetComponent<ErtShuttlePinpointerComponent>(pin);

        // Бомба -> шаттл: пинпоинтер держит маркер шаттла.
        await server.WaitPost(() => ertPin.Toggle(pin, ertComp));
        Assert.That(ertComp.ShowingShuttle, Is.True, "mode must switch to shuttle");
        Assert.That(pinComp.Targets, Does.Contain(marker), "shuttle target must be tracked after switching");

        // Шаттл -> бомба: явная цель очищается, возвращается поиск по whitelist.
        await server.WaitPost(() => ertPin.Toggle(pin, ertComp));
        Assert.That(ertComp.ShowingShuttle, Is.False, "mode must switch back to bomb");
        Assert.That(pinComp.Targets, Is.Empty, "bomb mode must clear the explicit target so whitelist search resumes");

        await pair.CleanReturnAsync();
    }
}
