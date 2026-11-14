// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Chemistry;

/// <summary>
/// Reproduces the production line "system.chemical_reaction: Solution 3719413 could not finish reacting
/// in under 20 loops." from a large beaker with Bicaridine, Sugar and Lithium.
/// The Bruizine reaction has a fractional Lithium amount (0.9u). When only a 0.01u Lithium residue is
/// left, the amount the reaction removes rounds down to zero, so the reagent is never consumed and the
/// reaction keeps running until the iteration limit. It even makes Razorium without using up Lithium.
/// </summary>
[TestFixture]
[TestOf(typeof(ChemicalReactionSystem))]
public sealed class ReactionIterationLimitTest
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

        public string[] Snapshot()
        {
            lock (Messages)
                return Messages.ToArray();
        }
    }

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestReactionLoopContainer
  components:
  - type: SolutionContainerManager
    solutions:
      beaker:
        maxVol: 50
        canMix: true";

    [Test]
    public async Task FractionalReactantStopsReacting()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var logMan = server.ResolveDependency<ILogManager>();
        var solutionContainerSystem = entMan.System<SharedSolutionContainerSystem>();
        var map = await pair.CreateTestMap();

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        try
        {
            await server.WaitAssertion(() =>
            {
                var beaker = entMan.SpawnEntity("TestReactionLoopContainer", map.GridCoords);
                Assert.That(solutionContainerSystem.TryGetSolution(beaker, "beaker", out var solutionEnt, out var solution));

                solutionEnt.Value.Comp.Solution.CanReact = false;
                Assert.That(solutionContainerSystem.TryAddReagent(solutionEnt.Value, "Bicaridine", 30, out _));
                Assert.That(solutionContainerSystem.TryAddReagent(solutionEnt.Value, "Sugar", 1, out _));
                Assert.That(solutionContainerSystem.TryAddReagent(solutionEnt.Value, "Lithium", 1, out _));

                // First part of the production mix: Bicaridine + Sugar + Lithium makes Bruizine and the
                // leftover Bicaridine turns it into Razorium, which leaves a 0.01u Lithium residue.
                solutionEnt.Value.Comp.Solution.CanReact = true;
                solutionContainerSystem.UpdateChemicals(solutionEnt.Value);
                Assert.That(solution.GetTotalPrototypeQuantity("Bicaridine"), Is.EqualTo(FixedPoint2.New(27)));

                // The second unit of Sugar made the Bruizine reaction applicable again. Without the guard
                // it repeats at the smallest fixed point step and hits MaxReactionIterations.
                Assert.That(solutionContainerSystem.TryAddReagent(solutionEnt.Value, "Sugar", 1, out _));

                var loopErrors = errors.Snapshot()
                    .Where(message => message.Contains("could not finish reacting"))
                    .ToArray();
                Assert.That(loopErrors, Is.Empty,
                    $"a reaction must not repeat without consuming its reactants: {string.Join('\n', loopErrors)}");

                // The residue cannot be consumed, so the solution has to settle on it instead of reacting on.
                Assert.That(solution.GetTotalPrototypeQuantity("Lithium"), Is.EqualTo(FixedPoint2.New(0.01)));
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }
}
