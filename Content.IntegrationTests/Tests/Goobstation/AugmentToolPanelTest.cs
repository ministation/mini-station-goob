// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Server.Augments;
using Content.Goobstation.Shared.Augments;
using Content.Server.Body.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Goobstation;

[TestFixture]
[TestOf(typeof(AugmentToolPanelSystem))]
public sealed class AugmentToolPanelTest
{
    private sealed class ErrorLogCapture : ILogHandler
    {
        private readonly List<string> _messages = new();

        public void Log(string sawmillName, LogEvent message)
        {
            if (message.Level < LogEventLevel.Error)
                return;

            lock (_messages)
                _messages.Add(message.RenderMessage());
        }

        public string[] Snapshot()
        {
            lock (_messages)
                return _messages.ToArray();
        }
    }

    /// <summary>
    /// A lit welder is Larger than the panel storage allows, so retracting it used to fail to insert and
    /// leave the tool stuck in the hand with "Inserting tool ... back into ... failed" in the log.
    /// Retracting an active tool must turn it off before putting it back.
    /// </summary>
    [Test]
    public async Task RetractingActivatedToolTurnsItOffAndStoresIt()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var logMan = server.ResolveDependency<ILogManager>();
        var map = await pair.CreateTestMap();

        var errorLogs = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errorLogs);

        try
        {
            await server.WaitAssertion(() =>
            {
                var hands = entMan.System<SharedHandsSystem>();
                var storage = entMan.System<SharedStorageSystem>();
                var toggle = entMan.System<ItemToggleSystem>();
                var bodySystem = entMan.System<BodySystem>();
                var panelSystem = entMan.System<AugmentToolPanelSystem>();
                var xform = entMan.System<SharedTransformSystem>();

                var mob = entMan.SpawnEntity("MobHuman", map.GridCoords);
                var panel = entMan.SpawnEntity("AugmentToolsPanel", map.GridCoords);
                var welder = entMan.SpawnEntity("WelderExperimental", map.GridCoords);

                // the panel is implanted as an organ into the right arm, which is how Transform.ParentUid is set up
                var arm = bodySystem.GetBodyChildrenOfType(mob, BodyPartType.Arm, symmetry: BodyPartSymmetry.Right).First();
                entMan.GetComponent<OrganComponent>(panel).Body = mob;
                xform.SetParent(panel, arm.Id);

                Assert.That(storage.Insert(panel, welder, out _), Is.True,
                    "the welding tool must fit into the empty panel storage");

                var handId = entMan.GetComponent<HandsComponent>(mob).Hands
                    .First(hand => hand.Value.Location == HandLocation.Right).Key;
                hands.DoPickup(mob, handId, welder);
                entMan.AddComponent<AugmentToolPanelActiveItemComponent>(welder);

                Assert.That(toggle.TryActivate(welder, user: mob), Is.True, "the welding tool must be usable");
                Assert.That(entMan.GetComponent<ItemComponent>(welder).Size,
                    Is.EqualTo(new ProtoId<ItemSizePrototype>("Large")),
                    "a lit welder is Large and does not fit the panel storage");

                panelSystem.SwitchTool((panel, entMan.GetComponent<AugmentToolPanelComponent>(panel)), null, mob);

                Assert.Multiple(() =>
                {
                    Assert.That(toggle.IsActivated(welder), Is.False,
                        "retracting an active tool must turn it off");
                    Assert.That(hands.GetHeldItem(mob, handId), Is.Null,
                        "the retracted tool must leave the hand");
                    Assert.That(entMan.GetComponent<StorageComponent>(panel).Container.ContainedEntities,
                        Does.Contain(welder), "the retracted tool must be back in the panel storage");
                    Assert.That(errorLogs.Snapshot().Where(message => message.Contains("Inserting tool")),
                        Is.Empty, "retracting a tool must not log an insertion failure");
                });
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errorLogs);
        }

        await pair.CleanReturnAsync();
    }
}
