// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.Body.Part;
using Content.Shared.Damage.Components;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

/// <summary>
/// Extracting a heart end to end: a surgeon with the right tools, a patient lying down, the
/// prerequisite surgeries and the final "Remove organ" step.
///
/// The delayed case is the regression test: a step used to count as done only while the temporary
/// SurgeryPain modifier from that step was still alive (30-60 s). A surgeon who paused - fetching a
/// tool, talking, dragging the patient - came back to a procedure that had reset itself, was refused
/// the last step with "invalid surgery" and could not pull the organ out at all.
/// </summary>
[TestFixture]
public sealed class HeartRemovalTest
{
    private const string OpenIncision = "SurgeryOpenIncision";
    private const string OpenRibcage = "SurgeryOpenRibcage";
    private const string RemoveHeart = "SurgeryRemoveHeart";
    private const string RemoveOrganStep = "SurgeryStepRemoveOrgan";

    private sealed class Scenario
    {
        public EntityUid Patient;
        public EntityUid Chest;
        public EntityUid Heart;
        public EntityUid Surgeon;
        public EntityUid Scalpel;
        public EntityUid Retractor;
        public EntityUid Saw;
        public EntityUid Hemostat;
        public string Who = string.Empty;
    }

    [Test]
    public async Task HeartCanBeRemoved()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Connected = false,
            InLobby = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();

        Scenario Prepare(string who)
        {
            var entMan = server.EntMan;
            var body = entMan.System<BodySystem>();
            var hands = entMan.System<HandsSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var surgery = entMan.System<Content.Server._Shitmed.Medical.Surgery.SurgerySystem>();

            var scenario = new Scenario { Who = who };
            scenario.Patient = entMan.SpawnAtPosition("MobHuman", map.GridCoords);
            scenario.Surgeon = entMan.SpawnAtPosition("MobHuman", map.GridCoords);

            var scalpel = scenario.Scalpel = entMan.SpawnAtPosition("Scalpel", map.GridCoords);
            var retractor = scenario.Retractor = entMan.SpawnAtPosition("Retractor", map.GridCoords);
            var saw = scenario.Saw = entMan.SpawnAtPosition("Saw", map.GridCoords);
            var hemostat = scenario.Hemostat = entMan.SpawnAtPosition("Hemostat", map.GridCoords);

            // Untrained speed is 0.25x and the steps take seconds; keep the test quick.
            entMan.GetComponent<ScalpelComponent>(scalpel).Speed = 100f;
            entMan.GetComponent<RetractorComponent>(retractor).Speed = 100f;
            entMan.GetComponent<BoneSawComponent>(saw).Speed = 100f;
            entMan.GetComponent<HemostatComponent>(hemostat).Speed = 100f;
            entMan.GetComponent<TweezersComponent>(hemostat).Speed = 100f;

            scenario.Chest = body.GetBodyChildrenOfType(scenario.Patient, BodyPartType.Chest).First().Id;
            Assert.That(body.TryGetBodyPartOrgans(scenario.Chest, typeof(HeartComponent), out var organs), Is.True,
                $"[{who}] no heart in the chest");
            scenario.Heart = organs![0].Id;

            Assert.That(hands.TryPickup(scenario.Surgeon, hemostat, checkActionBlocker: false, animate: false), Is.True,
                $"[{who}] the surgeon could not hold the hemostat");
            standing.Down(scenario.Patient);

            // The test map is vacuum: without this the pair suffocates (and the surgeon drops the
            // tool) over the minute the delayed scenario waits, which has nothing to do with surgery.
            entMan.AddComponent<GodmodeComponent>(scenario.Surgeon);
            entMan.AddComponent<GodmodeComponent>(scenario.Patient);

            // The prerequisite surgeries, step by step, with the tool each step asks for.
            ApplyStep(scenario, OpenIncision, "SurgeryStepOpenIncisionScalpel", scalpel);
            ApplyStep(scenario, OpenIncision, "SurgeryStepRetractSkin", retractor);
            ApplyStep(scenario, OpenRibcage, "SurgeryStepSawBones", saw);
            ApplyStep(scenario, OpenRibcage, "SurgeryStepPriseOpenBones", retractor);
            ApplyStep(scenario, RemoveHeart, "SurgeryStepOpenIncisionScalpel", scalpel);
            ApplyStep(scenario, RemoveHeart, "SurgeryStepClampBleeders", hemostat);

            return scenario;
        }

        void ApplyStep(Scenario scenario, string surgeryProto, string stepProto, EntityUid tool)
        {
            var entMan = server.EntMan;
            var surgery = entMan.System<Content.Server._Shitmed.Medical.Surgery.SurgerySystem>();
            var surgeryEnt = surgery.GetSingleton(surgeryProto);
            var stepEnt = surgery.GetSingleton(stepProto);
            Assert.That(surgeryEnt, Is.Not.Null, $"[{scenario.Who}] {surgeryProto} is missing");
            Assert.That(stepEnt, Is.Not.Null, $"[{scenario.Who}] {stepProto} is missing");

            var ev = new SurgeryStepEvent(scenario.Surgeon, scenario.Patient, scenario.Chest, tool,
                surgeryEnt!.Value, stepEnt!.Value, false);
            entMan.EventBus.RaiseLocalEvent(stepEnt.Value, ref ev);
        }

        bool IsStepComplete(Scenario scenario, string surgeryProto, int index)
        {
            var entMan = server.EntMan;
            var surgery = entMan.System<Content.Server._Shitmed.Medical.Surgery.SurgerySystem>();
            var surgeryEnt = surgery.GetSingleton(surgeryProto)!.Value;
            var comp = entMan.GetComponent<SurgeryComponent>(surgeryEnt);
            var stepEnt = surgery.GetSingleton(comp.Steps[index])!.Value;

            var check = new SurgeryStepCompleteCheckEvent(scenario.Patient, scenario.Chest, surgeryEnt);
            entMan.EventBus.RaiseLocalEvent(stepEnt, ref check);
            return !check.Cancelled;
        }

        void RemoveOrgan(Scenario scenario)
        {
            var entMan = server.EntMan;
            var surgery = entMan.System<Content.Server._Shitmed.Medical.Surgery.SurgerySystem>();

            // The surgeon takes the tool back into the active hand before the last step.
            var hands = entMan.System<HandsSystem>();
            if (!hands.IsHolding(scenario.Surgeon, scenario.Hemostat))
                Assert.That(hands.TryPickupAnyHand(scenario.Surgeon, scenario.Hemostat, checkActionBlocker: false, animate: false),
                    Is.True, $"[{scenario.Who}] could not take the hemostat again");

            var ok = surgery.TryDoSurgeryStep(scenario.Patient, scenario.Chest, scenario.Surgeon,
                RemoveHeart, RemoveOrganStep, out var error);
            TestContext.WriteLine($"[{scenario.Who}] server accepted the Remove organ step = {ok}, reason = {error}");
            Assert.That(ok, Is.True,
                $"[{scenario.Who}] the server refused the Remove organ step: {error} (the heart cannot be cut out)");
        }

        void AssertHeartOut(Scenario scenario)
        {
            var body = server.EntMan.System<BodySystem>();
            var stillInside = body.TryGetBodyPartOrgans(scenario.Chest, typeof(HeartComponent), out _);
            TestContext.WriteLine($"[{scenario.Who}] heart still inside the chest: {stillInside}");
            Assert.That(stillInside, Is.False, $"[{scenario.Who}] the heart is still inside the chest");
        }

        // 1. A surgeon who works without pauses finishes the operation.
        Scenario quick = null;
        await server.WaitPost(() => quick = Prepare("quick surgeon"));
        await pair.RunTicksSync(10);
        await server.WaitPost(() => RemoveOrgan(quick));
        await pair.RunTicksSync(30);
        await server.WaitPost(() => AssertHeartOut(quick));

        // 2. A surgeon who is called away for a minute: the steps that were performed must still
        //    count as performed, and the operation must be finishable after following the window
        //    (bleeding wounds may legitimately ask to be clamped again).
        Scenario delayed = null;
        await server.WaitPost(() => delayed = Prepare("surgeon who took a minute"));
        await pair.RunTicksSync(1800);

        await server.WaitPost(() =>
        {
            Assert.That(IsStepComplete(delayed!, OpenIncision, 0), Is.True,
                "a performed step (Cut with a scalpel) un-completed itself while the surgeon was away");
            Assert.That(IsStepComplete(delayed!, OpenRibcage, 0), Is.True,
                "a performed step (Saw through bones) un-completed itself while the surgeon was away");
            Assert.That(IsStepComplete(delayed!, RemoveHeart, 0), Is.True,
                "a performed step (Cut with a scalpel) un-completed itself while the surgeon was away");
        });

        await server.WaitPost(() =>
        {
            var surgery = server.EntMan.System<Content.Server._Shitmed.Medical.Surgery.SurgerySystem>();
            var removeHeart = surgery.GetSingleton(RemoveHeart)!.Value;
            var next = surgery.GetNextStep(delayed!.Patient, delayed.Chest, removeHeart, delayed.Surgeon);
            var asked = next == null ? "nothing" : next.Value.Surgery.Comp.Steps[next.Value.Step].Id;
            TestContext.WriteLine($"[{delayed.Who}] the surgery window now asks for: {asked}");

            // The wound the operation opened bleeds again during the pause, and the window asks for
            // exactly the clamp step: clamp it again like a real surgeon would and carry on.
            if (next != null && next.Value.Surgery.Owner == removeHeart && next.Value.Step == 1)
                ApplyStep(delayed, RemoveHeart, "SurgeryStepClampBleeders", delayed.Hemostat);
        });
        await pair.RunTicksSync(10);

        await server.WaitPost(() => RemoveOrgan(delayed!));
        await pair.RunTicksSync(30);
        await server.WaitPost(() => AssertHeartOut(delayed!));

        await pair.CleanReturnAsync();
    }
}
