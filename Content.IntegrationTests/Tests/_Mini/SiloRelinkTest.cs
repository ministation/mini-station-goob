// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Goobstation.Common.Silo;
using Content.IntegrationTests.Pair;
using Content.Shared._Goobstation.Silo;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Content.Client.Materials;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// Сило должно открываться (как прежнее хранилище материалов) и переживать разборку:
/// латы не должны оставаться со ссылкой на удалённое сило, а новое сило должно их подхватывать.
/// </summary>
[TestFixture]
public sealed class SiloRelinkTest
{
    private static async Task<(EntityUid silo, EntityUid grid, Vector2 pos)> LoadPaperAndFindSilo(TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapLoader = entMan.System<MapLoaderSystem>();

        await server.WaitPost(() =>
        {
            var opts = new DeserializationOptions { InitializeMaps = true };
            Assert.That(mapLoader.TryLoadMap(new ResPath("/Maps/_Mini/paper.yml"), out _, out _, opts), "map loads");
        });
        await pair.RunTicksSync(20);

        EntityUid silo = default;
        EntityUid grid = default;
        Vector2 pos = default;
        await server.WaitAssertion(() =>
        {
            var query = entMan.AllEntityQueryEnumerator<SiloComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var siloXform))
            {
                silo = uid;
                grid = siloXform.GridUid!.Value;
                pos = siloXform.LocalPosition;
            }

            Assert.That(silo, Is.Not.EqualTo(default(EntityUid)), "silo exists on paper map");
        });

        return (silo, grid, pos);
    }

    [Test]
    public async Task SiloHasWindow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var entMan = pair.Server.ResolveDependency<IEntityManager>();
        var (silo, _, _) = await LoadPaperAndFindSilo(pair);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<OreSiloComponent>(silo), "silo has the storage window component");
            Assert.That(entMan.HasComponent<UserInterfaceComponent>(silo), "silo has a user interface");

            var activatable = entMan.GetComponent<ActivatableUIComponent>(silo);
            Assert.That(activatable.Key, Is.EqualTo(OreSiloUiKey.Key), "silo opens the material silo window");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LatheSeesSiloMaterials()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, DummyTicker = true });
        var server = pair.Server;
        var client = pair.Client;
        var sEnt = server.ResolveDependency<IEntityManager>();
        var sMat = sEnt.System<SharedMaterialStorageSystem>();
        var sPower = sEnt.System<SharedPowerReceiverSystem>();
        var (silo, _, _) = await LoadPaperAndFindSilo(pair);

        NetEntity siloNet = default;
        NetEntity latheNet = default;

        await server.WaitAssertion(() =>
        {
            // В тестовом окружении сети питания не считаются, поэтому просто объявляем сило запитанным.
            sPower.SetNeedsPower(silo, false);
            Assert.That(sMat.TryChangeMaterialAmount(silo, "Steel", 1000), "steel added to silo");

            siloNet = sEnt.GetNetEntity(silo);

            var query = sEnt.AllEntityQueryEnumerator<SiloUtilizerComponent>();
            while (query.MoveNext(out var uid, out var utilizer))
            {
                if (utilizer.Silo != silo)
                    continue;

                latheNet = sEnt.GetNetEntity(uid);
                break;
            }

            Assert.That(latheNet, Is.Not.EqualTo(NetEntity.Invalid), "a lathe is linked to the silo");
        });

        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            var lathe = sEnt.GetEntity(latheNet);
            var stored = sMat.GetStoredMaterials((lathe, sEnt.GetComponent<MaterialStorageComponent>(lathe)));
            Assert.That(stored.TryGetValue("Steel", out var steel) ? steel : 0, Is.EqualTo(1000), "server: lathe sees the silo steel");
        });

        await client.WaitAssertion(() =>
        {
            var cEnt = client.ResolveDependency<IEntityManager>();
            var cMat = cEnt.System<MaterialStorageSystem>();
            Assert.That(cEnt.TryGetEntity(siloNet, out var cSilo), "client has the silo entity");
            var lathe = cEnt.GetEntity(latheNet);
            var stored = cMat.GetStoredMaterials((lathe, cEnt.GetComponent<MaterialStorageComponent>(lathe)));
            Assert.That(stored.TryGetValue("Steel", out var steel) ? steel : 0, Is.EqualTo(1000), "client lathe UI sees the silo steel");
            Assert.That(cEnt.HasComponent<MaterialStorageComponent>(cSilo!.Value), "client has silo storage");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SiloRelinksLathesAfterRebuild()
    {
        await using var pair = await PoolManager.GetServerClient();
        var entMan = pair.Server.ResolveDependency<IEntityManager>();
        var (silo, grid, pos) = await LoadPaperAndFindSilo(pair);

        EntityUid lathe = default;
        await pair.Server.WaitAssertion(() =>
        {
            var query = entMan.AllEntityQueryEnumerator<SiloUtilizerComponent>();
            while (query.MoveNext(out var uid, out var utilizer))
            {
                if (utilizer.Silo != silo)
                    continue;

                lathe = uid;
                break;
            }

            Assert.That(lathe, Is.Not.EqualTo(default(EntityUid)), "a lathe is linked to the silo");
        });

        // Разбираем сило: ссылки лат должны очиститься, иначе фабы навсегда теряют общие материалы,
        // а клиентам уходит битое состояние.
        await pair.Server.WaitAssertion(() => entMan.QueueDeleteEntity(silo));
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var utilizer = entMan.GetComponent<SiloUtilizerComponent>(lathe);
            Assert.That(utilizer.Silo, Is.Null, "lathe link is cleared after the silo is gone");
        });

        // Ставим новое сило на то же место: латы должны подключиться к нему сами.
        EntityUid newSilo = default;
        await pair.Server.WaitAssertion(() =>
        {
            newSilo = entMan.SpawnEntity("MaterialSilo", new EntityCoordinates(grid, pos));
        });
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(newSilo), "new silo spawned");
            var utilizer = entMan.GetComponent<SiloUtilizerComponent>(lathe);
            Assert.That(utilizer.Silo, Is.EqualTo(newSilo), "lathe is relinked to the new silo");
        });

        await pair.CleanReturnAsync();
    }
}
