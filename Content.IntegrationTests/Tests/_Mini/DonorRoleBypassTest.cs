// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;

using Content.Server.Players.PlayTimeTracking;
using Content.Server._Mini.AntagTokens;
using Content.Shared.CCVar;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// Donors must bypass playtime requirements on both the client and the server, otherwise
/// the role looks open in the lobby but joining is silently rejected.
/// </summary>
[TestFixture]
public sealed class DonorRoleBypassTest
{
    [Test, NonParallelizable]
    public async Task DonorSkipsPlaytimeRequirements()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });

        await pair.Server.WaitAssertion(() =>
        {
            var cfg = pair.Server.ResolveDependency<IConfigurationManager>();
            var playtime = pair.Server.System<PlayTimeTrackingSystem>();
            var tokens = pair.Server.System<AntagTokenSystem>();
            var session = pair.Server.PlayerMan.Sessions.Single();

            // Integration tests run with role timers off, turn them on for this check.
            cfg.SetCVar(CCVars.GameRoleTimers, true);

            // Chaplain requires 15 hours of overall playtime.
            var jobId = new ProtoId<JobPrototype>("Chaplain");

            try
            {
                Assert.That(playtime.IsAllowed(session, jobId), Is.False,
                    "A fresh session should not meet playtime requirements");

                tokens.SetSponsorLevelOverride(session.UserId, 1);
                try
                {
                    Assert.That(playtime.IsAllowed(session, jobId), Is.True,
                        "A donor must bypass playtime requirements");
                    Assert.That(playtime.GetDisallowedJobs(session), Does.Not.Contain(jobId),
                        "A donor job must not be marked as disallowed");
                }
                finally
                {
                    tokens.SetSponsorLevelOverride(session.UserId, null);
                }

                Assert.That(playtime.IsAllowed(session, jobId), Is.False,
                    "The bypass must go away with the sponsor level");
            }
            finally
            {
                cfg.SetCVar(CCVars.GameRoleTimers, false);
            }
        });

        await pair.CleanReturnAsync();
    }
}
