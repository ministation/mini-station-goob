using Content.Corvax.Interfaces.Shared;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Reflection;
using Robust.Shared.Sandboxing;

namespace Content.SponsorImplementations.Server;

public sealed class EntryPoint: GameServer
{
    [Dependency] private readonly IReflectionManager _reflectionManager = default!;
    [Dependency] private readonly ISandboxHelper _sandbox = default!;
    [Dependency] private readonly ISponsorRecordProvider _sponsorRecordProvider = default!;

    public override void PreInit()
    {
        Logger.GetSawmill("SponsorImplementations").Info("Starting up");
        DependencyRegistration.Register(Dependencies);
        IoCManager.BuildGraph();
    }

    public override void PostInit()
    {
        IoCManager.InjectDependencies(this);
        IoCManager.Resolve<ISharedSponsorsManager>().Initialize();

        var types = _reflectionManager.GetAllChildren(typeof(ISponsorDataProvider));
        foreach (var type in types)
        {
            _sponsorRecordProvider.RegisterSponsorDataProvider((ISponsorDataProvider)_sandbox.CreateInstance(type));
        }
    }
}
