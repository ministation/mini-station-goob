// SPDX-License-Identifier: MIT

using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests;

public sealed class LogErrorTest
{
    /// <summary>
    ///     This test ensures that error logs cause tests to fail.
    /// </summary>
    /// <remarks>
    ///     Mini: our RobustToolbox no longer throws inside sawmills. Instead, failing logs are collected by
    ///     PoolTestLogHandler and reported when the pair is returned, failing the test at that point.
    ///     We verify errors are recorded as failing logs, then clear them so this test itself passes.
    /// </remarks>
    [Test]
    public async Task TestLogErrorCausesTestFailure()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;

        var cfg = server.ResolveDependency<IConfigurationManager>();
        var serverLog = server.ResolveDependency<ILogManager>().RootSawmill;
        var clientLog = client.ResolveDependency<ILogManager>().RootSawmill;

        // Default cvar is properly configured
        Assert.That(cfg.GetCVar(RTCVars.FailureLogLevel), Is.EqualTo(LogLevel.Error));

        // Warnings don't cause tests to fail.
        await server.WaitPost(() => serverLog.Warning("test"));
        Assert.That(pair.ServerLogHandler.FailingLogs, Is.Empty);

        // But errors are recorded and reported as failures when the pair is returned.
        await server.WaitPost(() => serverLog.Error("test"));
        Assert.That(pair.ServerLogHandler.FailingLogs, Has.Count.EqualTo(1));

        await client.WaitPost(() => clientLog.Error("test"));
        Assert.That(pair.ClientLogHandler.FailingLogs, Has.Count.EqualTo(1));

        // Clear the intentional errors so this test doesn't fail on pair return.
        pair.ServerLogHandler.ClearFailingLogs();
        pair.ClientLogHandler.ClearFailingLogs();

        await pair.CleanReturnAsync();
    }
}