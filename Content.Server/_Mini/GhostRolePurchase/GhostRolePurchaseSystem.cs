// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server._Mini.AntagTokens;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared._Mini.GhostRolePurchase;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._Mini.GhostRolePurchase;

public sealed class GhostRolePurchaseSystem : EntitySystem
{
    [Dependency] private readonly GhostRolePurchaseTimerSystem _timerSystem = default!;
    [Dependency] private readonly AntagTokenSystem _antagTokens = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private static readonly HashSet<string> AnyModePresets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Survival", "SurvivalPlusMid", "Traitor", "Zombie", "Revolutionary", "Nukeops",
        "Blob", "Heretic", "Shadowling", "Xenomorphs", "Changeling",
        "TheGhost", "TheGuide", "SecretPlusLow", "SecretPlusMid", "SecretPlusAdmeme",
        "Secret", "AllAtOnce", "AllerAtOnce", "OopsAllThieves", "CosmicCult",
        "RevTraitor", "RevLing", "NukeTraitor", "NukeLing", "Traitorling",
        "Honkops", "KesslerSyndrome", "Zombieteors", "Deathmatch",
    };

    private static readonly Dictionary<string, HashSet<string>> RoleAllowedPresets =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["Thief"] = AnyModePresets,

        ["Agent"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "TheGhost", "TheGuide", "SecretPlusLow", "SecretPlusMid",
            "Survival", "SurvivalPlusMid",
            "SecretPlusAdmeme",
            "Traitor", "RevTraitor", "NukeTraitor", "Traitorling",
            "OopsAllThieves",
        },

        ["Ninja"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "TheGuide", "SecretPlusMid",
            "Survival", "SurvivalPlusMid",
        },

        ["Dragon"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Survival", "SurvivalPlusMid",
        },

        ["Abductor"] = AnyModePresets,

        ["InitialInfected"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Zombie", "Zombieteors",
            "TheGuide", "SecretPlusMid",
            "Survival", "SurvivalPlusMid",
        },

        ["Revenant"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Survival", "SurvivalPlusMid",
            "Traitor", "Revolutionary", "Zombie", "Nukeops", "Blob",
            "Heretic", "Shadowling", "Xenomorphs", "Changeling",
            "TheGhost", "TheGuide", "SecretPlusLow", "SecretPlusMid", "SecretPlusAdmeme",
            "Secret", "AllAtOnce", "AllerAtOnce", "OopsAllThieves", "CosmicCult",
            "RevTraitor", "RevLing", "NukeTraitor", "NukeLing", "Traitorling",
            "Honkops", "KesslerSyndrome", "Zombieteors", "Deathmatch",
        },

        ["Yao"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Nukeops", "NukeTraitor", "NukeLing",
            "Survival", "SurvivalPlusMid",
        },

        ["RevolutionaryHead"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Revolutionary", "RevTraitor", "RevLing",
        },

        ["SpaceCultist"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "CosmicCult",
        },

        ["Blob"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Blob",
            "Survival", "SurvivalPlusMid",
        },

        ["Wizard"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Survival", "SurvivalPlusMid",
        },

        ["SlaughterDemon"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Survival", "SurvivalPlusMid",
        },

        ["Heretic"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Survival", "SurvivalPlusMid",
            "Heretic",
        },

        ["Changeling"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Survival", "SurvivalPlusMid",
            "Changeling", "Traitorling", "NukeLing", "RevLing",
        },

        ["Shadowling"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Shadowling",
            "Survival", "SurvivalPlusMid",
        },

        ["Slasher"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Survival", "SurvivalPlusMid",
        },

        ["Xenomorph"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Survival", "SurvivalPlusMid",
        },

        ["Bingle"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Survival", "SurvivalPlusMid",
        },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<GhostRolePurchaseRequestEvent>(OnPurchaseRequest);
    }

    private void OnPurchaseRequest(GhostRolePurchaseRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!TryPurchaseGhostRole(args.SenderSession, ev.RoleId, out var error))
        {
            if (args.SenderSession.AttachedEntity is { Valid: true } uid)
                _popup.PopupEntity(error ?? "Purchase failed.", uid, uid);
            return;
        }

        if (args.SenderSession.AttachedEntity is { Valid: true } successUid)
            _popup.PopupEntity("Ghost role purchased successfully!", successUid, successUid);
    }

    public bool TryPurchaseGhostRole(ICommonSession session, string roleId, out string? error)
    {
        error = null;

        if (_timerSystem.IsTimerActive())
        {
            var remaining = _timerSystem.GetRemainingTime();
            error = $"Ghost role purchases are blocked. Time remaining: {remaining:mm\\:ss}";
            return false;
        }

        if (!IsRoleAvailableInGameMode(roleId))
        {
            error = "This role is not available in the current game mode.";
            return false;
        }

        if (!GhostRolePriceCatalog.TryGetPrice(roleId, out var price))
        {
            error = "Role price not found.";
            return false;
        }

        if (session.AttachedEntity is not { Valid: true } uid)
        {
            error = "Player does not have a valid entity.";
            return false;
        }

        if (!TryComp<GhostRoleTicketComponent>(uid, out var tickets))
        {
            error = "Ticket component not found.";
            return false;
        }

        if (tickets.Tickets < price)
        {
            error = $"Not enough tickets. Required: {price}, available: {tickets.Tickets}";
            return false;
        }

        tickets.Tickets -= price;
        Dirty(uid, tickets);

        if (!_antagTokens.TryPurchaseForSession(session, roleId, out var purchaseError))
        {
            tickets.Tickets += price;
            Dirty(uid, tickets);
            error = purchaseError;
            return false;
        }

        _timerSystem.StartTimer();

        RaiseNetworkEvent(new GhostRoleTicketUpdateEvent(tickets.Tickets), session);

        RaiseLocalEvent(new GhostRolePurchasedEvent(session.UserId, roleId));

        return true;
    }

    public bool IsRoleAvailableInGameMode(string roleId)
    {
        var preset = _gameTicker.RunLevel == GameRunLevel.PreRoundLobby
            ? _gameTicker.Preset
            : _gameTicker.CurrentPreset ?? _gameTicker.Preset;

        if (preset == null)
            return true;

        if (!RoleAllowedPresets.TryGetValue(roleId, out var allowed))
            return false;

        return allowed.Contains(preset.ID);
    }

    public int GetRolePrice(string roleId)
    {
        return GhostRolePriceCatalog.TryGetPrice(roleId, out var price) ? price : 0;
    }
}
