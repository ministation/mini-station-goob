// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Immutable;
using Content.Server.Database;
using Content.Shared.Database;
using NUnit.Framework;
using Robust.Shared.Network;

namespace Content.Tests.Server.Database;

[TestFixture]
[TestOf(typeof(BanDef))]
public sealed class BanDefTest
{
    private static BanDef CreateBan(BanType type,
        ServerBanExemptFlags exemptFlags = ServerBanExemptFlags.None,
        ImmutableArray<BanRoleDef>? roles = null)
    {
        return new BanDef(
            null,
            type,
            [new NetUserId(Guid.NewGuid())],
            [],
            [],
            DateTimeOffset.UtcNow,
            null,
            [],
            TimeSpan.Zero,
            "voice chat ban test",
            NoteSeverity.Medium,
            null,
            null,
            exemptFlags,
            roles);
    }

    [Test]
    public void VoiceBanIsValid()
    {
        Assert.DoesNotThrow(() => CreateBan(BanType.Voice));
    }

    [Test]
    public void VoiceBanRejectsRoles()
    {
        Assert.Throws<ArgumentException>(() => CreateBan(BanType.Voice, roles: [new BanRoleDef("Job", "Captain")]));
    }

    [Test]
    public void VoiceBanRejectsExemptFlags()
    {
        Assert.Throws<ArgumentException>(() => CreateBan(BanType.Voice, exemptFlags: ServerBanExemptFlags.IP));
    }
}
