// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._CorvaxNext.Silicons.Borgs.Components;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared._Lavaland.BossMechanics;
using Content.Shared.Actions;
using Content.Shared.Body.Events;
using Content.Shared.Popups;
using Content.Shared.StationAi;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// AI-uplink brain: while the organ sits in a robotic body the station AI can take that body
/// over with its regular remote control verb.
/// </summary>
public sealed class AiUplinkSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AiUplinkBrainComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<AiUplinkBrainComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);
    }

    private void OnOrganAdded(Entity<AiUplinkBrainComponent> ent, ref OrganAddedToBodyEvent args)
    {
        var body = args.Body;

        // TG allows robotic organs only: the uplink brain replaces a positronic brain in a silicon body.
        if (!HasComp<SiliconComponent>(body))
        {
            _popup.PopupEntity(Loc.GetString("ai-uplink-organics"), body, body);
            return;
        }

        var controller = EnsureComp<AiRemoteControllerComponent>(body);
        EnsureComp<StationAiVisionComponent>(body);

        // The controller component is added at runtime, so it does not get a MapInit to set itself up.
        if (controller.BackToAiActionEntity == null)
        {
            EntityUid? action = null;
            _actions.AddAction(body, ref action, controller.BackToAiAction);
            controller.BackToAiActionEntity = action;
        }

        EnsureComp<AiUplinkShellComponent>(body);
        _popup.PopupEntity(Loc.GetString("ai-uplink-online"), body, body);
    }

    private void OnOrganRemoved(Entity<AiUplinkBrainComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        var body = args.OldBody;

        if (!HasComp<AiUplinkShellComponent>(body))
            return;

        RemComp<AiUplinkShellComponent>(body);
        // Shutdown of the controller returns the AI mind to its core and cleans the action up.
        RemComp<AiRemoteControllerComponent>(body);
        _popup.PopupEntity(Loc.GetString("ai-uplink-offline"), body, body);
    }
}
