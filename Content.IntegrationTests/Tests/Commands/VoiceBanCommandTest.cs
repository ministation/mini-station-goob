// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.CCVar;
using Content.Server.Administration.Commands;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Shared.Database;
using Robust.Server.Console;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using ClientVoiceChatManager = Content.Goobstation.Client.VoiceChat.VoiceChatManager;

namespace Content.IntegrationTests.Tests.Commands;

[TestFixture]
[TestOf(typeof(VoiceBanCommand))]
public sealed class VoiceBanCommandTest
{
    [Test]
    public async Task VoiceBanTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;

        var sPlayerManager = server.ResolveDependency<IPlayerManager>();
        var sConsole = server.ResolveDependency<IServerConsoleHost>();
        var sDatabase = server.ResolveDependency<IServerDbManager>();
        var sBans = server.ResolveDependency<IBanManager>();
        var sCfg = server.ResolveDependency<IConfigurationManager>();
        var cVoice = client.ResolveDependency<ClientVoiceChatManager>();
        var netMan = client.ResolveDependency<IClientNetManager>();
        var clientSession = sPlayerManager.Sessions.Single();
        var clientId = clientSession.UserId;

        Assert.That(netMan.IsConnected, Is.True);

        // Voice chat has to be enabled so that link requests are answered at all.
        await server.WaitPost(() => sCfg.SetCVar(GoobCVars.VoiceChatWebSocketBind, "127.0.0.1:0"));
        await server.WaitPost(() => sCfg.SetCVar(GoobCVars.VoiceChatEnabled, true));
        await pair.RunTicksSync(10);

        await server.WaitPost(() => sConsole.ExecuteCommand($"voiceban {clientSession.Name} test"));
        await pair.RunTicksSync(10);

        // A voice chat ban is stored separately and does not kick the player from the server.
        Assert.That(netMan.IsConnected, Is.True);
        Assert.That(sBans.IsVoiceBanned(clientId), Is.True);

        var voiceBans = await sDatabase.GetBansAsync(null, clientId, null, null, true, BanType.Voice);
        Assert.That(voiceBans, Has.Count.EqualTo(1));

        var ban = voiceBans.Single();
        Assert.That(ban.Type, Is.EqualTo(BanType.Voice));
        Assert.That(ban.Id, Is.Not.Null);

        Assert.That(await sDatabase.GetBansAsync(null, clientId, null, null, true, BanType.Server), Is.Empty);
        Assert.That(await sDatabase.GetBansAsync(null, clientId, null, null, true, BanType.Role), Is.Empty);

        // The client gets a ban response instead of a voice link code.
        await client.WaitPost(() => cVoice.RequestLink());
        await pair.RunTicksSync(10);
        Assert.That(cVoice.VoiceBanned, Is.True);
        Assert.That(cVoice.LinkUrl, Is.Null);
        Assert.That(cVoice.VoiceBanReason, Is.EqualTo("test"));

        await server.WaitPost(() => sConsole.ExecuteCommand($"voiceunban {ban.Id!.Value}"));
        await pair.RunTicksSync(10);

        Assert.That(sBans.IsVoiceBanned(clientId), Is.False);
        Assert.That(await sDatabase.GetBansAsync(null, clientId, null, null, false, BanType.Voice), Is.Empty);

        // After the pardon the client can get a new link code again.
        await client.WaitPost(() => cVoice.RequestLink());
        await pair.RunTicksSync(10);
        Assert.That(cVoice.VoiceBanned, Is.False);
        Assert.That(cVoice.LinkCode, Is.Not.Null);

        await server.WaitPost(() => sCfg.SetCVar(GoobCVars.VoiceChatEnabled, false));

        await pair.CleanReturnAsync();
    }
}
