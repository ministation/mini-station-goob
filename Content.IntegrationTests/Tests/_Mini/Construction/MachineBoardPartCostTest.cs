// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using Content.Shared.Construction;
using Content.Shared.Construction.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Mini.Construction;

/// <summary>
/// Mini/Orion MachineParts: ensures machine board part requirements are counted exactly once
/// in the material cost (used by the flatpacker to charge materials).
/// Regression test for a copy-pasted duplicate PartRequirements loop that doubled the cost.
/// </summary>
[TestFixture]
[TestOf(typeof(MachinePartSystem))]
public sealed class MachineBoardPartCostTest
{
    private const string PartEntityId = "MachineBoardPartCostTestPart";
    private const string PartTypeId = "MachineBoardPartCostTestPartType";
    private const string BoardEntityId = "MachineBoardPartCostTestBoard";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {PartEntityId}
  name: {PartEntityId}
  components:
  - type: PhysicalComposition
    materialComposition:
      Steel: 100

- type: machinePart
  id: {PartTypeId}
  name: machine-part-name-servo
  stockPartPrototype: {PartEntityId}

- type: entity
  id: {BoardEntityId}
  name: {BoardEntityId}
  components:
  - type: MachineBoard
    prototype: Autolathe
    partRequirements:
      {PartTypeId}: 4";

    [Test]
    public async Task PartRequirementsCountedOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var machinePartSystem = server.System<MachinePartSystem>();

        EntityUid board = default;
        await server.WaitPost(() => board = entMan.SpawnEntity(BoardEntityId, MapCoordinates.Nullspace));

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<MachineBoardComponent>(board);

            Assert.That(machinePartSystem.TryGetMachineBoardMaterialCost((board, comp), out var materials),
                "Failed to get machine board material cost");

            // 4 parts x 100 steel each, counted exactly once.
            Assert.That(materials, Contains.Key("Steel"));
            Assert.That(materials["Steel"], Is.EqualTo(400),
                "Machine board part material cost must count each part requirement exactly once");

            entMan.DeleteEntity(board);
        });
    }
}
