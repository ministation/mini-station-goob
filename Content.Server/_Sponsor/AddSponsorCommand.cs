using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._CorvaxGoob.TTS;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Traits;
using Content.SponsorImplementations.Server;
using Content.SponsorImplementations.Shared;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Sponsor;

[AdminCommand(AdminFlags.Host)]
public sealed class AddSponsorCommand: IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public string Command => "setsponsor";
    public string Description => "Adds sponsor to the server";
    public string Help => $"Usage: {Command} <target> <server priority join> <extra char slots> <color> Prototypes...";

    public async void Execute(IConsoleShell shell, string arg, string[] args)
    {
        if (IoCManager.Instance == null ||
            !IoCManager.Instance.TryResolveType(out ISponsorRecordProvider? _sponsorRecordProvider))
        {
            shell.WriteError("ISponsorRecordProvider not found");
            return;
        }

        if (args.Length < 4)
        {
            shell.WriteLine(Help);
            return;
        }

        var argsQueue = new Queue<string>(args);

        var located = await _locator.LookupIdByNameOrIdAsync(argsQueue.Dequeue());

        if (located == null)
        {
            shell.WriteLine("Could not locate player");
            return;
        }

        var oldData = _sponsorRecordProvider.GetSponsorDataProvider<DataBaseSponsorDataProvider>().GetSponsorInfo(located.UserId);

        var serverPriorityJoin = oldData?.ServerPriorityJoin ?? false;
        switch (argsQueue.Dequeue())
        {
            case "true":
                serverPriorityJoin = true;
                break;
            case "false":
                serverPriorityJoin = false;
                break;
        }

        var extraSlotsStr = argsQueue.Dequeue();
        var extraSlots = oldData?.ExtraCharSlots ?? 0;
        if (extraSlotsStr != "^" && !int.TryParse(extraSlotsStr, out extraSlots))
        {
            shell.WriteLine("Error parsing extra char slots");
            return;
        }

        var colorStr = argsQueue.Dequeue();
        var color = oldData?.Color;
        if (colorStr != "^")
        {
            if (Color.TryParse(colorStr, out var colorEns))
            {
                color = colorEns;
            }
            else
            {
                shell.WriteLine("Error parsing color");
                return;
            }
        }

        var prototypes = oldData?.Prototypes ?? [];

        if (argsQueue.TryDequeue(out var result))
        {
            if(result != "^")
            {
                prototypes.Clear();
                prototypes.Add(result);
            }

            while (argsQueue.TryDequeue(out result))
            {
                if (_prototypeManager.TryIndex<SponsorGroupPrototype>(result, out var prototype))
                {
                    prototypes.AddRange(prototype.Prototypes);
                    continue;
                }

                prototypes.Add(result);
            }
        }

        var data = new SponsorData()
        {
            Color = color,
            ExtraCharSlots = extraSlots,
            Prototypes = prototypes,
            ServerPriorityJoin = serverPriorityJoin,
        };

        await _sponsorRecordProvider.GetSponsorDataProvider<DataBaseSponsorDataProvider>()
            .SetSponsorInfo(located.UserId, data);
        _sponsorRecordProvider.SendSponsorDataToClient(located.UserId);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 0)
        {
            return CompletionResult.Empty;
        }

        if (args.Length == 1)
        {
            var options = _playerManager.Sessions.Select(c => c.Name).OrderBy(c => c).ToArray();
            return CompletionResult.FromHintOptions(options, "player ckey");
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(["true", "false", "^"],
                "boolean. Is this player has priority join? ^ if no change");
        }

        if (args.Length == 3)
        {
            return CompletionResult.FromHint("number or ^ of no change");
        }

        if (args.Length == 4)
        {
            return CompletionResult.FromHint("color or ^ of no change. Example: #223344 or White");
        }

        if (args.Length == 5)
        {
            return CompletionResult.FromHintOptions(
                [
                    ..GetOptions(),
                    new CompletionOption("^"),
                ],
                "prototypes for sponsor. For append list use ^");
        }

        return CompletionResult.FromHintOptions(GetOptions(), "prototypes for sponsor.");
    }

    private static IEnumerable<CompletionOption> GetOptions()
    {
        return CompletionPrototypeFactory.Instance
            .With<SponsorGroupPrototype>()
            .With<SpeciesPrototype>()
            .With<LoadoutPrototype>()
            .With<MarkingPrototype>()
            .With<TraitPrototype>()
            .With<TTSVoicePrototype>()
            .Build();
    }
}

public sealed class CompletionPrototypeFactory
{
    private readonly List<CompletionOption> _options = [];

    public static CompletionPrototypeFactory Instance => new();
    public CompletionPrototypeFactory With<T>() where T: class, IPrototype
    {
        _options.AddRange(CompletionHelper.PrototypeIDs<T>());
        return this;
    }

    public IEnumerable<CompletionOption> Build()
    {
        return _options;
    }
}

[AdminCommand(AdminFlags.Host)]
public sealed class ClearSponsorCommand: IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;

    public string Command => "clearsponsor";
    public string Description => "clear sponsor from the server";
    public string Help => $"Usage: {Command} <target>";

    public async void Execute(IConsoleShell shell, string arg, string[] args)
    {
        if (IoCManager.Instance == null ||
            !IoCManager.Instance.TryResolveType(out ISponsorRecordProvider? _sponsorRecordProvider))
        {
            shell.WriteError("ISponsorRecordProvider not found");
            return;
        }

        if (args.Length < 1)
        {
            shell.WriteLine(Help);
            return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(args[0]);

        if (located == null)
        {
            shell.WriteLine("Could not locate player");
            return;
        }

        await _sponsorRecordProvider.GetSponsorDataProvider<DataBaseSponsorDataProvider>()
            .SetSponsorInfo(located.UserId, null);
        _sponsorRecordProvider.SendSponsorDataToClient(located.UserId);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _playerManager.Sessions.Select(c => c.Name).OrderBy(c => c).ToArray();
            return CompletionResult.FromHintOptions(options, "player ckey");
        }

        return CompletionResult.Empty;
    }
}


[AdminCommand(AdminFlags.Host)]
public sealed class SetSponsorGroupCommand: IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public string Command => "setsponsorgroup";
    public string Description => "clear sponsor from the server";
    public string Help => $"Usage: {Command} <target> <group>";

    public async void Execute(IConsoleShell shell, string arg, string[] args)
    {
        if (IoCManager.Instance == null ||
            !IoCManager.Instance.TryResolveType(out ISponsorRecordProvider? _sponsorRecordProvider))
        {
            shell.WriteError("ISponsorRecordProvider not found");
            return;
        }

        if (args.Length < 2)
        {
            shell.WriteLine(Help);
            return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(args[0]);

        if (located == null)
        {
            shell.WriteError("Could not locate player");
            return;
        }

        if (!_prototypeManager.TryIndex<SponsorGroupPrototype>(args[1], out var prototype))
        {
            shell.WriteError("Could not find prototype");
            return;
        }

        await _sponsorRecordProvider.GetSponsorDataProvider<DataBaseSponsorDataProvider>()
            .SetSponsorInfo(located.UserId, prototype);
        _sponsorRecordProvider.SendSponsorDataToClient(located.UserId);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _playerManager.Sessions.Select(c => c.Name).OrderBy(c => c).ToArray();
            return CompletionResult.FromHintOptions(options, "player ckey");
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<SponsorGroupPrototype>(), "group prototype");
        }

        return CompletionResult.Empty;
    }
}
